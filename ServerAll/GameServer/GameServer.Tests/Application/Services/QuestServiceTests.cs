using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Quest;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Domain.Entities.Quest;
using Shared.Infrastructure.Quests;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using GameServer.Tests.Infrastructure.Fakes.Services;
using AppWalletService = GameServer.Application.Domains.Wallet.WalletService;

namespace GameServer.Tests.Application.Services;

/// <summary>
/// 퀘스트 서비스 로직 — 수주/진행(ReportKill)/완료·보상 + 중복수령 차단. 보상은 실제 Progression/Wallet/Inventory 조합.
/// 카탈로그 시드: quest_slime_hunt(slime×3, exp50+gold100) · quest_slime_slayer(slime×5, exp80+potion×2) · quest_potion_collect.
/// </summary>
public class QuestServiceTests
{
    private const long UserId = 7L;

    // 대화 목표를 가진 유일한 시드 퀘스트(quests.json).
    private const string TalkQuestId = "quest_greet_elder";
    private const string TalkNpcId   = "npc_elder";

    private readonly FakeQuestRepository _quests = new();
    private readonly FakeWalletRepository _walletRepo = new();
    private readonly FakeInventoryRepository _invRepo = new();
    private readonly QuestService _service;

    public QuestServiceTests()
    {
        var progression = new ProgressionService(new FakeProgressionRepository(), new FakeEquipmentService());
        var wallet = new AppWalletService(_walletRepo);
        var inventory = new InventoryService(_invRepo, new CodexService(new FakeCodexRepository()));
        _service = new QuestService(_quests, progression, wallet, inventory,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<QuestService>.Instance);
    }

    [Fact]
    public async Task GetQuests는_전체_카탈로그를_미수주_상태로_반환한다()
    {
        var views = await _service.GetQuestsAsync(UserId);

        Assert.Equal(QuestCatalog.All.Count, views.Count);
        Assert.All(views, v => Assert.Equal(QuestProgressStatus.NotAccepted, v.Status));
    }

    [Fact]
    public async Task 수주하면_Accepted_상태가_된다()
    {
        var ok = await _service.AcceptAsync(UserId, "quest_slime_hunt");

        Assert.True(ok.Success);
        var view = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_slime_hunt");
        Assert.Equal(QuestProgressStatus.Accepted, view.Status);
    }

    [Fact]
    public async Task 이미_수주했거나_미존재_퀘스트는_수주_실패()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt");

        Assert.False((await _service.AcceptAsync(UserId, "quest_slime_hunt")).Success); // 중복
        Assert.False((await _service.AcceptAsync(UserId, "no_such_quest")).Success);    // 미존재
    }

    [Fact]
    public async Task ReportKill은_대상_몬스터_퀘스트만_진행시킨다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt"); // slime ×3

        await _service.ReportKillAsync(UserId, "goblin"); // 대상 아님 → 무변동
        var afterGoblin = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_slime_hunt");
        Assert.Equal(0, afterGoblin.Progress);

        await _service.ReportKillAsync(UserId, "creepy_demon");
        var afterSlime = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_slime_hunt");
        Assert.Equal(1, afterSlime.Progress);
    }

    [Fact]
    public async Task 진행이_required에_도달하면_Completed_이고_상한을_넘지_않는다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt"); // 3 필요

        for (int i = 0; i < 5; i++) // 5번 보고해도 3에서 멈춤
            await _service.ReportKillAsync(UserId, "creepy_demon");

        var view = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_slime_hunt");
        Assert.Equal(3, view.Progress);
        Assert.Equal(QuestProgressStatus.Completed, view.Status);
    }

    [Fact]
    public async Task 완료_퀘스트_보상수령은_골드를_지급하고_Claimed_가_된다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt"); // 완료 시 exp50 + gold100
        for (int i = 0; i < 3; i++) await _service.ReportKillAsync(UserId, "creepy_demon");

        var claim = await _service.ClaimRewardAsync(UserId, "quest_slime_hunt");

        Assert.True(claim.Success);
        Assert.Equal(100, claim.Reward!.Gold);
        Assert.Equal(100, await _walletRepo.GetBalanceAsync(UserId)); // 실제 지급
        var view = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_slime_hunt");
        Assert.Equal(QuestProgressStatus.Claimed, view.Status);
    }

    [Fact]
    public async Task 미완료_보상수령은_실패한다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt");
        await _service.ReportKillAsync(UserId, "creepy_demon"); // 1/3

        Assert.False((await _service.ClaimRewardAsync(UserId, "quest_slime_hunt")).Success);
    }

    [Fact]
    public async Task 보상은_한_번만_수령된다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_hunt");
        for (int i = 0; i < 3; i++) await _service.ReportKillAsync(UserId, "creepy_demon");

        await _service.ClaimRewardAsync(UserId, "quest_slime_hunt");
        var second = await _service.ClaimRewardAsync(UserId, "quest_slime_hunt");

        Assert.False(second.Success); // 이미 수령
        Assert.Equal(100, await _walletRepo.GetBalanceAsync(UserId)); // 이중 지급 없음
    }

    [Fact]
    public async Task ReportTalk은_대상_NPC_TalkToNpc퀘스트를_완료시킨다()
    {
        await _service.AcceptAsync(UserId, "quest_greet_elder"); // TalkToNpc npc_elder ×1

        await _service.ReportTalkAsync(UserId, "other_npc"); // 대상 아님 → 무진행
        Assert.Equal(QuestProgressStatus.Accepted,
            (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_greet_elder").Status);

        await _service.ReportTalkAsync(UserId, "npc_elder"); // 대상 → 완료(count 1)
        Assert.Equal(QuestProgressStatus.Completed,
            (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_greet_elder").Status);
    }

    [Fact]
    public async Task ReportTalk_반복은_진행을_넘기지_않는다_멱등()
    {
        await _service.AcceptAsync(UserId, "quest_greet_elder");

        await _service.ReportTalkAsync(UserId, "npc_elder");
        await _service.ReportTalkAsync(UserId, "npc_elder"); // 반복 — 상한(1)이라 무효

        var view = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == "quest_greet_elder");
        Assert.Equal(1, view.Progress);
    }

    [Fact]
    public async Task 아이템_보상_퀘스트는_인벤토리에_지급된다()
    {
        await _service.AcceptAsync(UserId, "quest_slime_slayer"); // exp80 + potion_hp_small ×2
        for (int i = 0; i < 5; i++) await _service.ReportKillAsync(UserId, "creepy_demon");

        var claim = await _service.ClaimRewardAsync(UserId, "quest_slime_slayer");

        Assert.True(claim.Success);
        var inv = await _invRepo.GetAllAsync(UserId);
        Assert.Contains(inv, i => i.ItemId == 1001 && i.Quantity == 2);
    }

    // ── ReportTalk 신뢰 경계 (F5) ────────────────────────────────────
    //
    // 근접 검증은 이 구조에서 불가능하다(서버는 NPC 위치도, Main 플레이어 위치도 모른다).
    // 서버가 보는 것은 "이 요청을 정상적으로 처리할 수 있는가" 뿐이다.
    // 불필요한 호출 자체를 막는 것은 클라 게이트의 몫(NPC.hasQuest + 수주 상태).

    [Fact]
    public async Task 대화_목표가_없는_NPC_는_저장소를_건드리지_않는다()
    {
        var counting = new CountingQuestRepository(_quests);
        var service = BuildService(counting);

        var advanced = await service.ReportTalkAsync(UserId, "npc_does_not_exist");

        Assert.Equal(0, advanced);
        // 카탈로그만 봐도 판정되는 요청이 DB 왕복을 유발하면 안 된다.
        Assert.Equal(0, counting.GetAllCalls);
    }

    [Fact]
    public async Task 수주하지_않았으면_진행하지_않는다()
    {
        var advanced = await _service.ReportTalkAsync(UserId, TalkNpcId);

        Assert.Equal(0, advanced);
    }

    [Fact]
    public async Task 수주한_대화_퀘스트는_진행되고_상한에서_멈춘다()
    {
        await _service.AcceptAsync(UserId, TalkQuestId);

        Assert.Equal(1, await _service.ReportTalkAsync(UserId, TalkNpcId)); // requiredCount=1 → 완료
        Assert.Equal(0, await _service.ReportTalkAsync(UserId, TalkNpcId)); // 상한 도달 → 무진행

        var view = (await _service.GetQuestsAsync(UserId)).Single(v => v.Def.QuestId == TalkQuestId);
        Assert.Equal(QuestProgressStatus.Completed, view.Status);
    }

    private QuestService BuildService(IQuestRepository repo)
    {
        var progression = new ProgressionService(new FakeProgressionRepository(), new FakeEquipmentService());
        var wallet = new AppWalletService(new FakeWalletRepository());
        var inventory = new InventoryService(new FakeInventoryRepository(), new CodexService(new FakeCodexRepository()));
        return new QuestService(repo, progression, wallet, inventory,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<QuestService>.Instance);
    }

    /// <summary>저장소 왕복 횟수를 세는 래퍼 — "카탈로그로 걸러지면 DB 를 안 본다" 를 관측한다.</summary>
    private sealed class CountingQuestRepository(IQuestRepository inner) : IQuestRepository
    {
        public int GetAllCalls { get; private set; }

        public Task<List<UserQuest>> GetAllForUserAsync(long userId, CancellationToken ct = default)
        {
            GetAllCalls++;
            return inner.GetAllForUserAsync(userId, ct);
        }

        public Task<UserQuest?> GetAsync(long userId, string questId, CancellationToken ct = default)
            => inner.GetAsync(userId, questId, ct);

        public Task UpsertAsync(UserQuest quest, CancellationToken ct = default)
            => inner.UpsertAsync(quest, ct);
    }
}

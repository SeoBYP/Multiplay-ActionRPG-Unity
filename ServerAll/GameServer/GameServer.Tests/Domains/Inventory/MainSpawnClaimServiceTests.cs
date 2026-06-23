using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;
using AppWalletService = GameServer.Application.Domains.Wallet.WalletService;

namespace GameServer.Tests.Domains.Inventory;

/// <summary>
/// Main 획득 서버 검증(B-lite) 핵심 로직 — 슬롯 검증·쿨다운 재청구 차단·서버 권위 roll·지급.
/// 실제 임베디드 데이터 사용: SpawnLayoutTable("main_field_01" 슬롯 1~3 = slime) + DropTableCatalog(slime→potion 보장).
/// Redis 만 fake 로 교체(IClaimCooldownStore) → 무한파밍 차단(쿨다운)을 Redis 없이 검증. main-spawn-claim.md.
/// </summary>
public class MainSpawnClaimServiceTests
{
    private const string MainMap = "main_field_01";

    private readonly FakeInventoryRepository _repository = new();
    private readonly FakeWalletRepository _walletRepository = new();
    private readonly FakeClaimCooldownStore _cooldown = new();
    private readonly ProgressionService _progression = new(new FakeProgressionRepository(), new Infrastructure.Fakes.Services.FakeEquipmentService());
    private readonly Infrastructure.Fakes.Services.FakeQuestService _quest = new();
    private readonly MainSpawnClaimService _service;

    public MainSpawnClaimServiceTests()
    {
        _service = new MainSpawnClaimService(
            new AppInventoryService(_repository, new GameServer.Application.Domains.Codex.CodexService(new FakeCodexRepository())),
            new AppWalletService(_walletRepository),
            _progression,
            _quest,
            _cooldown,
            NullLogger<MainSpawnClaimService>.Instance);
    }

    [Fact]
    public async Task ClaimExp_성공시_퀘스트에_킬을_보고한다()
    {
        await _service.ClaimExpAsync(7L, MainMap, 1); // slot 1 = slime

        Assert.Contains((7L, "slime"), _quest.ReportedKills);
    }

    [Fact]
    public async Task ClaimExp_쿨다운중이면_킬을_보고하지_않는다()
    {
        await _service.ClaimExpAsync(7L, MainMap, 1); // 1회차 점유
        _quest.ReportedKills.Clear();

        await _service.ClaimExpAsync(7L, MainMap, 1); // 쿨다운 중 → exp/킬보고 없음

        Assert.Empty(_quest.ReportedKills);
    }

    [Fact]
    public async Task ClaimKill은_맵에_없는_슬롯이면_거부한다()
    {
        var res = await _service.ClaimKillAsync(1L, MainMap, slotId: 999);

        Assert.False(res.Success);
        Assert.Empty(await _repository.GetAllAsync(1L)); // 위조 슬롯 = 지급 없음
    }

    [Fact]
    public async Task ClaimKill은_없는_맵이면_거부한다()
    {
        var res = await _service.ClaimKillAsync(1L, "hack_map", slotId: 1);

        Assert.False(res.Success);
        Assert.Empty(await _repository.GetAllAsync(1L));
    }

    [Fact]
    public async Task ClaimKill은_유효슬롯이면_서버roll로_보상을_지급한다()
    {
        var res = await _service.ClaimKillAsync(1L, MainMap, slotId: 1);

        Assert.True(res.Success, res.FailReason);
        Assert.Contains(res.Granted, g => g.ItemId == "potion_hp_small"); // slime 보장 드랍(서버 roll)
        var inv = await _repository.GetAllAsync(1L);
        Assert.Contains(inv, i => i.ItemId == "potion_hp_small" && i.Quantity >= 1);
    }

    [Fact]
    public async Task ClaimExp는_유효슬롯이면_킬_즉시_몬스터_exp를_적립한다()
    {
        // main_field_01 슬롯 1 = slime, MonsterCatalog slime.ExpReward = 20. 줍기와 무관하게 죽이면 즉시.
        var res = await _service.ClaimExpAsync(1L, MainMap, slotId: 1);

        Assert.True(res.Success, res.FailReason);
        Assert.Equal(20, res.ExpGained);
        var prog = await _progression.GetProgressionAsync(1L);
        Assert.Equal(20, prog.Exp); // 서버 권위 적립(영속)
    }

    [Fact]
    public async Task ClaimExp는_쿨다운_재청구시_exp를_적립하지_않는다()
    {
        await _service.ClaimExpAsync(1L, MainMap, slotId: 1); // 1회차 +20

        var second = await _service.ClaimExpAsync(1L, MainMap, slotId: 1); // exp 쿨다운 내 재청구

        Assert.True(second.Success);
        Assert.Equal(0, second.ExpGained); // exp 파밍도 쿨다운으로 상한
        var prog = await _progression.GetProgressionAsync(1L);
        Assert.Equal(20, prog.Exp);        // 누적 20 그대로(이중 적립 없음)
    }

    [Fact]
    public async Task ClaimExp와_ClaimKill은_쿨다운이_독립이다()
    {
        // exp(킬)와 아이템(줍기)은 별도 쿨다운 → exp 청구가 아이템 줍기를 막지 않는다.
        var exp = await _service.ClaimExpAsync(1L, MainMap, slotId: 1);
        var item = await _service.ClaimKillAsync(1L, MainMap, slotId: 1);

        Assert.Equal(20, exp.ExpGained);
        Assert.NotEmpty(item.Granted); // 같은 슬롯이어도 아이템 줍기는 별개 쿨다운이라 성공
    }

    [Fact]
    public async Task ClaimExp는_위조슬롯이면_거부한다()
    {
        var res = await _service.ClaimExpAsync(1L, MainMap, slotId: 999);

        Assert.False(res.Success);
        var prog = await _progression.GetProgressionAsync(1L);
        Assert.Equal(0, prog.Exp); // 적립 없음
    }

    [Fact]
    public async Task ClaimKill은_쿨다운_내_재청구를_거부한다()
    {
        // 1회차: 정상 지급(슬롯 점유).
        var first = await _service.ClaimKillAsync(1L, MainMap, slotId: 1);
        Assert.NotEmpty(first.Granted);
        var afterFirst = (await _repository.GetAllAsync(1L)).Single(i => i.ItemId == "potion_hp_small").Quantity;

        // 2회차(쿨다운 내, 무한 스폰 후 재청구 시도): 보상 없음 → 인벤토리 불변 = 무한파밍 차단.
        var second = await _service.ClaimKillAsync(1L, MainMap, slotId: 1);

        Assert.True(second.Success);        // 쿨다운은 에러 아님
        Assert.Empty(second.Granted);       // 보상 0
        var afterSecond = (await _repository.GetAllAsync(1L)).Single(i => i.ItemId == "potion_hp_small").Quantity;
        Assert.Equal(afterFirst, afterSecond);
    }

    /// <summary>
    /// Redis 대체 — NX 시맨틱만 모사(키 점유 1회). TTL 만료는 무시(단위테스트는 "2회차 거부"만 검증).
    /// </summary>
    private sealed class FakeClaimCooldownStore : IClaimCooldownStore
    {
        private readonly HashSet<string> _held = new();

        public Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default)
            => Task.FromResult(_held.Add(key)); // 처음=true(점유), 이미 있으면=false(쿨다운)
    }
}

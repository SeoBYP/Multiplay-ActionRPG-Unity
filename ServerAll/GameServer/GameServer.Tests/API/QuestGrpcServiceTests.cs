using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Progression;
using GameServer.Grpc.Quest;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using GameServer.Tests.Infrastructure.Fakes.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppWalletService = GameServer.Application.Domains.Wallet.WalletService;

namespace GameServer.Tests.API;

/// <summary>
/// QuestService gRPC 진입점 — 인증 + 도메인↔proto 매핑. 로직은 <see cref="Application.Services.QuestServiceTests"/>.
/// </summary>
public class QuestGrpcServiceTests
{
    private readonly FakeQuestRepository _quests = new();
    private readonly QuestGrpcService _service;

    public QuestGrpcServiceTests()
    {
        var progression = new ProgressionService(new FakeProgressionRepository(), new FakeEquipmentService());
        var wallet = new AppWalletService(new FakeWalletRepository());
        var inventory = new InventoryService(new FakeInventoryRepository(), new CodexService(new FakeCodexRepository()));
        var questService = new GameServer.Application.Domains.Quest.QuestService(_quests, progression, wallet, inventory);
        _service = new QuestGrpcService(questService, NullLogger<QuestGrpcService>.Instance);
    }

    [Fact]
    public async Task GetQuests는_전체_카탈로그를_반환한다()
    {
        var res = await _service.GetQuests(new GetQuestsRequest(), Authed(7L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(4, res.Quests.Count); // 시드 4종(slime_hunt·slime_slayer·potion_collect·greet_elder)
        Assert.Contains(res.Quests, q => q.QuestId == "quest_slime_hunt" && q.Status == QuestProgressStatus.NotAccepted);
    }

    [Fact]
    public async Task AcceptQuest_성공후_상태가_Accepted로_보인다()
    {
        Assert.True((await _service.AcceptQuest(new AcceptQuestRequest { QuestId = "quest_slime_hunt" }, Authed(7L))).Result.Success);

        var res = await _service.GetQuests(new GetQuestsRequest(), Authed(7L));
        Assert.Equal(QuestProgressStatus.Accepted, res.Quests.Single(q => q.QuestId == "quest_slime_hunt").Status);
    }

    [Fact]
    public async Task 미완료_보상수령은_실패로_매핑된다()
    {
        await _service.AcceptQuest(new AcceptQuestRequest { QuestId = "quest_slime_hunt" }, Authed(7L));

        var res = await _service.ClaimQuestReward(new ClaimQuestRewardRequest { QuestId = "quest_slime_hunt" }, Authed(7L));
        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task 미인증_조회는_거부된다()
    {
        var res = await _service.GetQuests(new GetQuestsRequest(), Anonymous());
        Assert.False(res.Result.Success);
    }

    // ── 테스트 더블 ──

    private static ServerCallContext Context(ClaimsPrincipal user)
    {
        var ctx = new FakeServerCallContext();
        ctx.UserState["__HttpContext"] = new DefaultHttpContext { User = user };
        return ctx;
    }

    private static ServerCallContext Authed(long userId)
        => Context(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) }, "test")));

    private static ServerCallContext Anonymous()
        => Context(new ClaimsPrincipal(new ClaimsIdentity()));

    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Dictionary<object, object> _userState = new();
        protected override string MethodCore => "Quest";
        protected override string HostCore => string.Empty;
        protected override string PeerCore => string.Empty;
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => new();
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());
        protected override IDictionary<object, object> UserStateCore => _userState;
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}

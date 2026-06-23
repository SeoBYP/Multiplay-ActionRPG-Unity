using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Application.Domains.Codex;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Grpc.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;
using AppGrantedItem = GameServer.Application.Domains.Inventory.GrantedItem;

namespace GameServer.Tests.API;

/// <summary>
/// InventoryService gRPC 진입점 검증 — ClaimKill(Main 획득 B-lite, 서버가 검증·roll·지급) + ConsumeItem(소모품 차감).
/// ClaimKill 의 슬롯/쿨다운/roll 로직 자체는 <see cref="MainSpawnClaimServiceTests"/> 가 검증한다.
/// 여기서는 gRPC 레이어(인증 + 결과 매핑)만 본다 → IMainSpawnClaimService 는 fake.
/// </summary>
public class InventoryGrpcServiceTests
{
    private readonly FakeInventoryRepository _repository = new();
    private readonly AppInventoryService _inventory;
    private readonly FakeMainSpawnClaimService _claim = new();
    private readonly RecordingConsumeQueue _consumeQueue = new();
    private readonly InventoryGrpcService _service;

    public InventoryGrpcServiceTests()
    {
        _inventory = new AppInventoryService(_repository, new CodexService(new FakeCodexRepository()));
        _service = new InventoryGrpcService(_inventory, _claim, _consumeQueue, NullLogger<InventoryGrpcService>.Instance);
    }

    // ── ClaimKill (Main 획득 B-lite — gRPC 레이어: 인증 + 매핑) ──

    [Fact]
    public async Task ClaimKill_성공시_서버가_roll한_보상을_매핑해_반환한다()
    {
        _claim.Result = MainClaimResult.Ok(new[] { new AppGrantedItem("potion_hp_small", 2, 5) });

        var res = await _service.ClaimKill(new ClaimKillRequest { MapId = "main_field_01", SlotId = 1 }, Authed(7L));

        Assert.True(res.Result.Success, res.Result.Message);
        var g = Assert.Single(res.Granted);
        Assert.Equal("potion_hp_small", g.ItemId);
        Assert.Equal(2, g.Qty);
        Assert.Equal(5, g.NewQuantity);
        Assert.Equal((7L, "main_field_01", 1), _claim.LastCall); // 진입점이 userId(JWT)·요청을 그대로 위임
    }

    [Fact]
    public async Task ClaimMonsterExp_성공시_획득_exp를_응답에_매핑한다()
    {
        _claim.ExpResult = MainExpClaimResult.Ok(20);

        var res = await _service.ClaimMonsterExp(new ClaimMonsterExpRequest { MapId = "main_field_01", SlotId = 1 }, Authed(1L));

        Assert.True(res.Result.Success);
        Assert.Equal(20, res.ExpGained);
        Assert.Equal((1L, "main_field_01", 1), _claim.LastExpCall); // userId(JWT)·요청을 그대로 위임
    }

    [Fact]
    public async Task ClaimMonsterExp_미인증은_거부되고_서비스를_호출하지_않는다()
    {
        var res = await _service.ClaimMonsterExp(new ClaimMonsterExpRequest { MapId = "main_field_01", SlotId = 1 }, Anonymous());

        Assert.False(res.Result.Success);
        Assert.Null(_claim.LastExpCall);
    }

    [Fact]
    public async Task ClaimKill_쿨다운이면_성공이지만_보상이_비어있다()
    {
        _claim.Result = MainClaimResult.OnCooldown();

        var res = await _service.ClaimKill(new ClaimKillRequest { MapId = "main_field_01", SlotId = 1 }, Authed(1L));

        Assert.True(res.Result.Success); // 쿨다운은 에러 아님 — 보상만 없음
        Assert.Empty(res.Granted);
    }

    [Fact]
    public async Task ClaimKill_검증실패_위조슬롯은_거부된다()
    {
        _claim.Result = MainClaimResult.Fail("invalid slot 999");

        var res = await _service.ClaimKill(new ClaimKillRequest { MapId = "main_field_01", SlotId = 999 }, Authed(1L));

        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task ClaimKill_미인증은_거부되고_서비스를_호출하지_않는다()
    {
        var res = await _service.ClaimKill(new ClaimKillRequest { MapId = "main_field_01", SlotId = 1 }, Anonymous());

        Assert.False(res.Result.Success);
        Assert.Null(_claim.LastCall); // 인증 게이트에서 차단 — 클레임 로직 미진입
    }

    // ── ConsumeItem (3.8 소모품 — 서버 권위 차감) ──

    [Fact]
    public async Task 보유한_소모품_사용은_성공하고_남은수량을_반환한다()
    {
        await _inventory.GrantItemAsync(1L, "potion_hp_small", 3);

        var res = await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Authed(1L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(2, res.RemainingQuantity);
    }

    [Fact]
    public async Task 미보유_소모품_사용은_거부된다()
    {
        var res = await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Authed(1L));

        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task 소비_성공시_PlayerConsumed를_발행한다_EffectId는_itemId()
    {
        await _inventory.GrantItemAsync(1L, "potion_hp_small", 1);

        await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Authed(1L));

        var msg = Assert.Single(_consumeQueue.Sent);
        Assert.Equal(1L, msg.UserId);
        Assert.Equal("potion_hp_small", msg.EffectId); // EffectId == itemId 규칙
    }

    [Fact]
    public async Task 소비_실패시_PlayerConsumed를_발행하지_않는다()
    {
        // 미보유 → 차감 실패 → 발행 없음(서버 권위 회복이 위조되지 않도록).
        await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Authed(1L));

        Assert.Empty(_consumeQueue.Sent);
    }

    [Fact]
    public async Task 보유보다_많이_사용하면_거부되고_변화가_없다()
    {
        await _inventory.GrantItemAsync(1L, "potion_hp_small", 2);

        var res = await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 5 }, Authed(1L));

        Assert.False(res.Result.Success);
        var inv = await _repository.GetAllAsync(1L);
        Assert.Equal(2, inv[0].Quantity);
    }

    [Fact]
    public async Task 미인증_사용은_거부된다()
    {
        var res = await _service.ConsumeItem(new ConsumeItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Anonymous());

        Assert.False(res.Result.Success);
    }

    // ── 테스트 더블 ──

    private sealed class FakeMainSpawnClaimService : IMainSpawnClaimService
    {
        public MainClaimResult Result = MainClaimResult.Ok(Array.Empty<AppGrantedItem>());
        public MainExpClaimResult ExpResult = MainExpClaimResult.Ok(0);
        public (long userId, string mapId, int slotId)? LastCall;
        public (long userId, string mapId, int slotId)? LastExpCall;

        public Task<MainClaimResult> ClaimKillAsync(long userId, string mapId, int slotId, CancellationToken ct = default)
        {
            LastCall = (userId, mapId, slotId);
            return Task.FromResult(Result);
        }

        public Task<MainExpClaimResult> ClaimExpAsync(long userId, string mapId, int slotId, CancellationToken ct = default)
        {
            LastExpCall = (userId, mapId, slotId);
            return Task.FromResult(ExpResult);
        }
    }

    /// <summary>발행 검증용 — ConsumeItem 성공 시 PlayerConsumedMessage 를 모은다.</summary>
    private sealed class RecordingConsumeQueue
        : Shared.Infrastructure.MessageQueue.IMessageQueue<Shared.Infrastructure.Messages.PlayerConsumedMessage>
    {
        public readonly List<Shared.Infrastructure.Messages.PlayerConsumedMessage> Sent = new();
        public Task EnqueueAsync(Shared.Infrastructure.Messages.PlayerConsumedMessage message)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
        public IAsyncEnumerable<Shared.Infrastructure.Messages.PlayerConsumedMessage> DequeueAllAsync(
            CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<Shared.Infrastructure.Messages.PlayerConsumedMessage>();
    }

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

    /// <summary>
    /// 최소 ServerCallContext 테스트 더블. GetUserId 는 UserState["__HttpContext"].User 만 읽으므로
    /// UserState 외 멤버는 기본값으로 충분(별도 Grpc 테스트 패키지 불필요).
    /// </summary>
    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Dictionary<object, object> _userState = new();

        protected override string MethodCore => "ClaimKill";
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

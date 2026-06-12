using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Application.Domains.Inventory;
using GameServer.Grpc.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;

namespace GameServer.Tests.API;

/// <summary>
/// Main 싱글 경로 지급(GrantItem) gRPC 진입점의 서버 가드 검증.
/// 가드는 진입점 전용(수량 상한) — catalog·amount 검증은 InventoryService(GrantItemAsync)가 수행하므로
/// 실제 InventoryService + FakeRepository 를 합성해 "가드 + 위임"을 함께 본다.
/// ※ 던전(co-op) 경로는 LootGrantConsumer 가 GrantItemAsync 를 직접 호출 → 이 cap 과 무관.
/// </summary>
public class InventoryGrpcServiceTests
{
    private const int OverCap = 100; // MaxGrantPerCall(99) 초과

    private readonly FakeInventoryRepository _repository = new();
    private readonly RecordingConsumeQueue _consumeQueue = new();
    private readonly InventoryGrpcService _service;

    public InventoryGrpcServiceTests()
    {
        _service = new InventoryGrpcService(
            new AppInventoryService(_repository),
            _consumeQueue,
            NullLogger<InventoryGrpcService>.Instance);
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
        // Grpc.AspNetCore 의 GetHttpContext() 는 UserState["__HttpContext"] 를 읽는다 → GetUserId 가 User 클레임을 본다.
        var ctx = new FakeServerCallContext();
        ctx.UserState["__HttpContext"] = new DefaultHttpContext { User = user };
        return ctx;
    }

    private static ServerCallContext Authed(long userId)
        => Context(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) }, "test")));

    private static ServerCallContext Anonymous()
        => Context(new ClaimsPrincipal(new ClaimsIdentity()));

    [Fact]
    public async Task 정상_지급은_성공하고_보유수량을_반환한다()
    {
        var res = await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 3 }, Authed(1L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(3, res.NewQuantity);
        var inv = await _repository.GetAllAsync(1L);
        Assert.Equal(3, inv[0].Quantity);
    }

    [Fact]
    public async Task 수량상한_초과는_거부되고_지급되지_않는다()
    {
        var res = await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = OverCap }, Authed(1L));

        Assert.False(res.Result.Success);
        Assert.Empty(await _repository.GetAllAsync(1L));
    }

    [Fact]
    public async Task 수량이_0이하이면_거부된다()
    {
        Assert.False((await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 0 }, Authed(1L))).Result.Success);
        Assert.False((await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = -5 }, Authed(1L))).Result.Success);
        Assert.Empty(await _repository.GetAllAsync(1L));
    }

    [Fact]
    public async Task 미존재_itemId는_거부되고_지급되지_않는다()
    {
        var res = await _service.GrantItem(new GrantItemRequest { ItemId = "hack_sword_9000", Qty = 1 }, Authed(1L));

        Assert.False(res.Result.Success);
        Assert.Empty(await _repository.GetAllAsync(1L));
    }

    [Fact]
    public async Task 미인증_userId없음은_거부된다()
    {
        var res = await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Anonymous());

        Assert.False(res.Result.Success);
        Assert.Empty(await _repository.GetAllAsync(1L));
    }

    // ── ConsumeItem (3.8 소모품 — 서버 권위 차감) ──

    [Fact]
    public async Task 보유한_소모품_사용은_성공하고_남은수량을_반환한다()
    {
        await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 3 }, Authed(1L));

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
        await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 1 }, Authed(1L));

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
        await _service.GrantItem(new GrantItemRequest { ItemId = "potion_hp_small", Qty = 2 }, Authed(1L));

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

    /// <summary>
    /// 최소 ServerCallContext 테스트 더블. GetUserId 는 UserState["__HttpContext"].User 만 읽으므로
    /// UserState 외 멤버는 기본값으로 충분(별도 Grpc 테스트 패키지 불필요).
    /// </summary>
    private sealed class FakeServerCallContext : ServerCallContext
    {
        private readonly Dictionary<object, object> _userState = new();

        protected override string MethodCore => "GrantItem";
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

using System.Security.Claims;
using GameServer.API.Services;
using GameServer.Application.Domains.Equipment;
using GameServer.Grpc.Equipment;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;
using AppEquipmentService = GameServer.Application.Domains.Equipment.EquipmentService;

namespace GameServer.Tests.API;

/// <summary>
/// EquipmentService gRPC 진입점 검증 — 인증 게이트 + 장착/해제/조회 + proto↔도메인 슬롯 매핑.
/// 장착 검증(소유·장비여부) 로직은 <see cref="Application.Services.EquipmentServiceTests"/> 가 담당.
/// 여기서는 gRPC 레이어(인증 + 슬롯 enum 매핑 + 결과 매핑)만 본다 → 실제 서비스 + Fake 저장소.
/// </summary>
public class EquipmentGrpcServiceTests
{
    private readonly FakeInventoryRepository _inventoryRepo = new();
    private readonly FakeEquipmentRepository _equipmentRepo = new();
    private readonly EquipmentGrpcService _service;

    public EquipmentGrpcServiceTests()
    {
        var inventory = new AppInventoryService(_inventoryRepo, new GameServer.Application.Domains.Codex.CodexService(new FakeCodexRepository()));
        var equipment = new AppEquipmentService(_equipmentRepo, inventory);
        _service = new EquipmentGrpcService(equipment, NullLogger<EquipmentGrpcService>.Instance);
    }

    private async Task GrantAsync(long userId, string itemId)
        => await _inventoryRepo.AddQuantityAsync(userId, itemId, 1, maxStack: 1);

    [Fact]
    public async Task 보유한_장비_장착은_성공하고_서버가_결정한_슬롯을_반환한다()
    {
        await GrantAsync(1L, "sword_basic");

        var res = await _service.Equip(new EquipRequest { ItemId = "sword_basic" }, Authed(1L));

        Assert.True(res.Result.Success, res.Result.Message);
        Assert.Equal(EquipmentType.Weapon, res.Slot);
        Assert.Equal("sword_basic", res.ItemId);
    }

    [Fact]
    public async Task 미보유_장비_장착은_거부된다()
    {
        var res = await _service.Equip(new EquipRequest { ItemId = "sword_basic" }, Authed(1L));

        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task 장착_미인증은_거부된다()
    {
        var res = await _service.Equip(new EquipRequest { ItemId = "sword_basic" }, Anonymous());

        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task 해제는_슬롯을_비우고_성공한다()
    {
        await GrantAsync(1L, "armor_leather");
        await _service.Equip(new EquipRequest { ItemId = "armor_leather" }, Authed(1L));

        var res = await _service.Unequip(new UnequipRequest { Slot = EquipmentType.Armor }, Authed(1L));

        Assert.True(res.Result.Success, res.Result.Message);
        var get = await _service.GetEquipment(new GetEquipmentRequest(), Authed(1L));
        Assert.Empty(get.Items);
    }

    [Fact]
    public async Task 미지정_슬롯_해제는_거부된다()
    {
        var res = await _service.Unequip(new UnequipRequest { Slot = EquipmentType.Unspecified }, Authed(1L));

        Assert.False(res.Result.Success);
    }

    [Fact]
    public async Task GetEquipment는_착용_세트를_슬롯과_함께_반환한다()
    {
        await GrantAsync(1L, "sword_basic");
        await GrantAsync(1L, "armor_leather");
        await _service.Equip(new EquipRequest { ItemId = "sword_basic" }, Authed(1L));
        await _service.Equip(new EquipRequest { ItemId = "armor_leather" }, Authed(1L));

        var res = await _service.GetEquipment(new GetEquipmentRequest(), Authed(1L));

        Assert.True(res.Result.Success);
        Assert.Equal(2, res.Items.Count);
        Assert.Contains(res.Items, i => i.Slot == EquipmentType.Weapon && i.ItemId == "sword_basic");
        Assert.Contains(res.Items, i => i.Slot == EquipmentType.Armor && i.ItemId == "armor_leather");
    }

    [Fact]
    public async Task GetEquipment_미인증은_거부된다()
    {
        var res = await _service.GetEquipment(new GetEquipmentRequest(), Anonymous());

        Assert.False(res.Result.Success);
    }

    // ── 인증 컨텍스트 하니스(InventoryGrpcServiceTests 와 동형) ──

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

        protected override string MethodCore => "Equip";
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

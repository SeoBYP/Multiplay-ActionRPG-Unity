using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using AppInventoryService = GameServer.Application.Domains.Inventory.InventoryService;

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
    private readonly FakeClaimCooldownStore _cooldown = new();
    private readonly MainSpawnClaimService _service;

    public MainSpawnClaimServiceTests()
    {
        _service = new MainSpawnClaimService(
            new AppInventoryService(_repository),
            _cooldown,
            NullLogger<MainSpawnClaimService>.Instance);
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

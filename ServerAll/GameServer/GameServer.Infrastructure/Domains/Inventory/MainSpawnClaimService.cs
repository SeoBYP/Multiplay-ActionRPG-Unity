using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Loot;
using Shared.Infrastructure.Spawn;

namespace GameServer.Infrastructure.Domains.Inventory;

/// <summary>
/// Main 획득 서버 검증(B-lite) 구현. 정본 = docs/wiki/main-spawn-claim.md.
///
/// - 슬롯 검증: `SpawnLayoutTable`(서버가 보유한 map 스폰 데이터)에 (mapId, slotId)가 존재해야 한다.
/// - 쿨다운: Redis `SET NX PX` 한 줄로 원자 처리 — 키 없으면 클레임 성공+TTL 생성, 있으면 쿨다운 중(재청구 차단).
/// - roll/지급: 서버 권위 `DropTableCatalog.Roll`(클라 roll 불신) → `IInventoryService.GrantItemAsync`.
///   → 파밍률이 맵 설계치(슬롯수 ÷ 쿨다운)로 상한 = 무한 파밍 불가.
/// </summary>
public sealed class MainSpawnClaimService : IMainSpawnClaimService
{
    /// <summary>데이터 누락 방어 — 쿨다운 0(=무한 클레임)을 막기 위한 하한.</summary>
    private const int MinCooldownMs = 1000;

    private const string KeyPrefix = "mainclaim";

    private readonly IInventoryService _inventory;
    private readonly IClaimCooldownStore _cooldown;
    private readonly ILogger<MainSpawnClaimService> _logger;

    public MainSpawnClaimService(
        IInventoryService inventory,
        IClaimCooldownStore cooldown,
        ILogger<MainSpawnClaimService> logger)
    {
        _inventory = inventory;
        _cooldown = cooldown;
        _logger = logger;
    }

    public async Task<MainClaimResult> ClaimKillAsync(long userId, string mapId, int slotId, CancellationToken ct = default)
    {
        // 1. 슬롯 검증 — 서버가 보유한 map 스폰 데이터에 존재해야(위조 맵/슬롯 거부).
        MapSpawnLayout layout;
        try
        {
            layout = SpawnLayoutTable.Get(mapId);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("ClaimKill rejected: unknown map '{Map}' (user {User})", mapId, userId);
            return MainClaimResult.Fail($"unknown map '{mapId}'");
        }

        var slot = layout.Monsters.FirstOrDefault(m => m.SlotId == slotId);
        if (slotId <= 0 || slot is null)
        {
            _logger.LogWarning("ClaimKill rejected: invalid slot {Slot} in map '{Map}' (user {User})", slotId, mapId, userId);
            return MainClaimResult.Fail($"invalid slot {slotId} in map '{mapId}'");
        }

        // 2. 쿨다운 — 키 없으면 점유(클레임) / 있으면 쿨다운 중(재청구 차단). 원자적(SET NX PX).
        var cooldown = TimeSpan.FromMilliseconds(Math.Max(slot.RespawnCooldownMs, MinCooldownMs));
        var key = $"{KeyPrefix}:{userId}:{mapId}:{slotId}";
        var claimed = await _cooldown.TryClaimAsync(key, cooldown, ct);
        if (!claimed)
        {
            _logger.LogInformation("ClaimKill on cooldown: user {User} map {Map} slot {Slot}", userId, mapId, slotId);
            return MainClaimResult.OnCooldown(); // 보상 없음(에러 아님 — 정상 재청구 차단)
        }

        // 3. 서버 권위 roll — 클라 roll 불신. 같은 DropTableRoll(던전·Main 공유).
        var drops = DropTableCatalog.Roll(slot.MonsterId, Random.Shared);

        // 4. 지급.
        var granted = new List<GrantedItem>();
        foreach (var d in drops)
        {
            var g = await _inventory.GrantItemAsync(userId, d.ItemId, d.Qty, ct);
            if (g.Success)
                granted.Add(new GrantedItem(d.ItemId, d.Qty, g.NewQuantity));
            else
                _logger.LogWarning("ClaimKill grant failed: {Item} x{Qty} — {Reason}", d.ItemId, d.Qty, g.FailReason);
        }

        _logger.LogInformation(
            "ClaimKill granted {Count} item(s): user {User} map {Map} slot {Slot} monster {Monster}",
            granted.Count, userId, mapId, slotId, slot.MonsterId);
        return MainClaimResult.Ok(granted);
    }
}

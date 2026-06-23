using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Loot;
using Shared.Infrastructure.Monsters;
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

    private const string ItemKeyPrefix = "mainclaim"; // 아이템 줍기 쿨다운
    private const string ExpKeyPrefix = "mainexp";    // 경험치 킬 쿨다운(아이템과 독립)

    private readonly IInventoryService _inventory;
    private readonly IWalletService _wallet;
    private readonly IProgressionService _progression;
    private readonly IQuestService _quest;
    private readonly IClaimCooldownStore _cooldown;
    private readonly ILogger<MainSpawnClaimService> _logger;

    public MainSpawnClaimService(
        IInventoryService inventory,
        IWalletService wallet,
        IProgressionService progression,
        IQuestService quest,
        IClaimCooldownStore cooldown,
        ILogger<MainSpawnClaimService> logger)
    {
        _inventory = inventory;
        _wallet = wallet;
        _progression = progression;
        _quest = quest;
        _cooldown = cooldown;
        _logger = logger;
    }

    public async Task<MainClaimResult> ClaimKillAsync(long userId, string mapId, int slotId, CancellationToken ct = default)
    {
        var (slot, fail) = ValidateSlot(mapId, slotId, userId, "ClaimKill");
        if (slot is null)
            return MainClaimResult.Fail(fail!);

        // 아이템 쿨다운(줍기) — 키 없으면 점유 / 있으면 재청구 차단. 원자적(SET NX PX).
        if (!await TryClaimCooldownAsync(ItemKeyPrefix, userId, mapId, slotId, slot.RespawnCooldownMs, ct))
        {
            _logger.LogInformation("ClaimKill on cooldown: user {User} map {Map} slot {Slot}", userId, mapId, slotId);
            return MainClaimResult.OnCooldown();
        }

        // 서버 권위 roll(클라 roll 불신, 던전·Main 공유) → 아이템 지급.
        var drops = DropTableCatalog.Roll(slot.MonsterId, Random.Shared);
        var granted = new List<GrantedItem>();
        foreach (var d in drops)
        {
            // 골드는 통화 — 지갑 잔액으로 적립(NewQuantity = 잔액). 그 외는 인벤토리 지급(3.4).
            if (Currencies.IsCurrency(d.ItemId))
            {
                var balance = await _wallet.AddAsync(userId, d.Qty, ct);
                granted.Add(new GrantedItem(d.ItemId, d.Qty, (int)balance));
                continue;
            }

            var g = await _inventory.GrantItemAsync(userId, d.ItemId, d.Qty, ct);
            if (g.Success)
                granted.Add(new GrantedItem(d.ItemId, d.Qty, g.NewQuantity));
            else
                _logger.LogWarning("ClaimKill grant failed: {Item} x{Qty} — {Reason}", d.ItemId, d.Qty, g.FailReason);
        }

        _logger.LogInformation("ClaimKill granted {Count} item(s): user {User} map {Map} slot {Slot} monster {Monster}",
            granted.Count, userId, mapId, slotId, slot.MonsterId);
        return MainClaimResult.Ok(granted);
    }

    public async Task<MainExpClaimResult> ClaimExpAsync(long userId, string mapId, int slotId, CancellationToken ct = default)
    {
        var (slot, fail) = ValidateSlot(mapId, slotId, userId, "ClaimMonsterExp");
        if (slot is null)
            return MainExpClaimResult.Fail(fail!);

        // exp 쿨다운(킬) — 아이템 줍기와 독립된 키. 죽이면 즉시 청구되며, 줍기 여부와 무관.
        if (!await TryClaimCooldownAsync(ExpKeyPrefix, userId, mapId, slotId, slot.RespawnCooldownMs, ct))
        {
            _logger.LogInformation("ClaimMonsterExp on cooldown: user {User} map {Map} slot {Slot}", userId, mapId, slotId);
            return MainExpClaimResult.OnCooldown(); // exp 0(에러 아님)
        }

        // 서버 권위 — 몬스터 정의의 ExpReward(클라 신뢰 0). 쿨다운 통과 1회만 적립 = exp 파밍률 상한.
        long expReward = MonsterCatalog.Get(slot.MonsterId).ExpReward;
        if (expReward > 0)
            await _progression.AddExpAsync(userId, expReward, ct);

        // 퀘스트 진행(4.4) — 쿨다운 통과 = 진짜 킬 1회. KillMonster 퀘스트 서버 권위 +1(위조 불가).
        await _quest.ReportKillAsync(userId, slot.MonsterId, ct);

        _logger.LogInformation("ClaimMonsterExp +{Exp} exp: user {User} map {Map} slot {Slot} monster {Monster}",
            expReward, userId, mapId, slotId, slot.MonsterId);
        return MainExpClaimResult.Ok(expReward);
    }

    /// <summary>슬롯 검증 — 서버가 보유한 map 스폰 데이터에 (mapId,slotId)가 존재해야(위조 거부). 실패 시 사유 반환.</summary>
    private (MonsterSpawnDef? Slot, string? FailReason) ValidateSlot(string mapId, int slotId, long userId, string op)
    {
        MapSpawnLayout layout;
        try
        {
            layout = SpawnLayoutTable.Get(mapId);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("{Op} rejected: unknown map '{Map}' (user {User})", op, mapId, userId);
            return (null, $"unknown map '{mapId}'");
        }

        var slot = layout.Monsters.FirstOrDefault(m => m.SlotId == slotId);
        if (slotId <= 0 || slot is null)
        {
            _logger.LogWarning("{Op} rejected: invalid slot {Slot} in map '{Map}' (user {User})", op, slotId, mapId, userId);
            return (null, $"invalid slot {slotId} in map '{mapId}'");
        }
        return (slot, null);
    }

    /// <summary>per-user 쿨다운 점유(SET NX PX). true=점유 성공(클레임 가능) / false=쿨다운 중.</summary>
    private Task<bool> TryClaimCooldownAsync(string prefix, long userId, string mapId, int slotId, int cooldownMs, CancellationToken ct)
    {
        var cooldown = TimeSpan.FromMilliseconds(Math.Max(cooldownMs, MinCooldownMs));
        var key = $"{prefix}:{userId}:{mapId}:{slotId}";
        return _cooldown.TryClaimAsync(key, cooldown, ct);
    }
}

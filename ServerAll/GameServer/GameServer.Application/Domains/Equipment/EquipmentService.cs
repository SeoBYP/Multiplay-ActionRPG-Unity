using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Equipment;

/// <summary>
/// 장비 서비스 구현. 장비 정의(EquipmentCatalog)·소유(IInventoryService) 검증 후 착용 상태를 저장소에 위임한다.
/// 스탯 합산(GetEquippedStatsAsync)도 여기 응집 — Progression 의 GetStatsAsync 는 결과만 더한다(3.2.5).
/// </summary>
public sealed class EquipmentService(
    IEquipmentRepository repository,
    IInventoryService inventory) : IEquipmentService
{
    public Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default)
        => repository.GetEquippedAsync(userId, ct);

    public async Task<EquipResult> EquipAsync(long userId, string itemId, CancellationToken ct = default)
    {
        var def = EquipmentCatalog.Get(itemId);
        if (def is null)
            return EquipResult.Fail(default, $"'{itemId}' is not equippable");

        // 소유 검증 — 보유하지 않은 장비는 착용 불가(치팅 방지). 소유는 인벤토리가 단일 진실.
        var owned = await inventory.GetInventoryAsync(userId, ct);
        if (!owned.Any(i => i.ItemId == itemId && i.Quantity > 0))
            return EquipResult.Fail(def.Slot, "not owned");

        // 교체형 — 같은 슬롯의 기존 착용은 SetAsync 가 덮어쓴다(아이템은 인벤토리에 그대로).
        await repository.SetAsync(userId, def.Slot, itemId, ct);
        return EquipResult.Ok(def.Slot, itemId);
    }

    public async Task<EquipResult> UnequipAsync(long userId, EquipmentType slot, CancellationToken ct = default)
    {
        // 멱등 — 비어 있어도 성공(빈 슬롯 = ItemId 빈 문자열).
        await repository.ClearAsync(userId, slot, ct);
        return EquipResult.Ok(slot, string.Empty);
    }

    public async Task<EquipmentStatModifier> GetEquippedStatsAsync(long userId, CancellationToken ct = default)
    {
        var equipped = await repository.GetEquippedAsync(userId, ct);

        var total = default(EquipmentStatModifier);
        foreach (var e in equipped)
        {
            var def = EquipmentCatalog.Get(e.ItemId);
            if (def is not null)
                total = total.Add(def.Stats);
        }

        return total;
    }
}

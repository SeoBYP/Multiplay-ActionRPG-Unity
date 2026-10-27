using Shared.Gameplay.Equipment;

namespace GameServer.Application.Domains.Equipment;

/// <summary>
/// 장착/해제 결과. 성공 시 영향받은 슬롯과 itemId.
/// 해제는 멱등(비어 있어도 성공) — ItemId 0 = "해제 후 빈 슬롯".
/// </summary>
public sealed record EquipResult(EquipmentType Slot, int ItemId, bool Success, string? FailReason = null)
{
    public static EquipResult Ok(EquipmentType slot, int itemId) => new(slot, itemId, true);

    public static EquipResult Fail(EquipmentType slot, string reason) => new(slot, 0, false, reason);
}

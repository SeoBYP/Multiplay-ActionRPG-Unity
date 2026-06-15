using Shared.Gameplay.Equipment;

namespace GameServer.Application.Domains.Equipment;

/// <summary>
/// 장착/해제 결과. gRPC(3.2.6)·테스트가 받는 응답. 성공 시 영향받은 슬롯과 itemId.
/// 해제는 멱등(비어 있어도 성공) — ItemId 가 빈 문자열이면 "해제 후 빈 슬롯".
/// </summary>
public sealed record EquipResult(EquipmentType Slot, string ItemId, bool Success, string? FailReason = null)
{
    public static EquipResult Ok(EquipmentType slot, string itemId) => new(slot, itemId, true);

    public static EquipResult Fail(EquipmentType slot, string reason) => new(slot, string.Empty, false, reason);
}

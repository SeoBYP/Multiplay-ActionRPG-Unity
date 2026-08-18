using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Equipment.Interfaces;

/// <summary>
/// 장비 도메인 서비스. 장착/해제/조회 + 스탯 합산 제공.
/// 장착 검증(장비인가·소유했는가)은 여기서 — 저장소는 착용 상태만 다룬다.
/// </summary>
public interface IEquipmentService
{
    /// <summary>유저의 현재 착용 세트.</summary>
    Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 아이템을 장착한다. 슬롯은 EquipmentCatalog 가 결정(교체형). 장비가 아니거나 미보유면 실패.
    /// </summary>
    Task<EquipResult> EquipAsync(long userId, string itemId, CancellationToken ct = default);

    /// <summary>슬롯의 장비를 해제한다(멱등 — 비어 있어도 성공). 아이템은 인벤토리에 그대로 남는다.</summary>
    Task<EquipResult> UnequipAsync(long userId, EquipmentType slot, CancellationToken ct = default);

    /// <summary>
    /// 착용 세트의 **가산 스탯 합산**(EquipmentCatalog Σ). 2.4 스탯 합산 권위(`GetStatsAsync`, 3.2.5)가 호출.
    /// 합산 로직을 장비 도메인에 응집 — Progression 은 base 위에 더하기만 한다.
    /// </summary>
    Task<EquipmentStatModifier> GetEquippedStatsAsync(long userId, CancellationToken ct = default);
}

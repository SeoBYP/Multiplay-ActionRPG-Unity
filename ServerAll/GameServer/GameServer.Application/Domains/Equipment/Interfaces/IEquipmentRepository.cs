using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;

namespace GameServer.Application.Domains.Equipment.Interfaces;

/// <summary>
/// 착용 상태 영속 저장소. Cache-Aside + Delete 패턴(networking.md).
/// 정의(EquipmentCatalog)는 다루지 않는다 — (UserId,Slot)→ItemId 만 영속.
/// </summary>
public interface IEquipmentRepository
{
    /// <summary>유저의 전체 착용 슬롯을 읽는다(캐시 우선). 빈 슬롯은 포함하지 않음 — 착용분만.</summary>
    Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default);

    /// <summary>(userId, slot)에 itemId 를 착용(upsert — 기존 착용 교체). DB 저장 후 캐시 DEL.</summary>
    Task SetAsync(long userId, EquipmentType slot, string itemId, CancellationToken ct = default);

    /// <summary>(userId, slot) 착용을 해제(행 삭제). 착용분이 있었으면 true. DB 저장 후 캐시 DEL.</summary>
    Task<bool> ClearAsync(long userId, EquipmentType slot, CancellationToken ct = default);
}

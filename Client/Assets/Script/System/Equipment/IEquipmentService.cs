using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Shared.Gameplay.Equipment;

namespace Game.System.Equipment
{
    /// <summary>
    /// 장비 착용 오케스트레이션. gRPC(EquipmentService) 결과를 도메인 타입으로 정규화한다(proto 은닉).
    /// 장착/해제 성공 시 <see cref="OnChanged"/> 를 발행 — 인벤토리에서 장착해도 장비창이 즉시 갱신되도록.
    /// </summary>
    public interface IEquipmentService
    {
        /// <summary>장착/해제로 착용 세트가 바뀌면 발행. 구독자(EquipmentModel)가 재조회한다.</summary>
        event Action OnChanged;

        UniTask<(EquipmentResult Result, IReadOnlyList<EquippedItemData> Items)> GetEquippedAsync(CancellationToken ct = default);

        /// <summary>아이템 장착(슬롯은 서버가 결정). 성공 시 장착된 슬롯.</summary>
        UniTask<(EquipmentResult Result, EquipmentType Slot)> EquipAsync(string itemId, CancellationToken ct = default);

        /// <summary>슬롯 해제(멱등).</summary>
        UniTask<EquipmentResult> UnequipAsync(EquipmentType slot, CancellationToken ct = default);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Equipment;
using UnityEngine;
using DomainType = Shared.Gameplay.Equipment.EquipmentType;
using ProtoType = GameServer.Grpc.Equipment.EquipmentType;

namespace Game.System.Equipment
{
    /// <summary>
    /// 장비 서비스. gRPC(EquipmentService.Equip/Unequip/GetEquipment) 호출 → 도메인 변환.
    /// proto EquipmentType ↔ 공통 EquipmentType 은 정수값 1:1(캐스팅). 성공 시 OnChanged 발행.
    /// </summary>
    public sealed class EquipmentService : IEquipmentService
    {
        private readonly IEquipmentGrpcService _grpc;

        public event Action OnChanged;

        public EquipmentService(IEquipmentGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(EquipmentResult Result, IReadOnlyList<EquippedItemData> Items)> GetEquippedAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetEquipmentAsync(new GetEquipmentRequest(), ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[EquipmentService] GetEquipment 실패: code={res.Result.ErrorCode}");
                    return (EquipmentResult.Failed, Array.Empty<EquippedItemData>());
                }

                var items = new List<EquippedItemData>(res.Items.Count);
                foreach (var info in res.Items)
                    items.Add(new EquippedItemData((DomainType)(int)info.Slot, info.ItemId));

                return (EquipmentResult.Success, items);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentService] GetEquipment 예외: {e.Message}");
                return (EquipmentResult.Failed, Array.Empty<EquippedItemData>());
            }
        }

        public async UniTask<(EquipmentResult Result, DomainType Slot)> EquipAsync(string itemId, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.EquipAsync(new EquipRequest { ItemId = itemId }, ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[EquipmentService] Equip 실패: {itemId} code={res.Result.ErrorCode}");
                    return (EquipmentResult.Failed, DomainType.None);
                }

                OnChanged?.Invoke();
                return (EquipmentResult.Success, (DomainType)(int)res.Slot);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentService] Equip 예외: {e.Message}");
                return (EquipmentResult.Failed, DomainType.None);
            }
        }

        public async UniTask<EquipmentResult> UnequipAsync(DomainType slot, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.UnequipAsync(new UnequipRequest { Slot = (ProtoType)(int)slot }, ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[EquipmentService] Unequip 실패: {slot} code={res.Result.ErrorCode}");
                    return EquipmentResult.Failed;
                }

                OnChanged?.Invoke();
                return EquipmentResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentService] Unequip 예외: {e.Message}");
                return EquipmentResult.Failed;
            }
        }
    }
}

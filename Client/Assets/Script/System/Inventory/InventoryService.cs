using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Inventory;
using UnityEngine;

namespace Game.System.Inventory
{
    /// <summary>
    /// 인벤토리 서비스. gRPC(InventoryService.GetInventory) 호출 → 도메인 DTO 변환.
    /// </summary>
    public sealed class InventoryService : IInventoryService
    {
        private readonly IInventoryGrpcService _grpc;

        public InventoryService(IInventoryGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(InventoryResult Result, IReadOnlyList<InventoryItemData> Items)> GetInventoryAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetInventoryAsync(new GetInventoryRequest(), ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[InventoryService] GetInventory 실패: code={res.Result.ErrorCode}");
                    return (InventoryResult.Failed, Array.Empty<InventoryItemData>());
                }

                var items = new List<InventoryItemData>(res.Items.Count);
                foreach (var info in res.Items)
                    items.Add(new InventoryItemData(info.ItemId, info.Quantity));

                return (InventoryResult.Success, items);
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryService] GetInventory 예외: {e.Message}");
                return (InventoryResult.Failed, Array.Empty<InventoryItemData>());
            }
        }

        public async UniTask<(InventoryResult Result, int Remaining)> ConsumeItemAsync(int itemId, int qty, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.ConsumeItemAsync(new ConsumeItemRequest { ItemId = itemId, Qty = qty }, ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[InventoryService] ConsumeItem 실패: {itemId} code={res.Result.ErrorCode}");
                    return (InventoryResult.Failed, 0);
                }

                return (InventoryResult.Success, res.RemainingQuantity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryService] ConsumeItem 예외: {e.Message}");
                return (InventoryResult.Failed, 0);
            }
        }
    }
}

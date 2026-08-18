using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Https.Interfaces;
using GameServer.Grpc.Shop;
using UnityEngine;
using ProtoShopCategory = GameServer.Grpc.Shop.ShopCategory;

namespace Game.System.Shop
{
    /// <summary>
    /// 상점 서비스. gRPC(ShopService.GetShop/Buy) 호출 → 도메인 DTO 변환. InventoryService 와 동형.
    /// </summary>
    public sealed class ShopService : IShopService
    {
        private readonly IShopGrpcService _grpc;

        public ShopService(IShopGrpcService grpc)
        {
            _grpc = grpc;
        }

        public async UniTask<(ShopResult Result, IReadOnlyList<ShopItemData> Items)> GetShopAsync(CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.GetShopAsync(new GetShopRequest(), ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[ShopService] GetShop 실패: code={res.Result.ErrorCode}");
                    return (ShopResult.Failed, Array.Empty<ShopItemData>());
                }

                var items = new List<ShopItemData>(res.Items.Count);
                foreach (var info in res.Items)
                {
                    var stats = new List<ShopStatData>(info.Stats.Count);
                    foreach (var s in info.Stats)
                        stats.Add(new ShopStatData(s.Stat, s.Amount));

                    items.Add(new ShopItemData(info.ItemId, info.BuyPrice, info.SellPrice, ToCategory(info.Category), stats));
                }

                return (ShopResult.Success, items);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopService] GetShop 예외: {e.Message}");
                return (ShopResult.Failed, Array.Empty<ShopItemData>());
            }
        }

        public async UniTask<(ShopResult Result, long Gold, int NewQuantity)> BuyAsync(int itemId, int qty, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.BuyAsync(new BuyRequest { ItemId = itemId, Qty = qty }, ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[ShopService] Buy 실패: {itemId} x{qty} code={res.Result.ErrorCode}");
                    return (ShopResult.Failed, 0, 0);
                }

                return (ShopResult.Success, res.Gold, res.NewQuantity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopService] Buy 예외: {e.Message}");
                return (ShopResult.Failed, 0, 0);
            }
        }

        public async UniTask<(ShopResult Result, long Gold, int RemainingQuantity)> SellAsync(int itemId, int qty, CancellationToken ct = default)
        {
            try
            {
                var res = await _grpc.SellAsync(new SellRequest { ItemId = itemId, Qty = qty }, ct);
                if (!res.Result.Success)
                {
                    Debug.LogWarning($"[ShopService] Sell 실패: {itemId} x{qty} code={res.Result.ErrorCode}");
                    return (ShopResult.Failed, 0, 0);
                }

                return (ShopResult.Success, res.Gold, res.RemainingQuantity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopService] Sell 예외: {e.Message}");
                return (ShopResult.Failed, 0, 0);
            }
        }

        private static ShopCategory ToCategory(ProtoShopCategory c) => c switch
        {
            ProtoShopCategory.Weapon => ShopCategory.Weapon,
            ProtoShopCategory.Armor => ShopCategory.Armor,
            ProtoShopCategory.Accessory => ShopCategory.Accessory,
            ProtoShopCategory.Potion => ShopCategory.Potion,
            _ => ShopCategory.Unspecified,
        };
    }
}

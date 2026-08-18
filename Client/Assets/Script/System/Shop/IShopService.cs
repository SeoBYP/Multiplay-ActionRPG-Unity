using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Shop
{
    /// <summary>
    /// 상점 서비스. 진열 조회 + 구매/판매(서버 권위). 가격·검증은 서버만. proto(GameServer.Grpc.Shop) 은닉.
    /// </summary>
    public interface IShopService
    {
        UniTask<(ShopResult Result, IReadOnlyList<ShopItemData> Items)> GetShopAsync(CancellationToken ct = default);

        /// <summary>구매. 성공 시 (구매후 골드 잔액, 구매후 보유 수량). 잔액부족/실패면 Failed.</summary>
        UniTask<(ShopResult Result, long Gold, int NewQuantity)> BuyAsync(int itemId, int qty, CancellationToken ct = default);

        /// <summary>판매. 성공 시 (판매후 골드 잔액, 남은 수량). 미보유/부족/실패면 Failed.</summary>
        UniTask<(ShopResult Result, long Gold, int RemainingQuantity)> SellAsync(int itemId, int qty, CancellationToken ct = default);
    }
}

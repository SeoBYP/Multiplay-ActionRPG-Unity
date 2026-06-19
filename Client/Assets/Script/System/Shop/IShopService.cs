using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.Shop
{
    /// <summary>
    /// 상점 서비스. 진열 조회 + 구매(서버 권위). 가격·검증은 서버만. proto(GameServer.Grpc.Shop) 은닉.
    /// 판매(Sell)는 판매 UI 도입 시 추가 — 이번 슬라이스는 구매 UI만.
    /// </summary>
    public interface IShopService
    {
        UniTask<(ShopResult Result, IReadOnlyList<ShopItemData> Items)> GetShopAsync(CancellationToken ct = default);

        /// <summary>구매. 성공 시 (구매후 골드 잔액, 구매후 보유 수량). 잔액부족/실패면 Failed.</summary>
        UniTask<(ShopResult Result, long Gold, int NewQuantity)> BuyAsync(string itemId, int qty, CancellationToken ct = default);
    }
}

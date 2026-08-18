using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Shop.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Shop;

/// <summary>
/// 상점 서비스 구현. 가격(ShopCatalog) 검증 후 지갑·인벤토리를 조합한다(서버 권위, 자체 영속 없음).
///
/// 구매(원자성): TrySpend(차감) → Grant(지급). 차감 먼저 = "골드 안 내고 아이템 받기" 복제 차단.
///   지급 실패(설정 오류 등)면 차감 골드를 환불(보상 트랜잭션 — 서버 단일 프로세스라 충분).
/// 판매(원자성): Consume(차감) → Add(적립). 차감 먼저 = "아이템 두고 골드 받기" 복제 차단.
/// </summary>
public sealed class ShopService(IWalletService wallet, IInventoryService inventory) : IShopService
{
    public IReadOnlyCollection<ShopItemDef> GetItems() => ShopCatalog.All;

    public async Task<ShopBuyResult> BuyAsync(long userId, string itemId, int qty, CancellationToken ct = default)
    {
        if (qty <= 0)
            return ShopBuyResult.Fail("qty must be positive");

        var def = ShopCatalog.Get(itemId);
        if (def is null)
            return ShopBuyResult.Fail("item not for sale");

        long totalPrice = def.BuyPrice * qty;

        // ① 차감 먼저(부족 시 거부, 변화 없음).
        var spend = await wallet.TrySpendAsync(userId, totalPrice, ct);
        if (!spend.Success)
            return ShopBuyResult.Fail("insufficient gold");

        // ② 지급. 실패하면 차감분 환불.
        var grant = await inventory.GrantItemAsync(userId, itemId, qty, ct);
        if (!grant.Success)
        {
            await wallet.AddAsync(userId, totalPrice, ct); // 보상: 환불
            return ShopBuyResult.Fail($"grant failed: {grant.FailReason}");
        }

        return ShopBuyResult.Ok(spend.Balance, grant.NewQuantity);
    }

    public async Task<ShopSellResult> SellAsync(long userId, string itemId, int qty, CancellationToken ct = default)
    {
        if (qty <= 0)
            return ShopSellResult.Fail("qty must be positive");

        var def = ShopCatalog.Get(itemId);
        if (def is null)
            return ShopSellResult.Fail("item not sellable");

        // ① 차감 먼저(미보유/부족 시 거부, 변화 없음).
        var consume = await inventory.ConsumeItemAsync(userId, itemId, qty, ct);
        if (!consume.Success)
            return ShopSellResult.Fail("not owned or insufficient quantity");

        // ② 적립.
        long balance = await wallet.AddAsync(userId, def.SellPrice * qty, ct);
        return ShopSellResult.Ok(balance, consume.RemainingQuantity);
    }
}

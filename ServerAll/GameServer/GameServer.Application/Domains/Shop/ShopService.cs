using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Shop.Interfaces;
using GameServer.Application.Domains.Wallet.Interfaces;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Shop;

/// <summary>
/// 상점 서비스 구현. 가격(ShopCatalog) 검증 후 지갑·인벤토리를 조합한다(자체 영속 없음).
/// 항상 차감이 먼저다 — 구매는 TrySpend→Grant("골드 안 내고 아이템 받기" 차단, 지급 실패 시 환불),
/// 판매는 Consume→Add("아이템 두고 골드 받기" 차단).
/// </summary>
public sealed class ShopService(IWalletService wallet, IInventoryService inventory) : IShopService
{
    public IReadOnlyCollection<ShopItemDef> GetItems() => ShopCatalog.All;

    public async Task<ShopBuyResult> BuyAsync(long userId, int itemId, int qty, CancellationToken ct = default)
    {
        if (qty <= 0)
            return ShopBuyResult.Fail("qty must be positive");

        var def = ShopCatalog.Get(itemId);
        if (def is null)
            return ShopBuyResult.Fail("item not for sale");

        long totalPrice = def.BuyPrice * qty;

        var spend = await wallet.TrySpendAsync(userId, totalPrice, ct);
        if (!spend.Success)
            return ShopBuyResult.Fail("insufficient gold");

        var grant = await inventory.GrantItemAsync(userId, itemId, qty, ct);
        if (!grant.Success)
        {
            await wallet.AddAsync(userId, totalPrice, ct); // 환불
            return ShopBuyResult.Fail($"grant failed: {grant.FailReason}");
        }

        return ShopBuyResult.Ok(spend.Balance, grant.NewQuantity);
    }

    public async Task<ShopSellResult> SellAsync(long userId, int itemId, int qty, CancellationToken ct = default)
    {
        if (qty <= 0)
            return ShopSellResult.Fail("qty must be positive");

        var def = ShopCatalog.Get(itemId);
        if (def is null)
            return ShopSellResult.Fail("item not sellable");

        var consume = await inventory.ConsumeItemAsync(userId, itemId, qty, ct);
        if (!consume.Success)
            return ShopSellResult.Fail("not owned or insufficient quantity");

        long balance = await wallet.AddAsync(userId, def.SellPrice * qty, ct);
        return ShopSellResult.Ok(balance, consume.RemainingQuantity);
    }
}

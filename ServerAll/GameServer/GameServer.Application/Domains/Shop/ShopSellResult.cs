namespace GameServer.Application.Domains.Shop;

/// <summary>판매 결과. 성공 시 적립 후 골드 잔액 + 차감 후 남은 수량. 미보유/안 파는 아이템이면 Success=false.</summary>
public sealed record ShopSellResult(long Gold, int RemainingQuantity, bool Success, string? FailReason = null)
{
    public static ShopSellResult Ok(long gold, int remainingQuantity) => new(gold, remainingQuantity, true);

    public static ShopSellResult Fail(string reason) => new(0, 0, false, reason);
}

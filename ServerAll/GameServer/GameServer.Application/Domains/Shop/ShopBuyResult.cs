namespace GameServer.Application.Domains.Shop;

/// <summary>구매 결과. 성공 시 차감 후 골드 잔액 + 지급 후 보유 수량. 잔액 부족/안 파는 아이템이면 Success=false.</summary>
public sealed record ShopBuyResult(long Gold, int NewQuantity, bool Success, string? FailReason = null)
{
    public static ShopBuyResult Ok(long gold, int newQuantity) => new(gold, newQuantity, true);

    public static ShopBuyResult Fail(string reason) => new(0, 0, false, reason);
}

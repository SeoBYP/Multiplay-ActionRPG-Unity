namespace GameServer.Application.Domains.Wallet;

/// <summary>
/// 골드 차감 결과. 상점 구매(3.5) 등이 차감 후 받는 응답. 잔액 부족 시 Success=false(미차감).
/// 멱등은 호출자 책임.
/// </summary>
public sealed record WalletSpendResult(long Balance, bool Success, string? FailReason = null)
{
    public static WalletSpendResult Ok(long balance) => new(balance, true);

    public static WalletSpendResult Fail(long balance, string reason) => new(balance, false, reason);
}

namespace GameServer.Application.Domains.Wallet.Interfaces;

/// <summary>
/// 재화(골드) 도메인 서비스. 잔액 조회 + 증감(서버 권위). 골드는 통화 — 인벤토리와 분리(3.4).
/// 멱등(중복지급 차단)은 호출자 책임(루트 PickupId·결과 ResultId 등) — 여기선 단건 증감만.
/// </summary>
public interface IWalletService
{
    /// <summary>유저의 현재 골드 잔액. 지갑이 없으면 0.</summary>
    Task<long> GetBalanceAsync(long userId, CancellationToken ct = default);

    /// <summary>골드 적립(보상/루트/상점판매). amount≤0 이면 무변동. 적립 후 잔액 반환.</summary>
    Task<long> AddAsync(long userId, long amount, CancellationToken ct = default);

    /// <summary>골드 차감(상점구매 등). 잔액 부족·amount≤0 이면 실패(미차감). 성공 시 남은 잔액 반환.</summary>
    Task<WalletSpendResult> TrySpendAsync(long userId, long amount, CancellationToken ct = default);
}

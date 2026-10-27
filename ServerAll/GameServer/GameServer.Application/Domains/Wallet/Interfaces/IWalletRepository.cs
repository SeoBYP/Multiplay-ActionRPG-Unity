namespace GameServer.Application.Domains.Wallet.Interfaces;

/// <summary>
/// 재화(골드) 잔액 영속 저장소. Cache-Aside + Delete 패턴(Add/Spend 는 DB 갱신 후 캐시 DEL).
/// </summary>
public interface IWalletRepository
{
    /// <summary>골드 잔액(캐시 우선). 지갑이 없으면 0.</summary>
    Task<long> GetBalanceAsync(long userId, CancellationToken ct = default);

    /// <summary>잔액 누적 적립. 행이 없으면 생성. 적립 후 잔액 반환.</summary>
    Task<long> AddBalanceAsync(long userId, long amount, CancellationToken ct = default);

    /// <summary>잔액 차감. 부족(또는 지갑 없음)이면 null(변화 없음), 성공 시 남은 잔액(0 가능).</summary>
    Task<long?> TrySpendBalanceAsync(long userId, long amount, CancellationToken ct = default);
}

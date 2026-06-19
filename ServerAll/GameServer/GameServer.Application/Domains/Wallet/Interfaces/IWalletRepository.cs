namespace GameServer.Application.Domains.Wallet.Interfaces;

/// <summary>
/// 재화(골드) 잔액 영속 저장소. Cache-Aside + Delete 패턴(networking.md).
///   GetBalance : Redis String HIT 즉시 반환 / MISS → DB(AsNoTracking) → 캐시 SET(TTL)
///   Add/Spend  : DB 갱신(없으면 lazy create) → SaveChanges → 캐시 DEL
/// </summary>
public interface IWalletRepository
{
    /// <summary>유저 골드 잔액(캐시 우선). 지갑이 없으면 0.</summary>
    Task<long> GetBalanceAsync(long userId, CancellationToken ct = default);

    /// <summary>잔액을 누적 적립한다. 행이 없으면 생성(lazy). DB 저장 후 캐시 DEL. 적립 후 잔액 반환.</summary>
    Task<long> AddBalanceAsync(long userId, long amount, CancellationToken ct = default);

    /// <summary>
    /// 잔액을 차감한다. 잔액 부족(또는 지갑 없음)이면 null(변화 없음).
    /// DB 저장 후 캐시 DEL. 성공 시 남은 잔액(0 가능)을 반환.
    /// </summary>
    Task<long?> TrySpendBalanceAsync(long userId, long amount, CancellationToken ct = default);
}

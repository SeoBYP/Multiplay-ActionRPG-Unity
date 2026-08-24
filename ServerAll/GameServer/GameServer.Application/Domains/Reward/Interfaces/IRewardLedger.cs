namespace GameServer.Application.Domains.Reward.Interfaces;

/// <param name="GrantKey">멱등 범위 전체를 담은 키(예: "dungeon:{roomId}:{userId}", "pickup:{pickupId}").</param>
/// <param name="Kind">"exp" | "item" | "currency" — 조회·정산용 분류.</param>
/// <param name="RefId">아이템 지급이면 itemId. 없으면 "".</param>
public readonly record struct RewardGrantRequest(
    string GrantKey,
    long UserId,
    string Kind,
    string RefId,
    long Amount);

/// <summary>
/// 보상을 **정확히 한 번만** 지급한다(exactly-once).
///
/// 지급 동작과 "지급했음" 기록을 같은 트랜잭션에 묶는 것이 핵심이다. 그래야
///   - 재시도가 안전하고(이미 준 건 UNIQUE 로 걸린다)
///   - 부분 실패도 나머지만 마저 줄 수 있다(참가자별로 키가 갈리므로)
/// 두 가지가 동시에 성립한다. Redis 키만으로는 지급과 기록이 다른 저장소라 둘 중 하나를 포기해야 했다.
/// </summary>
public interface IRewardLedger
{
    /// <summary>
    /// <paramref name="request"/> 의 GrantKey 가 처음이면 <paramref name="grant"/> 를 같은 트랜잭션에서 실행하고 원장에 기록한다.
    /// 이미 지급돼 있으면 아무것도 하지 않는다.
    /// </summary>
    /// <param name="grant">실제 지급 동작. 같은 DbContext 를 쓰는 서비스여야 트랜잭션이 함께 묶인다.</param>
    /// <returns>이번 호출에서 실제로 지급했으면 true, 이미 지급돼 있었으면 false.</returns>
    Task<bool> GrantOnceAsync(
        RewardGrantRequest request,
        Func<CancellationToken, Task> grant,
        CancellationToken ct = default);
}

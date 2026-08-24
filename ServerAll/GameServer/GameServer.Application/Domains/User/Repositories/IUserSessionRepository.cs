using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.User.Interfaces;

/// <summary>
/// 사용자 세션 데이터를 관리하기 위한 저장소 인터페이스
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// 새로운 세션을 생성합니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>생성된 세션 정보 (실패 시 null)</returns>
    Task<UserSession?> CreateSessionAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 세션 ID로 세션 정보를 조회합니다.
    /// </summary>
    /// <param name="sessionId">세션 ID</param>
    /// <returns>찾은 세션 정보 (없으면 null)</returns>
    Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 사용자 ID로 세션 정보를 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>찾은 세션 정보 (없으면 null)</returns>
    Task<UserSession?> GetSessionByUserIdAsync(long userId, CancellationToken ct = default);
    
    /// <summary>
    /// 세션을 제거합니다.
    /// </summary>
    /// <param name="sessionId">제거할 세션 ID</param>
    Task RemoveSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 활성 상태인 세션의 총 개수를 조회합니다.
    /// </summary>
    /// <returns>활성 세션 수</returns>
    Task<long> GetActiveSessionCountAsync(CancellationToken ct = default);

    /// <summary>
    /// 활성 상태인 모든 세션 목록을 조회합니다.
    /// </summary>
    /// <returns>활성 세션 목록</returns>
    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// 이 세션이 방금 활동했음을 기록합니다(생존 신호 갱신).
    /// </summary>
    /// <remarks>
    /// 인증된 RPC 마다 호출되므로 쓰기 폭주를 막기 위해 구현이 스로틀한다 —
    /// 남은 수명이 절반 이상 남았으면 아무것도 쓰지 않는다.
    /// </remarks>
    Task TouchSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// 세션 활성 집합에 기록된 만료 시각(UTC)을 조회합니다.
    /// </summary>
    /// <remarks>
    /// 이 값은 세션 캐시가 만료된 뒤 다음 인증 RPC 에서 다시 찍힌다 —
    /// 그래서 "최근 인증 활동 시각 + AccessToken 수명"의 근사값이다.
    /// 시스템에 정식 하트비트가 없으므로 현재로선 이것이 유일한 생존 근사 신호다.
    /// 세션 자체가 없거나 활성 집합에 없으면 null.
    /// </remarks>
    Task<DateTime?> GetSessionActiveUntilAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 일정 시간이 경과하여 만료된 세션들을 정리합니다.
    /// </summary>
    /// <param name="timeout">만료 기준 시간</param>
    Task CleanupExpiredSessionsAsync(TimeSpan timeout);
}

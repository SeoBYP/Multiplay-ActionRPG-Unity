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
    /// <param name="userEmail">사용자 이메일</param>
    /// <param name="publicId">사용자 공개 ID</param>
    /// <returns>생성된 세션 정보 (실패 시 null)</returns>
    Task<UserSession?> CreateSessionAsync(long userId, string nickName, string userEmail, string publicId, CancellationToken ct = default);

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
    /// 세션의 현재 방 ID를 갱신합니다.
    /// </summary>
    Task UpdateRoomIdAsync(string sessionId, long roomId, CancellationToken ct = default);
    
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
    /// 일정 시간이 경과하여 만료된 세션들을 정리합니다.
    /// </summary>
    /// <param name="timeout">만료 기준 시간</param>
    Task CleanupExpiredSessionsAsync(TimeSpan timeout, CancellationToken ct = default);
}

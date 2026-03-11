namespace GameServer.Application.Domains.User.Interfaces;

using User = Domain.Entities.User.User;

/// <summary>
/// 사용자 데이터를 관리하기 위한 저장소 인터페이스
/// </summary>
public interface IUserRepository
{

    /// <summary>
    /// 새로운 사용자를 추가합니다.
    /// </summary>
    /// <param name="passwordHash">비밀번호 해시</param>
    /// <param name="email">이메일</param>
    /// <returns>추가된 사용자 엔티티</returns>
    Task<User> AddAsync(string passwordHash, string email, CancellationToken ct = default);

    /// <summary>
    /// 사용자를 삭제합니다.
    /// </summary>
    /// <param name="userId">삭제할 사용자 ID</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 사용자 정보를 업데이트합니다.
    /// </summary>
    /// <param name="user">업데이트할 정보를 가진 사용자 엔티티</param>
    /// <returns>업데이트 성공 여부</returns>
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// ID로 사용자를 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByIdAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 지정된 사용자 ID 목록에 해당하는 사용자 엔티티를 반환합니다.
    /// </summary>
    /// <param name="userIds">조회할 사용자 ID 목록</param>
    /// <param name="ct">작업 취소를 위한 토큰</param>
    /// <returns>사용자 엔티티 목록</returns>
    Task<List<User>> GetByIdsAsync(List<long> userIds, CancellationToken ct = default);

    /// <summary>
    /// 이메일로 사용자를 조회합니다.
    /// </summary>
    /// <param name="email">사용자 이메일</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 공개 ID로 사용자를 조회합니다.
    /// </summary>
    /// <param name="publicId">사용자 공개 ID</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);

    /// <summary>
    /// 닉네임으로 사용자를 조회합니다.
    /// </summary>
    /// <param name="nickname">사용자 닉네임</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByNicknameAsync(string nickname, CancellationToken ct = default);
    
    /// <summary>
    /// 이메일이 이미 존재하는지 확인합니다.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default);
    
    /// <summary>
    /// 닉네임이 이미 존재하는지 확인합니다.
    /// </summary>
    /// <param name="nickname"></param>
    /// <returns></returns>
    Task<bool> IsNicknameExistsAsync(string nickname, CancellationToken ct = default);

    /// <summary>
    /// 사용자 리프레시 토큰을 업데이트합니다.
    /// </summary>
    /// <param name="userId">업데이트할 사용자 ID</param>
    /// <param name="hashedToken">새로운 리프레시 토큰(해시된 값)</param>
    /// <param name="expiry">토큰 만료 시간</param>
    /// <param name="ct">작업 취소를 위한 토큰</param>
    /// <returns>업데이트 성공 여부</returns>
    Task<bool> UpdateRefreshTokenAsync(long userId, string hashedToken, DateTime expiry,
        CancellationToken ct = default);

    /// <summary>
    /// 사용자의 리프레시 토큰을 제거합니다.
    /// </summary>
    /// <param name="userId">리프레시 토큰을 삭제할 사용자 ID</param>
    /// <param name="ct">작업 취소를 위한 토큰</param>
    /// <returns>리프레시 토큰 삭제 성공 여부</returns>
    Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default);
}
namespace GameServer.Infrastructure.Interfaces.User;

using User = Domain.Entities.User.User;

/// <summary>
/// 사용자 데이터를 관리하기 위한 저장소 인터페이스
/// </summary>
public interface IUserRepository
{

    /// <summary>
    /// 새로운 사용자를 추가합니다.
    /// </summary>
    /// <param name="user">추가할 사용자 엔티티</param>
    /// <returns>추가된 사용자 엔티티</returns>
    Task<User> AddAsync(string nickname, string passwordHash, string email);

    /// <summary>
    /// 사용자를 삭제합니다.
    /// </summary>
    /// <param name="userId">삭제할 사용자 ID</param>
    /// <returns>삭제 성공 여부</returns>
    Task<bool> RemoveAsync(long userId);

    /// <summary>
    /// 사용자 정보를 업데이트합니다.
    /// </summary>
    /// <param name="user">업데이트할 정보를 가진 사용자 엔티티</param>
    /// <returns>업데이트 성공 여부</returns>
    Task<bool> UpdateAsync(User user);

    /// <summary>
    /// ID로 사용자를 조회합니다.
    /// </summary>
    /// <param name="userId">사용자 ID</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByIdAsync(long userId);

    /// <summary>
    /// 이메일로 사용자를 조회합니다.
    /// </summary>
    /// <param name="email">사용자 이메일</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// 공개 ID로 사용자를 조회합니다.
    /// </summary>
    /// <param name="publicId">사용자 공개 ID</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByPublicIdAsync(string publicId);

    /// <summary>
    /// 닉네임으로 사용자를 조회합니다.
    /// </summary>
    /// <param name="nickname">사용자 닉네임</param>
    /// <returns>찾은 사용자 엔티티 (없으면 null)</returns>
    Task<User?> GetByNicknameAsync(string nickname);
    
    /// <summary>
    /// 이메일이 이미 존재하는지 확인합니다.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    Task<bool> IsEmailExistsAsync(string email);
    
    /// <summary>
    /// 닉네임이 이미 존재하는지 확인합니다.
    /// </summary>
    /// <param name="nickname"></param>
    /// <returns></returns>
    Task<bool> IsNicknameExistsAsync(string nickname);
}
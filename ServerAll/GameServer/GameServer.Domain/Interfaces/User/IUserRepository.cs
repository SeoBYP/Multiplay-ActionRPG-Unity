namespace GameServer.Domain.Interfaces.User;

public interface IUserRepository
{
    // UserName 조회
    Task<Entities.User.User?> GetByUsernameAsync(string userName);
    
    // User 추가(회원가입)
    Task AddAsync(Entities.User.User user);
    
    // UserId로 조회
    Task<Entities.User.User?> GetByIdAsync(long userId);
}
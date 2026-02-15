namespace GameServer.Infrastructure.Interfaces.User;

public interface IUserRepository
{
    // UserName 조회
    Task<Domain.Entities.User.User?> GetByUsernameAsync(string userName);
    
    // User 추가(회원가입)
    Task AddAsync(Domain.Entities.User.User user);
    
    // UserId로 조회
    Task<Domain.Entities.User.User?> GetByIdAsync(long userId);
}
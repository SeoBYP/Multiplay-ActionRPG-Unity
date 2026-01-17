using GameServer.Domain.Entities;

namespace GameServer.Domain.Interfaces;

public interface IUserRepository
{
    // UserName 조회
    Task<User?> GetByUsernameAsync(string userName);
    
    // User 추가(회원가입)
    Task AddAsync(User user);
    
    // UserId로 조회
    Task<User?> GetByIdAsync(long userId);
}
using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile> CreateAsync(long userId, string nickName, CancellationToken ct = default);
    
    Task<UserProfile?> GetByIdAsync(long userId, CancellationToken ct = default);

    Task<UserProfile?> GetByNicknameAsync(string nickName, CancellationToken ct = default);
    
    Task<bool> UpdateAsync(UserProfile profile, CancellationToken ct = default);
    
    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);
}

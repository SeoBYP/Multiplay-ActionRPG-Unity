using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

public interface IUserCredentialRepository
{
    Task<UserCredential> CreateAsync(long userId, string email, string passwordHash, CancellationToken ct = default);
    
    Task<UserCredential?> FindByIdAsync(long userId, CancellationToken ct = default);
    Task<UserCredential?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<bool> UpdateAsync(UserCredential userCredential, CancellationToken ct = default);
    Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct = default);

    Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default);
    
    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);
    
    Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default);
}

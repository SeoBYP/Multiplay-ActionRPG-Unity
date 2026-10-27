namespace GameServer.Application.Domains.User.Interfaces;

using User = Domain.Entities.User.User;

public interface IUserRepository
{
    Task<User> CreateAsync(CancellationToken ct = default);

    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);

    Task<bool> UpdateAsync(User user, CancellationToken ct = default);

    Task<User?> GetByIdAsync(long userId, CancellationToken ct = default);

    Task<List<User>> GetByIdsAsync(List<long> userIds, CancellationToken ct = default);
}
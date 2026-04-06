using System.Collections.Concurrent;
using GameServer.Application.Domains.User.Interfaces;
using User = GameServer.Domain.Entities.User.User;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<long, User> _users = new();
    private long _idCounter;

    public Task<User> CreateAsync(CancellationToken ct = default)
    {
        var user = User.Create();
        user.SetUserId(Interlocked.Increment(ref _idCounter));
        _users[user.UserId] = user;
        return Task.FromResult(user);
    }

    public Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        return Task.FromResult(_users.TryRemove(userId, out _));
    }

    public Task<bool> UpdateAsync(User user, CancellationToken ct = default)
    {
        if (!_users.ContainsKey(user.UserId))
            return Task.FromResult(false);

        _users[user.UserId] = user;
        return Task.FromResult(true);
    }

    public Task<User?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<List<User>> GetByIdsAsync(List<long> userIds, CancellationToken ct = default)
    {
        return Task.FromResult(_users.Values.Where(u => userIds.Contains(u.UserId)).ToList());
    }

    public Task<User?> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => u.PublicId == publicId);
        return Task.FromResult(user);
    }

    public Task<bool> UpdateRefreshTokenAsync(long userId, string hashedToken, DateTime expiry, CancellationToken ct = default)
    {
        return Task.FromResult(_users.ContainsKey(userId));
    }

    public Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default)
    {
        return Task.FromResult(_users.ContainsKey(userId));
    }
}

using System.Collections.Concurrent;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeUserProfileRepository : IUserProfileRepository
{
    private readonly ConcurrentDictionary<long, UserProfile> _profiles = new();

    public Task<UserProfile> CreateAsync(long userId, string nickName, CancellationToken ct = default)
    {
        var profile = UserProfile.Create(userId, nickName);
        _profiles[userId] = profile;
        return Task.FromResult(profile);
    }

    public Task<UserProfile?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        _profiles.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<bool> UpdateAsync(UserProfile profile, CancellationToken ct = default)
    {
        _profiles[profile.UserId] = profile;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        return Task.FromResult(_profiles.TryRemove(userId, out _));
    }
}

using System.Collections.Concurrent;
using GameServer.Infrastructure.Interfaces.User;
using User = GameServer.Domain.Entities.User.User;

namespace GameServer.Tests.Infrastructure;

public class FakeUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<long, User> _users = new();
    private long _idCounter = 0;

    public Task<User> AddAsync(string passwordHash, string email)
    {
        var user = User.Create(passwordHash, email);
        var userId = Interlocked.Increment(ref _idCounter);
        user.SetUserId(userId);
        
        _users[userId] = user;
        return Task.FromResult(user);
    }

    public Task<bool> RemoveAsync(long userId)
    {
        return Task.FromResult(_users.TryRemove(userId, out _));
    }

    public Task<bool> UpdateAsync(User user)
    {
        if (!_users.ContainsKey(user.UserId))
        {
            return Task.FromResult(false);
        }

        _users[user.UserId] = user;
        return Task.FromResult(true);
    }

    public Task<User?> GetByIdAsync(long userId)
    {
        _users.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        var user = _users.Values.FirstOrDefault(u => u.Email == email);
        return Task.FromResult(user);
    }

    public Task<User?> GetByPublicIdAsync(string publicId)
    {
        var user = _users.Values.FirstOrDefault(u => u.PublicId == publicId);
        return Task.FromResult(user);
    }

    public Task<User?> GetByNicknameAsync(string nickname)
    {
        var user = _users.Values.FirstOrDefault(u => u.NickName == nickname);
        return Task.FromResult(user);
    }

    public Task<bool> IsEmailExistsAsync(string email)
    {
        return Task.FromResult(_users.Values.Any(u => u.Email == email));
    }

    public Task<bool> IsNicknameExistsAsync(string nickname)
    {
        return Task.FromResult(_users.Values.Any(u => u.NickName == nickname));
    }
}

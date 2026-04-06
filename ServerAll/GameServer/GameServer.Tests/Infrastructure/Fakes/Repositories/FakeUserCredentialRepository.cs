using System.Collections.Concurrent;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeUserCredentialRepository : IUserCredentialRepository
{
    private readonly ConcurrentDictionary<long, UserCredential> _credentials = new();
    private readonly ConcurrentDictionary<string, long> _emailToUserId = new(StringComparer.OrdinalIgnoreCase);

    public Task<UserCredential> CreateAsync(long userId, string email, string passwordHash, CancellationToken ct = default)
    {
        var credential = UserCredential.Create(userId, email, passwordHash);
        _credentials[userId] = credential;
        _emailToUserId[email] = userId;
        return Task.FromResult(credential);
    }

    public Task<UserCredential?> FindByIdAsync(long userId, CancellationToken ct = default)
    {
        _credentials.TryGetValue(userId, out var credential);
        return Task.FromResult(credential);
    }

    public Task<UserCredential?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        if (_emailToUserId.TryGetValue(email, out var userId) && _credentials.TryGetValue(userId, out var credential))
            return Task.FromResult<UserCredential?>(credential);

        return Task.FromResult<UserCredential?>(null);
    }

    public Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct = default)
    {
        if (!_credentials.TryGetValue(userId, out var credential))
            return Task.FromResult(false);

        var updated = UserCredential.Restore(
            credential.UserId,
            credential.Email,
            passwordHash,
            credential.RefreshToken,
            credential.RefreshTokenExpiresAt);

        _credentials[userId] = updated;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateRefreshTokenAsync(long userId, string hashedToken, DateTime expiry, CancellationToken ct = default)
    {
        if (!_credentials.TryGetValue(userId, out var credential))
            return Task.FromResult(false);

        credential.SetRefreshToken(hashedToken, expiry);
        return Task.FromResult(true);
    }

    public Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default)
    {
        if (!_credentials.TryGetValue(userId, out var credential))
            return Task.FromResult(false);

        credential.ClearRefreshToken();
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAsync(UserCredential userCredential, CancellationToken ct = default)
    {
        if (!_credentials.ContainsKey(userCredential.UserId))
            return Task.FromResult(false);

        _credentials[userCredential.UserId] = userCredential;
        _emailToUserId[userCredential.Email] = userCredential.UserId;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        if (_credentials.TryRemove(userId, out var credential))
        {
            _emailToUserId.TryRemove(credential.Email, out _);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult(_emailToUserId.ContainsKey(email));
    }
}

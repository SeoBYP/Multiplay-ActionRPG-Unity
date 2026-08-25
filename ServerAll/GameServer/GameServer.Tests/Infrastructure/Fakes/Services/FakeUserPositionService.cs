using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Infrastructure.Fakes.Services;

/// <summary>
/// Main 위치 지속화(B7) no-op. 인증·로비 테스트의 관심사가 아니라 의존만 채운다.
/// Flush 호출 여부는 <see cref="FlushedUserIds"/> 로 관측할 수 있다.
/// </summary>
public sealed class FakeUserPositionService : IUserPositionService
{
    public readonly List<long> FlushedUserIds = new();

    public Task<SavePositionResult> SaveAsync(
        long userId, string mapId, float x, float y, float z, float rotY, CancellationToken ct = default)
        => Task.FromResult(new SavePositionResult(Accepted: true, Snapped: false));

    public Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default)
        => Task.FromResult<UserPosition?>(null);

    public Task FlushAsync(long userId, CancellationToken ct = default)
    {
        FlushedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}

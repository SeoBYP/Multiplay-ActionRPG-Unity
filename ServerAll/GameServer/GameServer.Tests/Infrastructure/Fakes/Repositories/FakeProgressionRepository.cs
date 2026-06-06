using System.Collections.Concurrent;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeProgressionRepository : IProgressionRepository
{
    private readonly ConcurrentDictionary<long, UserProgression> _store = new();

    public Task<UserProgression?> GetAsync(long userId, CancellationToken ct = default)
    {
        _store.TryGetValue(userId, out var progression);
        return Task.FromResult(progression);
    }

    public Task<UserProgression> AddExpAsync(long userId, long amount, CancellationToken ct = default)
    {
        var progression = _store.GetOrAdd(userId, UserProgression.Create);
        progression.AddExp(amount);
        return Task.FromResult(progression);
    }
}

using GameServer.Application.Domains.Codex.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

/// <summary>실제 CodexRepository 의 멱등 발견 기록/조회를 인메모리로 모사.</summary>
public class FakeCodexRepository : ICodexRepository
{
    private readonly Dictionary<long, HashSet<string>> _discovered = new();

    public Task<List<string>> GetDiscoveredItemIdsAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_discovered.TryGetValue(userId, out var set) ? set.ToList() : new List<string>());

    public Task<bool> AddDiscoveredAsync(long userId, string itemId, CancellationToken ct = default)
    {
        if (!_discovered.TryGetValue(userId, out var set))
            _discovered[userId] = set = new HashSet<string>();

        return Task.FromResult(set.Add(itemId)); // 신규면 true, 이미 있으면 false(멱등)
    }
}

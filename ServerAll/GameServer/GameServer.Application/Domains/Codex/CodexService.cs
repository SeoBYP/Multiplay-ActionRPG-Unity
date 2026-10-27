using GameServer.Application.Domains.Codex.Interfaces;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Codex;

/// <summary>도감 서비스 구현. 카탈로그에 없는 itemId 는 기록하지 않는다. 멱등은 저장소가 보장.</summary>
public sealed class CodexService(ICodexRepository repository) : ICodexService
{
    public Task<List<int>> GetDiscoveredAsync(long userId, CancellationToken ct = default)
        => repository.GetDiscoveredItemIdsAsync(userId, ct);

    public Task<bool> MarkDiscoveredAsync(long userId, int itemId, CancellationToken ct = default)
    {
        // 방어적 재검증 — 정의 없는 itemId 는 도감 대상이 아니다
        if (!ItemCatalog.Contains(itemId))
            return Task.FromResult(false);

        return repository.AddDiscoveredAsync(userId, itemId, ct);
    }
}

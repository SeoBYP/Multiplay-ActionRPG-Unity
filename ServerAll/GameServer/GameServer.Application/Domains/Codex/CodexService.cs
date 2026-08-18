using GameServer.Application.Domains.Codex.Interfaces;
using Shared.Infrastructure.Items;

namespace GameServer.Application.Domains.Codex;

/// <summary>
/// 도감 서비스 구현. 발견 기록을 저장소에 위임한다. 미지의 itemId(카탈로그에 없음)는 발견 기록하지 않는다
/// — 드리프트(없는 itemId)를 도감에 남기지 않기 위함. 조회/지급 멱등은 저장소가 보장.
/// </summary>
public sealed class CodexService(ICodexRepository repository) : ICodexService
{
    public Task<List<string>> GetDiscoveredAsync(long userId, CancellationToken ct = default)
        => repository.GetDiscoveredItemIdsAsync(userId, ct);

    public Task<bool> MarkDiscoveredAsync(long userId, string itemId, CancellationToken ct = default)
    {
        // 정의가 없는 itemId 는 도감 대상 아님(GrantItemAsync 는 카탈로그 검증 후 호출하지만 방어적으로 한 번 더).
        if (!ItemCatalog.Contains(itemId))
            return Task.FromResult(false);

        return repository.AddDiscoveredAsync(userId, itemId, ct);
    }
}

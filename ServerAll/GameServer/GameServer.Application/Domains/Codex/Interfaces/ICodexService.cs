namespace GameServer.Application.Domains.Codex.Interfaces;

/// <summary>
/// 도감(컬렉션) 서비스. "한 번이라도 획득한 아이템"을 서버 권위로 기록·조회한다(3.7).
/// 발견은 아이템 지급 funnel(IInventoryService.GrantItemAsync)에서만 일어난다 — 클라가 임의 발견 보고 불가(치팅 차단).
/// </summary>
public interface ICodexService
{
    /// <summary>유저가 발견한 itemId 집합.</summary>
    Task<List<int>> GetDiscoveredAsync(long userId, CancellationToken ct = default);

    /// <summary>아이템을 발견 처리(획득 시 호출). 멱등 — 이미 발견했으면 무변동. 신규 발견이면 true.</summary>
    Task<bool> MarkDiscoveredAsync(long userId, int itemId, CancellationToken ct = default);
}

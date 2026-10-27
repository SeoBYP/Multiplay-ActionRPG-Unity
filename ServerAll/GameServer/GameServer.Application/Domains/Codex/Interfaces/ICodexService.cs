namespace GameServer.Application.Domains.Codex.Interfaces;

/// <summary>
/// 도감 서비스. 한 번이라도 획득한 아이템을 서버 권위로 기록한다.
/// 발견은 IInventoryService.GrantItemAsync 에서만 일어난다 — 클라가 임의로 보고할 수 없다.
/// </summary>
public interface ICodexService
{
    Task<List<int>> GetDiscoveredAsync(long userId, CancellationToken ct = default);

    /// <summary>아이템을 발견 처리(획득 시 호출). 멱등 — 이미 발견했으면 무변동. 신규 발견이면 true.</summary>
    Task<bool> MarkDiscoveredAsync(long userId, int itemId, CancellationToken ct = default);
}

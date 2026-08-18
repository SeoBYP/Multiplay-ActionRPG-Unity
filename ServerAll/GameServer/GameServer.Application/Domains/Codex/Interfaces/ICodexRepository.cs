namespace GameServer.Application.Domains.Codex.Interfaces;

/// <summary>
/// 도감 발견 기록 저장소. DB 전용(Redis 캐시 없음) — write-once·read-rare 라 캐시 이득 낮음.
/// 읽기는 AsNoTracking(long-lived DbContext stale 방지, networking.md).
/// </summary>
public interface ICodexRepository
{
    /// <summary>유저가 발견한 itemId 집합.</summary>
    Task<List<int>> GetDiscoveredItemIdsAsync(long userId, CancellationToken ct = default);

    /// <summary>발견 기록 추가. 이미 있으면 무시(멱등). 신규 발견이면 true.</summary>
    Task<bool> AddDiscoveredAsync(long userId, int itemId, CancellationToken ct = default);
}

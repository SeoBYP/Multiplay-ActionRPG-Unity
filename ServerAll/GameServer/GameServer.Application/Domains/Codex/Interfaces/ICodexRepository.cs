namespace GameServer.Application.Domains.Codex.Interfaces;

/// <summary>도감 발견 기록 저장소. DB 전용(write-once·read-rare 라 Redis 캐시 없음). 읽기는 AsNoTracking.</summary>
public interface ICodexRepository
{
    Task<List<int>> GetDiscoveredItemIdsAsync(long userId, CancellationToken ct = default);

    /// <summary>발견 기록 추가. 이미 있으면 무시(멱등). 신규 발견이면 true.</summary>
    Task<bool> AddDiscoveredAsync(long userId, int itemId, CancellationToken ct = default);
}

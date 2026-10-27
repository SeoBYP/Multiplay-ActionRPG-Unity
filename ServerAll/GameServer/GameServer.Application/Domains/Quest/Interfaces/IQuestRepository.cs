using GameServer.Domain.Entities.Quest;

namespace GameServer.Application.Domains.Quest.Interfaces;

/// <summary>
/// 퀘스트 수주/진행 저장소. DB 전용 — write-heavy(킬마다 진행)라 Redis 캐시 부적합. 읽기는 AsNoTracking.
/// </summary>
public interface IQuestRepository
{
    Task<List<UserQuest>> GetAllForUserAsync(long userId, CancellationToken ct = default);

    /// <summary>특정 퀘스트 행. 없으면 null(미수주).</summary>
    Task<UserQuest?> GetAsync(long userId, string questId, CancellationToken ct = default);

    /// <summary>행 upsert.</summary>
    Task UpsertAsync(UserQuest quest, CancellationToken ct = default);
}

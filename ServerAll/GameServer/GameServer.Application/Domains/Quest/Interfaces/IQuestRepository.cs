using GameServer.Domain.Entities.Quest;

namespace GameServer.Application.Domains.Quest.Interfaces;

/// <summary>
/// 퀘스트 수주/진행 저장소. DB 전용(Redis 캐시 없음) — read-rare/write-heavy(킬마다 진행)라 캐시 부적합(plan §4.4).
/// 읽기는 AsNoTracking(long-lived DbContext stale 방지, networking.md).
/// </summary>
public interface IQuestRepository
{
    /// <summary>유저의 모든 수주/진행 행(미수주는 행 없음).</summary>
    Task<List<UserQuest>> GetAllForUserAsync(long userId, CancellationToken ct = default);

    /// <summary>특정 퀘스트 행. 없으면 null(미수주).</summary>
    Task<UserQuest?> GetAsync(long userId, string questId, CancellationToken ct = default);

    /// <summary>행을 upsert(없으면 insert, 있으면 update). 진행/상태 변경 영속.</summary>
    Task UpsertAsync(UserQuest quest, CancellationToken ct = default);
}

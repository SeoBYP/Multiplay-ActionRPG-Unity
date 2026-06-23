using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Domain.Entities.Quest;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Infrastructure.Domains.Quest;

/// <summary>
/// 퀘스트 수주/진행 저장소. DB 전용(Redis 캐시 없음) — read-rare/write-heavy(킬마다 진행)라 캐시 부적합(plan §4.4).
///   Get  : DB(AsNoTracking) — long-lived DbContext stale 방지(networking.md). 반환 엔티티는 detached.
///   Upsert : 키로 tracked 조회 → 있으면 값 복사(update), 없으면 insert. detached 입력을 안전 반영.
/// </summary>
public class QuestRepository(
    GameServerDbContext context,
    ILogger<QuestRepository> logger) : IQuestRepository
{
    public async Task<List<UserQuest>> GetAllForUserAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            return await context.UserQuests
                .AsNoTracking()
                .Where(q => q.UserId == userId)
                .ToListAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get quests for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserQuest?> GetAsync(long userId, string questId, CancellationToken ct = default)
    {
        try
        {
            return await context.UserQuests
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.UserId == userId && q.QuestId == questId, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get quest {QuestId} for user {UserId}", questId, userId);
            throw;
        }
    }

    public async Task UpsertAsync(UserQuest quest, CancellationToken ct = default)
    {
        try
        {
            var existing = await context.UserQuests
                .SingleOrDefaultAsync(q => q.UserId == quest.UserId && q.QuestId == quest.QuestId, ct);

            if (existing is null)
                await context.UserQuests.AddAsync(quest, ct);
            else
                context.Entry(existing).CurrentValues.SetValues(quest); // Status·Progress·UpdatedAt 복사(키 동일)

            await context.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to upsert quest {QuestId} for user {UserId}", quest.QuestId, quest.UserId);
            throw;
        }
    }
}

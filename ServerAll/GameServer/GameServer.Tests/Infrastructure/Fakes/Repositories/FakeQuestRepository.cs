using GameServer.Application.Domains.Quest.Interfaces;
using GameServer.Domain.Entities.Quest;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

/// <summary>실제 QuestRepository 의 수주/진행 upsert·조회를 인메모리로 모사((userId,questId) 키).</summary>
public sealed class FakeQuestRepository : IQuestRepository
{
    private readonly Dictionary<(long, string), UserQuest> _rows = new();

    public Task<List<UserQuest>> GetAllForUserAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_rows.Values.Where(q => q.UserId == userId).ToList());

    public Task<UserQuest?> GetAsync(long userId, string questId, CancellationToken ct = default)
        => Task.FromResult(_rows.GetValueOrDefault((userId, questId)));

    public Task UpsertAsync(UserQuest quest, CancellationToken ct = default)
    {
        _rows[(quest.UserId, quest.QuestId)] = quest;
        return Task.CompletedTask;
    }
}

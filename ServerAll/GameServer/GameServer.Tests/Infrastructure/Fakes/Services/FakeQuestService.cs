using GameServer.Application.Domains.Quest;
using GameServer.Application.Domains.Quest.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Services;

/// <summary>퀘스트 로직과 무관한 테스트용 no-op. ReportKill 호출 인자만 기록(킬 훅 검증용).</summary>
public sealed class FakeQuestService : IQuestService
{
    public readonly List<(long userId, string monsterId)> ReportedKills = new();

    public Task<List<QuestStateView>> GetQuestsAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(new List<QuestStateView>());

    public Task<QuestAcceptResult> AcceptAsync(long userId, string questId, CancellationToken ct = default)
        => Task.FromResult(QuestAcceptResult.Ok());

    public Task<int> ReportKillAsync(long userId, string monsterId, CancellationToken ct = default)
    {
        ReportedKills.Add((userId, monsterId));
        return Task.FromResult(0);
    }

    public readonly List<(long userId, string npcId)> ReportedTalks = new();

    public Task<int> ReportTalkAsync(long userId, string npcId, CancellationToken ct = default)
    {
        ReportedTalks.Add((userId, npcId));
        return Task.FromResult(0);
    }

    public Task<QuestClaimResult> ClaimRewardAsync(long userId, string questId, CancellationToken ct = default)
        => Task.FromResult(QuestClaimResult.Fail("fake"));
}

using GameServer.Application.Domains.Progression.Interfaces;

namespace GameServer.Application.Domains.Progression;

/// <summary>
/// 진행(경험치) 도메인 서비스 구현. 현재는 Repository 의 lazy get-or-create + 적립을 위임한다.
/// 던전 보상 산정/멱등은 호출자(DungeonResultConsumer)가 담당 — 여기선 단일 유저 적립만.
/// </summary>
public sealed class ProgressionService(IProgressionRepository repository) : IProgressionService
{
    public async Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            var current = await repository.GetAsync(userId, ct);
            return current?.Exp ?? 0;
        }

        var updated = await repository.AddExpAsync(userId, amount, ct);
        return updated.Exp;
    }
}

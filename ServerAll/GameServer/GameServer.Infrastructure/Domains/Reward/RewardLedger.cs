using GameServer.Application.Domains.Reward.Interfaces;
using GameServer.Domain.Entities.Reward;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GameServer.Infrastructure.Domains.Reward;

/// <summary>
/// <see cref="IRewardLedger"/> 구현 — 원장 INSERT 와 실제 지급을 한 트랜잭션으로 묶는다.
///
/// 호출자는 **지급 1건당 새 스코프**를 열어야 한다. 실패 시 DbContext 변경 추적기에
/// 롤백된 변경이 남아, 같은 컨텍스트로 다음 지급을 저장하면 그것까지 함께 새어 나가기 때문이다.
/// (같은 부류의 사고 기록 = codemap §2.107 ② Register 롤백 자기파괴)
/// </summary>
public sealed class RewardLedger(
    GameServerDbContext context,
    ILogger<RewardLedger> logger) : IRewardLedger
{
    public async Task<bool> GrantOnceAsync(
        RewardGrantRequest request,
        Func<CancellationToken, Task> grant,
        CancellationToken ct = default)
    {
        // 빠른 경로 — 이미 지급됐으면 트랜잭션도 열지 않는다(재배달이 흔한 경로라 값어치가 있다).
        bool already = await context.RewardGrants
            .AsNoTracking()
            .AnyAsync(g => g.GrantKey == request.GrantKey, ct);

        if (already)
        {
            logger.LogInformation("[RewardLedger] {GrantKey} 이미 지급됨 — 스킵", request.GrantKey);
            return false;
        }

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var entry = context.RewardGrants.Add(
            RewardGrant.Create(request.GrantKey, request.UserId, request.Kind, request.RefId, request.Amount));

        try
        {
            // 원장을 먼저 쓴다 — 동시 중복은 여기서 UNIQUE 위반으로 걸러진다.
            await context.SaveChangesAsync(ct);

            // 같은 트랜잭션 안에서 실제 지급.
            await grant(ct);

            await tx.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // 다른 인스턴스가 방금 지급했다 — 이중지급이 아니라 정상 경합.
            entry.State = EntityState.Detached;
            await tx.RollbackAsync(ct);
            logger.LogInformation("[RewardLedger] {GrantKey} 경합 — 다른 곳에서 이미 지급", request.GrantKey);
            return false;
        }
        catch
        {
            // 지급 실패 → 원장도 함께 롤백된다. 메시지는 ACK 되지 않아 재배달되고, 그때 다시 시도한다.
            entry.State = EntityState.Detached;
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException e)
        => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

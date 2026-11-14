using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameServer.Infrastructure.Persistence;

internal static class DbContextExtensions
{
    // 행이 없으면 INSERT. 동시에 다른 요청이 먼저 만들었으면(UNIQUE 위반) 조용히 넘어간다.
    public static async Task<bool> AddIfMissingAsync<T>(this DbContext context, T entity, CancellationToken ct)
        where T : class
    {
        var entry = context.Add(entity);
        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            return false;
        }
        finally
        {
            entry.State = EntityState.Detached;
        } // 이후 조회가 이 객체 대신 DB 최신값을 읽도록
    }

    public static bool IsUniqueViolation(DbUpdateException e)
        => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
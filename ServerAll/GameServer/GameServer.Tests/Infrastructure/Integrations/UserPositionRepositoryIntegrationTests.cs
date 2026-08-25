using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Spawn;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// Main 위치 저장소(B7) — **Redis 1차 / DB 확정** 이라는 이 도메인만의 예외를 고정한다.
///
/// 다른 저장소는 "DB 저장 → 캐시 DEL" 인데, 위치는 주기 보고라 쓰기가 매우 잦고 유실이 허용된다.
/// 그래서 주기 쓰기는 Redis 로만 가고 이탈 시점에 한 번 DB 로 확정한다 —
/// 유실 폭이 "마지막 확정 이후"로 한정되는 것이 이 설계의 값어치다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class UserPositionRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private UserPositionRepository CreateRepository()
        => new(fixture.RedisConnection, fixture.CreateDbContext(), NullLogger<UserPositionRepository>.Instance);

    private async Task ClearAsync(long userId)
    {
        await fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.UserPosition(userId));
        await using var ctx = fixture.CreateDbContext();
        var row = await ctx.UserPositions.SingleOrDefaultAsync(p => p.UserId == userId);
        if (row is not null)
        {
            ctx.UserPositions.Remove(row);
            await ctx.SaveChangesAsync();
        }
    }

    private async Task<UserPosition?> DbRowAsync(long userId)
    {
        await using var ctx = fixture.CreateDbContext();
        return await ctx.UserPositions.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
    }

    [Fact]
    public async Task 주기_저장은_Redis_에만_쓰고_DB_는_건드리지_않는다()
    {
        long userId = 98001;
        await ClearAsync(userId);

        var repo = CreateRepository();
        await repo.SaveVolatileAsync(UserPosition.Create(userId, MapIds.MainField01, 1f, 2f, 3f, 90f));

        // 주기 경로가 매번 DB 를 때리면 이 설계의 이유가 사라진다.
        Assert.Null(await DbRowAsync(userId));

        var got = await repo.GetAsync(userId);
        Assert.NotNull(got);
        Assert.Equal(3f, got!.Z, 0.001f);
    }

    [Fact]
    public async Task Flush_하면_DB_로_확정되고_이후_Redis_가_비어도_읽힌다()
    {
        long userId = 98002;
        await ClearAsync(userId);

        var repo = CreateRepository();
        await repo.SaveVolatileAsync(UserPosition.Create(userId, MapIds.MainField01, 4f, 5f, 6f, 45f));
        await repo.FlushToDatabaseAsync(userId);

        Assert.NotNull(await DbRowAsync(userId));

        // 휘발 저장소가 날아가도(서버 재시작·TTL 만료) 마지막 확정값으로 복원된다.
        await fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.UserPosition(userId));

        var got = await CreateRepository().GetAsync(userId);
        Assert.NotNull(got);
        Assert.Equal(6f, got!.Z, 0.001f);
        Assert.Equal(45f, got.RotY, 0.001f);
    }

    [Fact]
    public async Task Flush_는_기존_확정값을_갱신한다()
    {
        long userId = 98003;
        await ClearAsync(userId);

        var repo = CreateRepository();
        await repo.SaveVolatileAsync(UserPosition.Create(userId, MapIds.MainField01, 1f, 0f, 1f, 0f));
        await repo.FlushToDatabaseAsync(userId);

        await CreateRepository().SaveVolatileAsync(UserPosition.Create(userId, MapIds.MainField01, 9f, 0f, 9f, 180f));
        await CreateRepository().FlushToDatabaseAsync(userId);

        var row = await DbRowAsync(userId);
        Assert.NotNull(row);
        Assert.Equal(9f, row!.X, 0.001f);   // INSERT 가 아니라 UPDATE 여야 한다(PK 중복 없이)
        Assert.Equal(180f, row.RotY, 0.001f);
    }

    [Fact]
    public async Task 보고가_없었으면_Flush_는_기존_확정값을_지우지_않는다()
    {
        long userId = 98004;
        await ClearAsync(userId);

        var repo = CreateRepository();
        await repo.SaveVolatileAsync(UserPosition.Create(userId, MapIds.MainField01, 2f, 0f, 2f, 0f));
        await repo.FlushToDatabaseAsync(userId);

        // 이번 세션엔 한 번도 안 움직였다 → 휘발 저장소가 비어 있다.
        await fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.UserPosition(userId));
        await CreateRepository().FlushToDatabaseAsync(userId);

        var row = await DbRowAsync(userId);
        Assert.NotNull(row);
        Assert.Equal(2f, row!.X, 0.001f);   // 지워지거나 0 으로 덮이면 안 된다
    }

    [Fact]
    public async Task 저장된_적이_없으면_null()
    {
        long userId = 98005;
        await ClearAsync(userId);

        Assert.Null(await CreateRepository().GetAsync(userId));
    }
}

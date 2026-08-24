using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.Progression;
using GameServer.Application.Security;
using GameServer.Infrastructure.Common;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Fakes;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using GameServer.Tests.Infrastructure.Fakes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// 유령 방 리퍼가 <b>살아 있는 방을 끊지 않는지</b>를 실 Redis+PG 로 고정한다.
/// </summary>
/// <remarks>
/// 리퍼는 "정리하지 못하는 것"보다 "살아 있는 방을 끊는 것"이 훨씬 나쁜 실패라서,
/// 오탐 쪽을 먼저 못 박는다. 생존 신호는 세션 활동(<c>TouchSessionAsync</c>)이고,
/// Redis 활성 기록이 사라져도 DB <c>LastActiveAt</c> 으로 폴백한다.
/// </remarks>
[Collection("RepositoryIntegrationTests")]
public class DungeonRoomReaperIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions { AccessTokenMinutes = 30 });

    private (DungeonLobbyService Service, UserSessionRepository Sessions, GameServerDbContext Context) CreateScope(
        TimeSpan grace)
    {
        var context = _fixture.CreateDbContext();
        var sessions = new UserSessionRepository(
            _fixture.RedisConnection, context, _jwtOptions, NullLogger<UserSessionRepository>.Instance);

        var service = new DungeonLobbyService(
            new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance),
            new FakeDungeonLobbySubscriptionService(),
            new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance),
            Mock.Of<IOutboxRepository>(),
            sessions,
            new FakeChatSubscriptionService(),
            new FakeUserProfileRepository(),
            new ProgressionService(new FakeProgressionRepository(), new FakeEquipmentService()),
            new RedisRoomReadyStore(_fixture.RedisConnection),
            new NoOpDistributedLock(),
            Options.Create(new DungeonRoomReaperOptions { Grace = grace }),
            NullLogger<DungeonLobbyService>.Instance);

        return (service, sessions, context);
    }

    [Fact]
    public async Task 활동_중인_세션의_방은_유예가_지나도_정리되지_않는다()
    {
        // 로비에 가만히 있어도 클라가 토큰을 갱신하면 서버가 세션을 touch 한다 → 그 방은 살아 있다.
        var (service, sessions, context) = CreateScope(TimeSpan.Zero);
        using var _ = context;

        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var session = await sessions.CreateSessionAsync(host.UserId);

        var room = await service.CreateDungeonRoomAsync(session!.SessionId, "Live Lobby", 4);
        Assert.True(room.IsSuccess);

        var reaped = await service.ReapRoomIfAbandonedAsync(room.Value!.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.False(reaped.Value, "활동 흔적이 있는 방을 끊으면 안 된다");
    }

    [Fact]
    public async Task Redis_활성기록이_비어도_DB의_최근_활동으로_방을_지킨다()
    {
        // 캐시가 날아갔다는 이유로 살아 있는 방을 끊으면 안 된다.
        var (service, sessions, context) = CreateScope(TimeSpan.Zero);
        using var _ = context;

        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var session = await sessions.CreateSessionAsync(host.UserId);
        var room = await service.CreateDungeonRoomAsync(session!.SessionId, "Cache Wiped", 4);

        var db = _fixture.RedisConnection.GetDatabase();
        await db.SortedSetRemoveAsync(RedisKeys.UserSessionActive(), session.SessionId);
        await db.KeyDeleteAsync(RedisKeys.UserSessionMapping(host.UserId));

        var reaped = await service.ReapRoomIfAbandonedAsync(room.Value!.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.False(reaped.Value, "DB LastActiveAt 폴백으로 생존을 인정해야 한다");
    }

    [Fact]
    public async Task 유예를_넘겨_조용한_방은_정리된다()
    {
        var (service, sessions, context) = CreateScope(TimeSpan.FromMinutes(30));
        using var _ = context;

        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var session = await sessions.CreateSessionAsync(host.UserId);
        var room = await service.CreateDungeonRoomAsync(session!.SessionId, "Ghost Lobby", 4);

        // 아무 신호도 남지 않은 상태 = 앱을 종료하고 돌아오지 않은 플레이어.
        var db = _fixture.RedisConnection.GetDatabase();
        await db.SortedSetRemoveAsync(RedisKeys.UserSessionActive(), session.SessionId);
        await db.KeyDeleteAsync(RedisKeys.UserSessionMapping(host.UserId));

        await context.UserSessions
            .Where(us => us.SessionId == session.SessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                us => us.LastActiveAt, DateTime.UtcNow.AddDays(-1)));

        var reaped = await service.ReapRoomIfAbandonedAsync(room.Value!.RoomId);

        Assert.True(reaped.IsSuccess);
        Assert.True(reaped.Value, "돌아오지 않는 방은 정리돼야 한다");
        Assert.Null(await context.DungeonRooms.AsNoTracking()
            .SingleOrDefaultAsync(r => r.RoomId == room.Value.RoomId));
    }
}

using GameServer.Application.Common;
using GameServer.Application.Common.Interfaces;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.Outbox;
using GameServer.Application.Domains.Progression;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Common;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
using GameServer.Tests.Infrastructure.Fakes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// F1(던전 입장 원자성)의 <b>경합 자체</b>를 실제 Redis·PostgreSQL 로 재현한다.
///
/// 단위 테스트는 <c>NoOpDistributedLock</c> 을 쓰므로 상호배제를 검증하지 못한다.
/// 여기서는 진짜 <see cref="RedisDistributedLock"/> 과 진짜 UNIQUE 제약을 걸고
/// 동시 요청을 던져 두 방어선이 실제로 작동하는지 본다.
///
/// 방어가 둘로 나뉜 이유가 테스트 구성에 그대로 드러난다:
///   · 같은 방 경합  → 같은 락 키 → 락이 막는다
///   · 다른 방 경합  → 다른 락 키 → 락은 안 막는다 → UNIQUE 제약이 막는다
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class DungeonRoomJoinConcurrencyTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    /// <summary>
    /// 동시 요청 하나를 흉내낸다. 실제 HTTP 요청처럼 <b>요청마다 DbContext 를 새로 만든다</b>
    /// — DbContext 는 스레드 안전하지 않아 공유하면 경합이 아니라 그냥 깨진다.
    /// </summary>
    private (DungeonLobbyService Service, GameServerDbContext Context) CreateRequestScope(
        FakeUserSessionRepository sessions)
    {
        var context = _fixture.CreateDbContext();

        var service = new DungeonLobbyService(
            new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance),
            new FakeDungeonLobbySubscriptionService(),
            new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance),
            Mock.Of<IOutboxRepository>(),   // JoinRoom 경로에선 호출되지 않는다
            sessions,
            new FakeChatSubscriptionService(),
            new FakeUserProfileRepository(),
            new ProgressionService(new FakeProgressionRepository(), new FakeEquipmentService()),
            new GameServer.Tests.Infrastructure.Fakes.Services.FakeUserPositionService(),
            new RedisRoomReadyStore(_fixture.RedisConnection),
            new RedisDistributedLock(_fixture.RedisConnection),   // ← 진짜 락 (검증 대상)
            Options.Create(new DungeonRoomReaperOptions()),
            NullLogger<DungeonLobbyService>.Instance);

        return (service, context);
    }

    // ── 락 자체의 상호배제 ────────────────────────────────────────────────

    [Fact]
    public async Task 분산락_같은_키에_동시_진입해도_임계구역엔_한_번에_하나만_들어간다()
    {
        var distributedLock = new RedisDistributedLock(_fixture.RedisConnection);
        var lockKey = $"test:room:{Guid.NewGuid():N}";

        var concurrent = 0;
        var maxObserved = 0;
        var entered = 0;

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var _lease = await distributedLock.AcquireAsync(lockKey);

            var now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxObserved, now);

            // 임계구역을 실제로 점유해 겹칠 기회를 준다(락이 없으면 여기서 반드시 겹친다).
            await Task.Delay(30);

            Interlocked.Increment(ref entered);
            Interlocked.Decrement(ref concurrent);
        });

        await Task.WhenAll(tasks);

        Assert.Equal(8, entered);
        Assert.Equal(1, maxObserved);
    }

    [Fact]
    public async Task 분산락_키가_다르면_서로를_막지_않는다()
    {
        // 이 성질 때문에 "한 유저가 서로 다른 두 방에 동시 입장"은 락으로 막을 수 없다.
        // → DB UNIQUE 제약이 필요한 이유(아래 마지막 테스트).
        var distributedLock = new RedisDistributedLock(_fixture.RedisConnection);
        var prefix = Guid.NewGuid().ToString("N");

        var concurrent = 0;
        var maxObserved = 0;

        var tasks = Enumerable.Range(0, 4).Select(async index =>
        {
            await using var _lease = await distributedLock.AcquireAsync($"test:room:{prefix}:{index}");

            var now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxObserved, now);
            await Task.Delay(50);
            Interlocked.Decrement(ref concurrent);
        });

        await Task.WhenAll(tasks);

        Assert.True(maxObserved > 1, $"다른 키끼리는 동시 진입이 가능해야 한다 (관측 최대 동시성 {maxObserved})");
    }

    // ── 축 1: 정원 초과 (같은 방, 서로 다른 유저) ──────────────────────────

    [Fact]
    public async Task JoinRoom_동시_입장이_몰려도_정원을_넘지_않는다()
    {
        const int maxPlayers = 4;
        const int joinerCount = 8;

        using var setupContext = _fixture.CreateDbContext();
        var userRepository = new UserRepository(_fixture.RedisConnection, setupContext, NullLogger<UserRepository>.Instance);
        var roomRepository = new DungeonRoomRepository(_fixture.RedisConnection, setupContext, NullLogger<DungeonRoomRepository>.Instance);
        var playerRepository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, setupContext, NullLogger<DungeonRoomPlayerRepository>.Instance);

        var host = await userRepository.CreateAsync();
        var room = await roomRepository.CreateAsync(host.UserId, "Capacity Race", maxPlayers);
        await playerRepository.CreateAsync(room!.RoomId, host.UserId);

        var sessions = new FakeUserSessionRepository();
        var joinerSessionIds = new List<string>(joinerCount);
        for (var i = 0; i < joinerCount; i++)
        {
            var joiner = await userRepository.CreateAsync();
            var session = await sessions.CreateSessionAsync(joiner.UserId);
            joinerSessionIds.Add(session!.SessionId);
        }

        // Act — 동시에 밀어 넣는다.
        var results = await Task.WhenAll(joinerSessionIds.Select(async sessionId =>
        {
            var (service, context) = CreateRequestScope(sessions);
            try
            {
                return await service.JoinRoomAsync(sessionId, room.RoomId);
            }
            finally
            {
                await context.DisposeAsync();
            }
        }));

        // Assert — 방장 1명 + 성공 인원 = 정원. 초과분은 RoomFull 로 거부.
        var succeeded = results.Count(r => r.IsSuccess);
        Assert.Equal(maxPlayers - 1, succeeded);

        Assert.All(
            results.Where(r => !r.IsSuccess),
            r => Assert.Equal(ErrorCodes.JoinRoomFailed, r.InternalErrorCode));

        using var verifyContext = _fixture.CreateDbContext();
        var actualPlayerCount = await verifyContext.DungeonRoomPlayers
            .AsNoTracking()
            .CountAsync(p => p.RoomId == room.RoomId);

        Assert.Equal(maxPlayers, actualPlayerCount);
    }

    // ── 축 2: 한 유저의 다중 방 입장 (다른 방 → 락이 안 막는다) ────────────

    /// <summary>
    /// UNIQUE 제약을 <b>결정적으로</b> 검증한다. 저장소는 사전 검사가 없어
    /// 두 번째 INSERT 가 DB 제약에 그대로 부딪히므로, 서비스 계층의 check-then-act 가
    /// 우연히 먼저 이기는 일이 없다.
    ///
    /// ⚠️ 아래 동시성 테스트만으로는 부족하다 — 실측(고장 주입)에서 <c>.IsUnique()</c> 를 떼도
    /// 두 요청이 충분히 겹치지 않아 사전 검사가 먼저 걸러 통과해 버렸다.
    /// "제약이 살아 있는가"는 이 테스트가 책임진다.
    /// </summary>
    [Fact]
    public async Task DungeonRoomPlayer_같은_유저를_다른_방에_또_넣으면_UNIQUE_제약이_막는다()
    {
        using var context = _fixture.CreateDbContext();
        var userRepository = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var roomRepository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var playerRepository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomPlayerRepository>.Instance);

        var user = await userRepository.CreateAsync();
        var roomA = await roomRepository.CreateAsync(user.UserId, "Constraint A", 4);
        var roomB = await roomRepository.CreateAsync(user.UserId, "Constraint B", 4);

        await playerRepository.CreateAsync(roomA!.RoomId, user.UserId);

        // 두 번째 방 입장 기록 — 사전 검사를 거치지 않는 경로라 제약이 직접 막아야 한다.
        var thrown = await Assert.ThrowsAsync<PlayerAlreadyInRoomException>(
            () => playerRepository.CreateAsync(roomB!.RoomId, user.UserId));

        Assert.Equal(user.UserId, thrown.UserId);

        // 제약 위반 후에도 첫 방 소속은 그대로 — 실패한 INSERT 가 상태를 오염시키지 않는다.
        using var verifyContext = _fixture.CreateDbContext();
        var rows = await verifyContext.DungeonRoomPlayers
            .AsNoTracking()
            .Where(p => p.UserId == user.UserId)
            .ToListAsync();

        var only = Assert.Single(rows);
        Assert.Equal(roomA.RoomId, only.RoomId);
    }

    /// <summary>
    /// 서비스 계층 end-to-end: 같은 유저가 두 방에 동시에 들어가려 하면 결과는 하나뿐이어야 한다.
    ///
    /// 어느 방어선이 막았는지는 이 테스트의 관심사가 아니다 — 실측상 두 요청이 충분히 겹치지 않으면
    /// 서비스의 사전 검사(<c>GetByUserIdAsync</c>)가 먼저 걸러낸다. 겹칠 때만 UNIQUE 제약이 최종
    /// 방어선으로 동작한다(그 제약 자체는 바로 위 테스트가 결정적으로 검증한다).
    /// 여기서 고정하는 것은 <b>"어떤 타이밍이 나오든 소속은 한 방뿐"</b> 이라는 불변식이다.
    /// </summary>
    [Fact]
    public async Task JoinRoom_같은_유저가_서로_다른_두_방에_동시_입장하면_하나만_성공한다()
    {
        using var setupContext = _fixture.CreateDbContext();
        var userRepository = new UserRepository(_fixture.RedisConnection, setupContext, NullLogger<UserRepository>.Instance);
        var roomRepository = new DungeonRoomRepository(_fixture.RedisConnection, setupContext, NullLogger<DungeonRoomRepository>.Instance);
        var playerRepository = new DungeonRoomPlayerRepository(_fixture.RedisConnection, setupContext, NullLogger<DungeonRoomPlayerRepository>.Instance);

        var hostA = await userRepository.CreateAsync();
        var hostB = await userRepository.CreateAsync();
        var roomA = await roomRepository.CreateAsync(hostA.UserId, "Room A", 4);
        var roomB = await roomRepository.CreateAsync(hostB.UserId, "Room B", 4);
        await playerRepository.CreateAsync(roomA!.RoomId, hostA.UserId);
        await playerRepository.CreateAsync(roomB!.RoomId, hostB.UserId);

        var joiner = await userRepository.CreateAsync();
        var sessions = new FakeUserSessionRepository();
        var session = await sessions.CreateSessionAsync(joiner.UserId);

        // Act — 서로 다른 방이라 락 키가 달라 상호배제가 걸리지 않는다.
        //       막는 것은 dungeon_room_players.UserId UNIQUE 제약뿐이다.
        var results = await Task.WhenAll(
            new[] { roomA.RoomId, roomB.RoomId }.Select(async roomId =>
            {
                var (service, context) = CreateRequestScope(sessions);
                try
                {
                    return await service.JoinRoomAsync(session!.SessionId, roomId);
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }));

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        var rejected = Assert.Single(results.Where(r => !r.IsSuccess));
        Assert.Equal(ErrorCodes.AlreadyInRoom, rejected.InternalErrorCode);

        using var verifyContext = _fixture.CreateDbContext();
        var rows = await verifyContext.DungeonRoomPlayers
            .AsNoTracking()
            .Where(p => p.UserId == joiner.UserId)
            .ToListAsync();

        Assert.Single(rows);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }
}

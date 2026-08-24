using GameServer.Domain.Entities;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.DungeonRoom;
using GameServer.Infrastructure.Domains.User;
using GameServer.Tests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class DungeonRoomRepositoryIntegrationTests(RepositoryTestFixture fixture)
{
    private readonly RepositoryTestFixture _fixture = fixture;

    [Fact]
    public async Task Create_ShouldSaveToDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        long hostId = host.UserId;
        string roomName = "Test Room";

        // Act
        var room = await repository.CreateAsync(hostId, roomName);

        // Assert
        Assert.NotNull(room);
        Assert.True(room.RoomId > 0);

        // Check DB
        var dbRoom = await context.DungeonRooms.FindAsync(room.RoomId);
        Assert.NotNull(dbRoom);
        Assert.Equal(roomName, dbRoom.RoomName);

        // Check Redis
        var redisKey = RedisKeys.DungeonRoom(room.RoomId);
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(redisKey);
        Assert.NotEmpty(entries);
        Assert.Equal(roomName, entries.First(e => e.Name == "RoomName").Value.ToString());
        
        var isActive = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
        Assert.True(isActive);
    }

    [Fact]
    public async Task MapId_ShouldRoundTripThroughDbRedisAndCacheMiss()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);

        // Act
        var room = await repository.CreateAsync(host.UserId, "Map Room", 4, "dungeon_01");

        // Assert: DB 컬럼
        var dbRoom = await context.DungeonRooms.AsNoTracking().SingleAsync(r => r.RoomId == room!.RoomId);
        Assert.Equal("dungeon_01", dbRoom.MapId);

        // Assert: Redis Hash 필드(ToHashEntry)
        var entries = await _fixture.RedisConnection.GetDatabase().HashGetAllAsync(RedisKeys.DungeonRoom(room!.RoomId));
        Assert.Equal("dungeon_01", entries.First(e => e.Name == "MapId").Value.ToString());

        // Assert: 캐시 MISS → DB 재구성 시에도 MapId 복원(ParseFromRedis 경로 제외, DB→FromRedis)
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoom(room.RoomId));
        var reloaded = await repository.GetByIdAsync(room.RoomId);
        Assert.Equal("dungeon_01", reloaded!.MapId);
    }

    [Fact]
    public async Task Read_HIT_ShouldReturnFromCacheWithoutDbQuery()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(host.UserId, "Hit Room");

        // Act
        // DB에서 직접 삭제하여 캐시 히트를 증명 (DB에 없는데 반환되면 캐시에서 가져온 것)
        context.DungeonRooms.Remove(room!);
        await context.SaveChangesAsync();

        var cachedRoom = await repository.GetByIdAsync(room!.RoomId);

        // Assert
        Assert.NotNull(cachedRoom);
        Assert.Equal("Hit Room", cachedRoom.RoomName);
    }

    [Fact]
    public async Task Read_MISS_ShouldLoadFromDbAndReCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(host.UserId, "Miss Room");

        // Act
        // Redis 캐시 강제 삭제
        await _fixture.RedisConnection.GetDatabase().KeyDeleteAsync(RedisKeys.DungeonRoom(room!.RoomId));

        var dbRoom = await repository.GetByIdAsync(room.RoomId);

        // Assert
        Assert.NotNull(dbRoom);
        Assert.Equal("Miss Room", dbRoom.RoomName);

        // Re-cache 확인
        var redisKey = RedisKeys.DungeonRoom(room.RoomId);
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(redisKey);
        Assert.True(exists);
    }

    [Fact]
    public async Task Update_ShouldUpdateDbAndInvalidateCache()
    {
        // Arrange
        long roomId;
        long hostId;
        const int initialMaxPlayers = 4;
        const int updatedMaxPlayers = 2;

        using (var arrangeContext = _fixture.CreateDbContext())
        {
            var userRepo = new UserRepository(_fixture.RedisConnection, arrangeContext, NullLogger<UserRepository>.Instance);
            var host = await userRepo.CreateAsync();
            hostId = host.UserId;

            var repository = new DungeonRoomRepository(_fixture.RedisConnection, arrangeContext, NullLogger<DungeonRoomRepository>.Instance);
            var room = await repository.CreateAsync(hostId, "Original Room", initialMaxPlayers);
            roomId = room!.RoomId;
        }

        // Act
        using (var actContext = _fixture.CreateDbContext())
        {
            var dbRoomToUpdate = await actContext.DungeonRooms.FindAsync(roomId);
            dbRoomToUpdate!.UpdateRoomSettings(hostId, 0, "Updated Room", updatedMaxPlayers);
            actContext.DungeonRooms.Update(dbRoomToUpdate);
            await actContext.SaveChangesAsync();

            var repository = new DungeonRoomRepository(_fixture.RedisConnection, actContext, NullLogger<DungeonRoomRepository>.Instance);
            await repository.UpdateAsync(dbRoomToUpdate); // 캐시 무효화
        }

        // Assert
        // DB 확인 (새 context 사용)
        using var assertContext = _fixture.CreateDbContext();
        var dbRoom = await assertContext.DungeonRooms.AsNoTracking().FirstOrDefaultAsync(r => r.RoomId == roomId);
        Assert.Equal("Updated Room", dbRoom!.RoomName);
        Assert.Equal(updatedMaxPlayers, dbRoom.MaxPlayers);

        // Cache 무효화 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoom(roomId));
        Assert.False(exists);
    }

    [Fact]
    public async Task Delete_ShouldRemoveFromDbAndCache()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(host.UserId, "Delete Room");

        // Act
        await repository.DeleteAsync(room!.RoomId);

        // Assert
        var dbRoom = await context.DungeonRooms.FindAsync(room.RoomId);
        Assert.Null(dbRoom);

        // Cache 확인
        var exists = await _fixture.RedisConnection.GetDatabase().KeyExistsAsync(RedisKeys.DungeonRoom(room.RoomId));
        Assert.False(exists);
        
        var isActive = await _fixture.RedisConnection.GetDatabase().SetContainsAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
        Assert.False(isActive);
    }

    [Fact]
    public async Task TTL_ShouldBeSetOnCacheKeys()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var room = await repository.CreateAsync(host.UserId, "TTL Room");

        // Act
        var ttl = await _fixture.RedisConnection.GetDatabase().KeyTimeToLiveAsync(RedisKeys.DungeonRoom(room!.RoomId));

        // Assert
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMinutes > 0);
        Assert.True(ttl.Value.TotalMinutes <= 30);
    }

    [Fact]
    public async Task GetAllActiveRooms_활성화된_방만_반환한다()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host1 = await userRepo.CreateAsync();
        var host2 = await userRepo.CreateAsync();

        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var activeRoom = await repository.CreateAsync(host1.UserId, "Active Room");
        var closedRoom = await repository.CreateAsync(host2.UserId, "Closed Room");

        // Closed 방은 ActiveSet 에서 제거
        await repository.DeleteAsync(closedRoom!.RoomId);

        // Act
        var activeRooms = await repository.GetAllActiveRoomsAsync();

        // Assert
        Assert.Contains(activeRooms, r => r.RoomId == activeRoom!.RoomId);
        Assert.DoesNotContain(activeRooms, r => r.RoomId == closedRoom.RoomId);
    }

    [Fact]
    public async Task 활성_방_목록은_Redis_활성셋이_불완전해도_DB의_활성_방을_전부_돌려준다()
    {
        // 회귀 고정: `game:room:active` 는 항목별이 아니라 **집합 전체**에 TTL 이 걸려 있어
        // 멤버십이 수시로 흔들린다(만료 후 최근 접근분만 재적재). 그때 목록이 집합만 믿으면
        // DB 에 살아 있는 방이 통째로 사라지고, 연속 호출이 서로 다른 집합을 보게 돼
        // 페이징에서 같은 방이 두 페이지에 나온다.
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);
        var db = _fixture.RedisConnection.GetDatabase();

        var hostA = await userRepo.CreateAsync();
        var hostB = await userRepo.CreateAsync();
        var hostC = await userRepo.CreateAsync();

        var roomA = await repository.CreateAsync(hostA.UserId, "Drift A");
        var roomB = await repository.CreateAsync(hostB.UserId, "Drift B");
        var roomC = await repository.CreateAsync(hostC.UserId, "Drift C");

        // 집합에서 한 방만 빠진 상태 = TTL 만료 후 일부만 재적재된 상황
        await db.SetRemoveAsync(RedisKeys.DungeonRoomActive(), roomC!.RoomId);

        var rooms = (await repository.GetAllActiveRoomsAsync()).ToList();

        Assert.Contains(rooms, r => r.RoomId == roomA!.RoomId);
        Assert.Contains(rooms, r => r.RoomId == roomB!.RoomId);
        Assert.Contains(rooms, r => r.RoomId == roomC.RoomId);
    }

    [Fact]
    public async Task 활성_방_페이지는_DB에서_최신순으로_잘라_오고_총계를_함께_준다()
    {
        // B4: 전량 조회 후 메모리에서 Skip/Take 하던 것을 DB 로 내린다.
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);

        var before = await repository.GetActiveRoomCountAsync();

        var created = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var host = await userRepo.CreateAsync();
            var room = await repository.CreateAsync(host.UserId, $"Page {i}");
            created.Add(room!.RoomId);
        }

        var page = await repository.GetActiveRoomsPageAsync(offset: 0, limit: 2);

        Assert.Equal(2, page.Rooms.Count);
        Assert.Equal(before + 3, page.TotalCount);
        // 최신순(RoomId 내림차순)
        Assert.Equal(created[2], page.Rooms[0].RoomId);
        Assert.Equal(created[1], page.Rooms[1].RoomId);

        var next = await repository.GetActiveRoomsPageAsync(offset: 2, limit: 2);
        Assert.DoesNotContain(next.Rooms, r => r.RoomId == created[2]);
    }

    // ── 캐시 stale 버그 회귀 테스트 ──────────────────────────────────
    // 버그 재현 시나리오:
    //   AddWithRoomUpdateAsync는 DB만 업데이트하고 Redis 캐시를 갱신하지 않는다.
    //   이후 GetByIdAsync가 stale Waiting 상태를 반환하면
    //   GameSessionReadyConsumer가 MarkGameSessionReady()를 스킵 → GameSessionEvent 미전송.
    //
    // 수정 검증:
    //   StartGameAsync 호출 후 InvalidateCacheAsync를 명시적으로 호출해야
    //   다음 GetByIdAsync가 DB에서 최신 Starting 상태를 읽어야 한다.

    [Fact]
    public async Task 캐시_무효화_없이는_StartGame_후_Waiting_stale_상태가_반환된다_버그_재현()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);

        // 방 생성 → DB + Redis에 Waiting 상태 저장
        var room = await repository.CreateAsync(host.UserId, "Stale Test Room");
        Assert.Equal(RoomStatus.Waiting, room!.Status);

        // OutboxRepository.AddWithRoomUpdateAsync 시뮬레이션:
        // DB만 Starting으로 업데이트, Redis 캐시는 건드리지 않음
        room.StartGame(host.UserId, 1);
        using var contextForUpdate = _fixture.CreateDbContext();
        contextForUpdate.DungeonRooms.Update(room);
        await contextForUpdate.SaveChangesAsync();
        // ← InvalidateCacheAsync 호출 없음 (버그 상황)

        // Act: GetByIdAsync → Redis에 Waiting 캐시가 남아 있으므로 Waiting 반환
        var staleRoom = await repository.GetByIdAsync(room.RoomId);

        // Assert: stale 캐시 때문에 Starting이 아닌 Waiting이 반환됨 → 버그 확인
        Assert.Equal(RoomStatus.Waiting, staleRoom!.Status); // stale hit
    }

    [Fact]
    public async Task 캐시_무효화_후_StartGame_후_Starting_상태가_반환된다_수정_검증()
    {
        // Arrange
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var repository = new DungeonRoomRepository(_fixture.RedisConnection, context, NullLogger<DungeonRoomRepository>.Instance);

        // 방 생성 → DB + Redis에 Waiting 상태 저장
        var room = await repository.CreateAsync(host.UserId, "Fix Test Room");
        Assert.Equal(RoomStatus.Waiting, room!.Status);

        // OutboxRepository.AddWithRoomUpdateAsync 시뮬레이션: DB만 업데이트
        room.StartGame(host.UserId, 1);
        using var contextForUpdate = _fixture.CreateDbContext();
        contextForUpdate.DungeonRooms.Update(room);
        await contextForUpdate.SaveChangesAsync();

        // 수정: InvalidateCacheAsync 호출 → Redis 캐시 삭제
        await repository.InvalidateCacheAsync(room.RoomId);

        // Act: GetByIdAsync → 캐시 미스 → DB에서 Starting 로드
        var freshRoom = await repository.GetByIdAsync(room.RoomId);

        // Assert: 캐시 무효화 후 DB의 Starting 상태를 반환해야 함
        Assert.Equal(RoomStatus.Starting, freshRoom!.Status);
    }

    // ── 장수(long-lived) DbContext + EF 추적 stale 버그 회귀 테스트 ──────
    // 실제 버그:
    //   SubscribeRoom 스트리밍 RPC는 Scoped DbContext를 수십 초 유지한다.
    //   GetByIdAsync의 DB 폴백이 추적 쿼리면, 먼저 적재된 stale 엔티티(Starting)를
    //   계속 반환하고 다른 스코프가 쓴 Playing을 영원히 못 읽는다.
    //   → SendLoop이 Starting만 읽어 GameSessionEvent 대신 UpdateEvent만 전송.
    //
    // 수정 검증:
    //   GetByIdAsync에 AsNoTracking 적용 → 같은 DbContext로 재조회해도 매번 DB 최신값.

    [Fact]
    public async Task 장수_DbContext로_재조회해도_다른_스코프의_DB_변경을_읽는다()
    {
        // Arrange ── 방 생성 (Waiting)
        using var seedContext = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, seedContext, NullLogger<UserRepository>.Instance);
        var host = await userRepo.CreateAsync();
        var seedRepo = new DungeonRoomRepository(_fixture.RedisConnection, seedContext, NullLogger<DungeonRoomRepository>.Instance);

        var room = await seedRepo.CreateAsync(host.UserId, "LongLived Room");
        var roomId = room!.RoomId;

        // 방을 Starting으로 만들고 캐시 무효화 (StartGame 시점 재현)
        room.StartGame(host.UserId, 1);
        await seedRepo.UpdateAsync(room); // DB=Starting, 캐시 DEL

        // ── 스트리밍 RPC가 쓰는 "장수" DbContext (수십 초 유지되는 단일 컨텍스트) ──
        using var streamingContext = _fixture.CreateDbContext();
        var streamingRepo = new DungeonRoomRepository(_fixture.RedisConnection, streamingContext, NullLogger<DungeonRoomRepository>.Instance);

        // 1차 조회: 캐시 미스 → DB(Starting) 읽음. 추적 쿼리였다면 여기서 streamingContext에 Starting이 적재됨.
        var firstRead = await streamingRepo.GetByIdAsync(roomId);
        Assert.Equal(RoomStatus.Starting, firstRead!.Status);

        // ── 다른 스코프(Consumer 재현)가 DB를 Playing으로 변경 + 캐시 무효화 ──
        using var consumerContext = _fixture.CreateDbContext();
        var consumerRepo = new DungeonRoomRepository(_fixture.RedisConnection, consumerContext, NullLogger<DungeonRoomRepository>.Instance);
        var consumerRoom = await consumerRepo.GetByIdAsync(roomId);
        consumerRoom!.MarkGameSessionReady(); // Starting → Playing
        await consumerRepo.UpdateAsync(consumerRoom); // DB=Playing, 캐시 DEL

        // Act ── 같은 장수 DbContext로 재조회 (SendLoop 2차 처리 재현)
        var secondRead = await streamingRepo.GetByIdAsync(roomId);

        // Assert ── AsNoTracking이므로 DB 최신값(Playing)을 읽어야 한다.
        //          추적 쿼리였다면 streamingContext가 캐싱한 stale Starting이 반환되어 실패.
        Assert.Equal(RoomStatus.Playing, secondRead!.Status);
    }
}

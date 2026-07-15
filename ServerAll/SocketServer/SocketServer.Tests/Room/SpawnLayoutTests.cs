using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// 결정론적 스폰 검증.
///
/// 핵심 규칙:
///   - SpawnResolver.Resolve 는 같은 (layout, index)에 항상 같은 SpawnPoint 를 반환한다.
///   - 인덱스가 포인트 수를 넘으면 모듈러로 순환한다(음수 포함).
///   - CreateRoom 은 dungeon_01 레이아웃 기준으로 플레이어별 위치/회전/SpawnIndex 를 분배한다.
///
/// 이 기대 벡터는 클라이언트 EditMode 의 SpawnResolverTests 와 동일해야 한다(미러 drift 방지).
/// </summary>
public class SpawnLayoutTests
{
    [Fact]
    public void dungeon_01_레이아웃은_인덱스별_고정_좌표를_반환한다()
    {
        var layout = SpawnLayoutTable.Get(MapIds.Dungeon01);

        // dungeon_01 재기획(2026-07): 파티는 맵 남쪽(Z-16 부근)에서 북향(+Z)으로 진입한다.
        Assert.Equal(new SpawnPoint(0f, 0f, -16f, 0f), SpawnResolver.Resolve(layout, 0));
        Assert.Equal(new SpawnPoint(2f, 0f, -16f, 0f), SpawnResolver.Resolve(layout, 1));
        Assert.Equal(new SpawnPoint(-2f, 0f, -16f, 0f), SpawnResolver.Resolve(layout, 2));
        Assert.Equal(new SpawnPoint(0f, 0f, -18f, 0f), SpawnResolver.Resolve(layout, 3));
    }

    [Fact]
    public void 인덱스가_포인트_수를_넘으면_모듈러로_순환한다()
    {
        var layout = SpawnLayoutTable.Get(MapIds.Dungeon01); // 4개 포인트

        Assert.Equal(SpawnResolver.Resolve(layout, 0), SpawnResolver.Resolve(layout, 4));
        Assert.Equal(SpawnResolver.Resolve(layout, 1), SpawnResolver.Resolve(layout, 5));
        Assert.Equal(SpawnResolver.Resolve(layout, 3), SpawnResolver.Resolve(layout, -1));
    }

    [Fact]
    public void 알수없는_맵은_예외를_던진다()
    {
        Assert.Throws<KeyNotFoundException>(() => SpawnLayoutTable.Get("no_such_map"));
    }

    [Fact]
    public void CreateRoom은_플레이어별로_스폰_위치와_인덱스를_분배한다()
    {
        var roomManager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        const long roomId = 10;
        var message = new GameStartRequestedMessage
        {
            RoomId = roomId,
            TraceId = "trace-test",
            MapId = MapIds.Dungeon01,
            PlayerInfos = new List<PlayerInfo>
            {
                new() { UserId = 100, Nickname = "A", SpawnIndex = 0 },
                new() { UserId = 200, Nickname = "B", SpawnIndex = 1 }
            }
        };

        var room = roomManager.CreateRoom(roomId, message.PlayerInfos, message);
        Assert.NotNull(room);
        Assert.Equal(MapIds.Dungeon01, room!.MapId);

        var a = room.GetPlayerState(100)!;
        Assert.Equal(0, a.SpawnIndex);
        Assert.Equal(0f, a.PosX);
        Assert.Equal(-16f, a.PosZ);

        var b = room.GetPlayerState(200)!;
        Assert.Equal(1, b.SpawnIndex);
        Assert.Equal(2f, b.PosX);
        Assert.Equal(0f, b.RotY);
    }
}

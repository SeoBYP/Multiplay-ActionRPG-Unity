using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Room;

/// <summary>
/// 합산 전투 스탯 전파(2.4 증분1) — GameServer 가 게임시작 메시지로 보낸 스탯을 SocketServer 가
/// PlayerState 에 세팅하는지. SocketServer 는 DB 접근 없이 메시지 값만 받는다(authority-model §4c).
/// </summary>
public class PlayerStatSeedTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "P100", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    [Fact]
    public void InitPlayerState는_전달된_스탯을_PlayerState에_세팅한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0f, 0f, 0f, 0f, attackPower: 25, defense: 8, maxHealth: 300);

        var p = room.GetAllPlayerStates().Single();
        Assert.Equal(25, p.AttackPower);
        Assert.Equal(8, p.Defense);
        Assert.Equal(300, p.Hp);   // MaxHealth>0 → 권위값으로 만피
        Assert.Equal(300, p.MaxHp);
    }

    [Fact]
    public void 스탯_미설정이면_MaxHp는_상수폴백_스탯은_0이다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0f, 0f, 0f, 0f); // 스탯 인자 생략(레거시/테스트 경로)

        var p = room.GetAllPlayerStates().Single();
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp, p.MaxHp);
        Assert.Equal(0, p.AttackPower);
        Assert.Equal(0, p.Defense);
    }

    [Fact]
    public void PlayerInfo_스탯필드는_기본값_0이다()
    {
        // GameServer 가 안 채운 경우(구 메시지)도 직렬화/수신이 깨지지 않아야 한다.
        var info = new PlayerInfo { UserId = 1, Nickname = "A", SpawnIndex = 0 };
        Assert.Equal(0, info.AttackPower);
        Assert.Equal(0, info.Defense);
        Assert.Equal(0, info.MaxHealth);
    }

    [Fact]
    public void CreateRoom은_게임시작_메시지의_PlayerInfo_스탯을_PlayerState로_전파한다()
    {
        // 실제 production 경로(RoomManager.CreateRoom → InitPlayerState) — 던전에 스탯이 들어가는지.
        var roomManager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        var message = new GameStartRequestedMessage
        {
            RoomId = 1,
            TraceId = "t",
            PlayerInfos = new List<PlayerInfo>
            {
                new() { UserId = 100, Nickname = "A", SpawnIndex = 0, AttackPower = 25, Defense = 8, MaxHealth = 300 },
            },
        };

        var room = roomManager.CreateRoom(1, message.PlayerInfos, message);

        var p = room!.GetAllPlayerStates().Single();
        Assert.Equal(25, p.AttackPower);
        Assert.Equal(8, p.Defense);
        Assert.Equal(300, p.MaxHp);
    }
}

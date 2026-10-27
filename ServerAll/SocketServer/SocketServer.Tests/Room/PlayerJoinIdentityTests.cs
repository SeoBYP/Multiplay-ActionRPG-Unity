using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Infrastructure.Messages;
using Shared.Packet.Packets;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Server.Tests.Room;

/// <summary>
/// S2 — C_PlayerJoin 의 신원.
///
/// 지금 서버는 "이 userId 가 이 방에 배정됐는가"(Redis gamesession:player:{userId})만 본다.
/// 연결이 <b>그 userId 본인인가</b>는 보지 않는다. userId 는 S_PlayerJoined/S_Move 로 파티원에게 공개된다.
///
/// 이 파일은 <b>프로토콜 변경 없이</b> 막을 수 있는 경로만 고정한다:
///   이미 A 로 입장한 연결이 B 로 다시 Join 하면 거부한다.
///
/// ⚠ "새 연결이 피해자 userId 로 Join" 은 정상 재접속(ReconnectTakeoverTests)과 구분할 정보가 지금 패킷에 없다.
///    그 경로는 join 토큰(공개 계약 변경)이 결정된 뒤 테스트를 추가한다.
/// </summary>
public sealed class PlayerJoinIdentityTests : IAsyncLifetime
{
    private const long RoomId = 1;
    private const long Victim = 100;
    private const long Attacker = 200;

    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private IConnectionMultiplexer _mux = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _mux = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());

        var db = _mux.GetDatabase();
        foreach (var (userId, nick) in new[] { (Victim, "A"), (Attacker, "B") })
        {
            await db.HashSetAsync($"gamesession:player:{userId}", new[]
            {
                new HashEntry("roomId", RoomId),
                new HashEntry("nickname", nick),
            });
        }
    }

    public async Task DisposeAsync()
    {
        await _mux.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private Session NewSession(RoomManager manager, ulong sessionId)
        => new(
            sessionId,
            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
            dispatcher: null!,
            manager,
            _mux.GetDatabase(),
            NullLogger<Session>.Instance,
            onDisconnected: null);

    private static (RoomManager manager, global::Server.Room.Room room) NewRoom()
    {
        var manager = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        var message = new GameStartRequestedMessage
        {
            RoomId = RoomId,
            PlayerInfos = new List<PlayerInfo>
            {
                new() { UserId = Victim,   Nickname = "A", SpawnIndex = 0 },
                new() { UserId = Attacker, Nickname = "B", SpawnIndex = 1 },
            },
            TraceId = "trace-join-identity",
        };
        var room = manager.CreateRoom(message.RoomId, message.PlayerInfos, message)!;
        room.AddPlayer(Victim, "A", 0, 1f, 0f, 1f, 0f);
        room.AddPlayer(Attacker, "B", 1, 2f, 0f, 2f, 0f);
        return (manager, room);
    }

    [Fact]
    public async Task 이미_입장한_연결은_다른_UserId로_다시_Join할_수_없다()
    {
        var (manager, room) = NewRoom();
        var victim = NewSession(manager, 1);
        var attacker = NewSession(manager, 2);

        await RoomJoinLeaveHandler.HandlePlayerJoin(victim, new C_PlayerJoin { RoomId = RoomId, UserId = Victim }, CancellationToken.None);
        await RoomJoinLeaveHandler.HandlePlayerJoin(attacker, new C_PlayerJoin { RoomId = RoomId, UserId = Attacker }, CancellationToken.None);
        Assert.NotNull(room.Sessions.Get(1)); // 전제: 둘 다 정상 입장
        Assert.NotNull(room.Sessions.Get(2));

        // 공격: 이미 Attacker 로 입장한 연결이 Victim 의 id 로 Join 을 다시 보낸다.
        await RoomJoinLeaveHandler.HandlePlayerJoin(attacker, new C_PlayerJoin { RoomId = RoomId, UserId = Victim }, CancellationToken.None);

        Assert.Equal(Attacker, attacker.UserId);          // 연결의 신원은 바뀌지 않는다
        Assert.NotNull(room.Sessions.Get(1));              // 피해자는 방에서 밀려나지 않는다
        Assert.Same(room, victim.Room);
        Assert.NotNull(room.Actors.GetMember(Victim));     // 피해자 액터는 지워지지 않는다
        Assert.NotNull(room.Actors.GetMember(Attacker));   // 공격자 자기 액터도 유령/삭제가 되지 않는다
    }
}

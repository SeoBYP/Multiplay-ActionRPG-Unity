using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;
using Server.Tests.Fakes;
using Shared.Packet;
using Shared.Packet.Packets;

// ⚠ 네임스페이스에 'Session' 세그먼트를 쓰지 않는다 — 전역 `Session` 타입을 가려
//   TestSessionFactory 등 기존 테스트가 CS0118 로 깨진다(.claude/rules/testing.md 의 'System' 금지와 같은 뿌리).
namespace Server.Tests.Sessions;

/// <summary>
/// AC-C2: 세션 송신 큐(D1 수정). 여러 스레드가 한 소켓에 직접 <c>SendAsync</c> 를 걸면 부분 전송이
/// 일어나는 순간 <b>한 프레임 중간에 다른 프레임 바이트가 끼어들어</b> 길이-프리픽스 파싱이 깨진다.
/// 큐 + 단일 소비자로 프레임 단위 원자성을 보장한다.
///
/// <para>실제 루프백 소켓을 쓴다 — 프레임 원자성은 **진짜 write** 에서만 드러나므로 목으로는 의미가 없다.</para>
/// </summary>
public class SessionSendQueueTests : IDisposable
{
    private readonly Socket _listener;
    private readonly Socket _client;   // 반대편(= 클라 역할). 여기서 바이트를 읽어 프레임을 검사한다.
    private readonly Socket _server;   // Session 이 소유
    private readonly CancellationTokenSource _cts = new();

    public SessionSendQueueTests()
    {
        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        _listener.Listen(1);

        _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _client.Connect((IPEndPoint)_listener.LocalEndPoint!);
        _server = _listener.Accept();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _client.Close(); } catch { }
        try { _server.Close(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }

    private global::Session NewSession()
    {
        var rm = new RoomManager(
            NullLogger<RoomManager>.Instance,
            NullLogger<global::Server.Room.Room>.Instance,
            new FakeRoomLifecyclePublisher(),
            new FakeDungeonResultPublisher(),
            new FakeLootPickupPublisher());

        return new global::Session(1UL, _server, dispatcher: null!, rm,
            redis: null!, NullLogger<global::Session>.Instance, onDisconnected: null);
    }

    /// <summary>클라 쪽에서 길이-프리픽스 프레임 N 개를 읽어 역직렬화한다. 깨진 프레임이면 여기서 드러난다.</summary>
    private List<Packet> ReadFrames(int count, TimeSpan timeout)
    {
        _client.ReceiveTimeout = (int)timeout.TotalMilliseconds;
        var result = new List<Packet>();

        for (int i = 0; i < count; i++)
        {
            byte[] lenBuf = ReadExact(4);
            int len = BitConverter.ToInt32(lenBuf, 0);
            Assert.InRange(len, 1, 65536); // 프레임이 섞이면 여기서 말도 안 되는 길이가 나온다
            byte[] body = ReadExact(len);

            var packet = PacketSerializer.Deserialize(body);
            Assert.NotNull(packet);
            result.Add(packet!);
        }

        return result;
    }

    private byte[] ReadExact(int n)
    {
        var buf = new byte[n];
        int off = 0;
        while (off < n)
        {
            int r = _client.Receive(buf, off, n - off, SocketFlags.None);
            if (r == 0) throw new IOException("peer closed");
            off += r;
        }
        return buf;
    }

    [Fact]
    public async Task 여러_스레드가_동시에_보내도_프레임이_섞이지_않는다()
    {
        // D1 의 핵심 계약.
        //
        // ⚠ **부분 전송을 강제해야 의미가 있다.** 프레임이 소켓 송신 버퍼보다 작으면 한 번의 write 로 나가
        //   애초에 섞일 수가 없다 → 그런 테스트는 큐를 빼도 통과한다(실제로 겪었다).
        //   그래서 버퍼를 작게(512) + 프레임을 훨씬 크게(~20KB) 만들어 **한 프레임이 여러 번의 write 로 쪼개지게** 한다.
        //   이 상태에서 큐가 없으면 스레드 A 의 프레임 중간에 스레드 B 의 바이트가 끼어들어 길이-프리픽스가 깨진다.
        _server.SendBufferSize = 512;

        var session = NewSession();
        var run = session.RunAsync(_cts.Token);

        const int threads = 4;
        const int perThread = 5;
        const int total = threads * perThread;

        string big = new string('x', 20_000); // 프레임 ≫ 송신 버퍼 → 부분 전송 보장

        // 리더를 **동시에** 돌린다 — 안 그러면 버퍼가 차서 송신이 멈춰 교착된다(큐 없는 구현에선 특히).
        var reader = Task.Run(() => ReadFrames(total, TimeSpan.FromSeconds(20)));

        var senders = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < perThread; i++)
                session.SendPacketAsync(new S_PlayerJoined { Success = true, Message = big });
        })).ToArray();

        await Task.WhenAll(senders);

        // 모든 프레임이 온전히 파싱돼야 한다 — 하나라도 섞였으면 길이 검사/역직렬화에서 터진다.
        var frames = await reader;
        Assert.Equal(total, frames.Count);
        Assert.All(frames, p => Assert.Equal(big, Assert.IsType<S_PlayerJoined>(p).Message));

        session.Disconnect();
        _cts.Cancel();
        await Task.WhenAny(run, Task.Delay(1000));
    }

    [Fact]
    public async Task 넣은_순서대로_나간다()
    {
        // 한 스레드가 연달아 넣은 패킷의 순서 보존 — 입장 시 로스터를 순차 전송하는 핸들러가 이것에 의존한다.
        var session = NewSession();
        var run = session.RunAsync(_cts.Token);

        const int n = 50;
        for (int i = 0; i < n; i++)
            await session.SendPacketAsync(new S_MonsterState { InstanceId = i, Seq = i });

        var frames = ReadFrames(n, TimeSpan.FromSeconds(10));
        for (int i = 0; i < n; i++)
            Assert.Equal(i, ((S_MonsterState)frames[i]).InstanceId);

        session.Disconnect();
        _cts.Cancel();
        await Task.WhenAny(run, Task.Delay(1000));
    }

    [Fact]
    public void 큐가_포화되면_세션을_끊는다_무한큐_금지()
    {
        // 무한 큐로 두면 느린 클라 하나가 서버 메모리를 계속 먹는다(DoS 벡터) → 포화 시 그 세션만 끊는다.
        // SendLoop 을 띄우지 않아(RunAsync 미호출) 큐가 비워지지 않는 상황을 만든다 = "전혀 못 받는 클라".
        var session = NewSession();

        for (int i = 0; i < global::Session.SendQueueCapacity; i++)
            session.SendPacketAsync(new S_MonsterState { InstanceId = i });

        Assert.True(session.Connected, "용량까지는 정상적으로 쌓인다");

        session.SendPacketAsync(new S_MonsterState { InstanceId = 9999 }); // 한 개 초과

        Assert.False(session.Connected, "포화 = 사실상 죽은 연결 → 끊어야 한다");
    }

    [Fact]
    public async Task 끊긴_세션에_보내면_조용히_무시한다()
    {
        var session = NewSession();
        session.Disconnect();

        var ex = await Record.ExceptionAsync(() => session.SendPacketAsync(new S_Pong { IsHealthy = true }));

        Assert.Null(ex); // 끊긴 뒤의 브로드캐스트가 예외로 번지면 방 전체 틱이 흔들린다
    }
}

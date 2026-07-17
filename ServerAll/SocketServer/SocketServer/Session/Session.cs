using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Server.PacketHandler;
using Server.Room;
using Shared.Packet;
using Shared.Packet.Packets;
using StackExchange.Redis;

public sealed class Session
{
    public long UserId { get; set; }
    public ulong SessionId { get; private set; }
    public bool Connected { get; private set; }
    public DateTime LastRecvAt { get; private set; }
    public DateTime ConnectedAt { get; }
    public string Nickname { get; set; }

    public Room? Room { get; set; }
    public RoomManager RoomManager { get; }
    public IDatabase Redis { get; }

    private Socket Socket;
    private Action<ulong>? _onDisconnected;
    private PacketDispatcher _dispatcher;
    private readonly ILogger<Session> _logger;

    /// <summary>
    /// 세션당 송신 큐 용량(프레임 수). 넘치면 그 세션을 끊는다 — 방 틱이 10Hz 라 1024 는
    /// <b>약 100초치</b>다. 여기까지 밀린 클라는 사실상 죽은 연결이고, 무한 큐로 두면
    /// 느린 클라 하나가 서버 메모리를 계속 먹는다(DoS 벡터).
    /// </summary>
    public const int SendQueueCapacity = 1024;

    /// <summary>
    /// 송신 큐(AC-C2, D1 수정). <b>생산자 N(틱·패킷 스레드) → 소비자 1(SendLoop)</b>.
    /// <para>
    /// 이게 없으면 여러 스레드가 같은 소켓에 <c>SendAsync</c> 를 동시에 걸고, 부분 전송이 일어나는 순간
    /// <b>한 프레임 중간에 다른 프레임 바이트가 끼어들어</b> 길이-프리픽스 파싱이 깨진다(치명).
    /// </para>
    /// <para>
    /// <c>FullMode.Wait</c> 를 쓰지만 <b>Wait 하지 않는다</b> — <c>TryWrite</c> 로만 넣어 가득 차면 즉시 false 를
    /// 받는다(대기 모드는 TryWrite 가 실패를 알려주는 유일한 모드다). 생산자가 틱 스레드라 <b>절대 블록시키면 안 된다</b>.
    /// </para>
    /// </summary>
    private readonly Channel<byte[]> _sendQueue = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(SendQueueCapacity)
        {
            SingleReader = true,   // SendLoop 하나만 읽는다 = 프레임 원자성의 근거
            SingleWriter = false,  // 틱·패킷 스레드가 함께 넣는다
            FullMode = BoundedChannelFullMode.Wait,
        });

    public Session(
        ulong sessionId,
        Socket socket,
        PacketDispatcher dispatcher,
        RoomManager roomManager,
        IDatabase redis,
        ILogger<Session> logger,
        Action<ulong>? onDisconnected = null)
    {
        SessionId = sessionId;
        Socket = socket;
        _dispatcher = dispatcher;
        _onDisconnected = onDisconnected;
        _logger = logger;

        Connected = true;
        ConnectedAt = DateTime.UtcNow;
        LastRecvAt = ConnectedAt;
        Nickname = string.Empty;
        RoomManager = roomManager;
        Redis = redis;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // 송신 큐 소비자를 함께 띄운다(AC-C2). 수신 루프와 독립 — 수신이 핸들러에서 대기해도 송신은 계속 나간다.
        var sendLoop = SendLoopAsync(ct);

        try
        {
            while (Connected && !ct.IsCancellationRequested)
            {
                byte[] lengthBytes = await ReceiveExactAsync(4, ct);
                int length = BitConverter.ToInt32(lengthBytes, 0);

                if (length <= 0 || length > 65536)
                {
                    _logger.LogWarning("Invalid packet length {Length} for session {SessionId}", length, SessionId);
                    break;
                }

                byte[] protobufData = await ReceiveExactAsync(length, ct);
                var packet = PacketSerializer.Deserialize(protobufData);
                if (packet is null)
                {
                    _logger.LogWarning("Failed to deserialize packet for session {SessionId}", SessionId);
                    continue;
                }

                LastRecvAt = DateTime.UtcNow;
                await _dispatcher.Dispatch(this, packet, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Session loop canceled for session {SessionId}", SessionId);
        }
        catch (SocketException e) when (IsExpectedDisconnect(e.SocketErrorCode))
        {
            _logger.LogInformation("Session {SessionId} disconnected by peer: {SocketErrorCode}", SessionId, e.SocketErrorCode);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Session loop failed for session {SessionId}", SessionId);
        }
        finally
        {
            // 수신 루프 종료 = 크래시/네트워크 끊김(C_PlayerLeave 없음) → graceful 퇴장.
            // 방에 다른 플레이어가 남아 있으면 재접속 유예 창 동안 PlayerState 보존(즉시 퇴장 확정 보류).
            RoomManager.LeaveRoom(this, graceful: true);
            Disconnect();

            // 큐를 닫아 SendLoop 을 깨우고 회수한다 — 안 하면 세션마다 Task 가 영원히 남는다(누수).
            // 소켓이 이미 닫혔으므로 남은 프레임의 write 실패는 정상이라 무시한다.
            _sendQueue.Writer.TryComplete();
            try { await sendLoop; } catch { /* 종료 경로 — 위에서 이미 로깅·처리됨 */ }

            _onDisconnected?.Invoke(SessionId);
        }
    }

    private async Task<byte[]> ReceiveExactAsync(int length, CancellationToken ct)
    {
        byte[] buffer = new byte[length];
        int offset = 0;

        while (offset < length)
        {
            int received = await Socket.ReceiveAsync(
                new ArraySegment<byte>(buffer, offset, length - offset),
                SocketFlags.None,
                ct);

            if (received == 0)
                throw new SocketException((int)SocketError.ConnectionReset);

            offset += received;
        }

        return buffer;
    }

    /// <summary>
    /// 패킷을 프레임으로 만들어 <b>송신 큐에 넣는다</b>(AC-C2). 실제 소켓 write 는 <see cref="SendLoopAsync"/> 가 한다.
    ///
    /// <para>⚠️ <b>의미가 바뀌었다</b>: 이 Task 의 완료는 "큐에 들어갔다"이지 <b>"전선에 나갔다"가 아니다.</b>
    /// 예전엔 await 가 실제 write 완료를 보장했다. 지금은 보장하지 않으므로
    /// <b>보내자마자 <see cref="Disconnect"/> 하면 그 패킷은 유실된다</b>(현재 그런 호출부는 없다 —
    /// Disconnect 는 하트비트 타임아웃·에러·종료 경로에서만 불린다).</para>
    ///
    /// <para><b>순서는 보존된다</b> — 한 스레드가 연달아 넣은 패킷은 넣은 순서대로 나간다(단일 소비자).
    /// 그래서 여러 패킷을 순차 await 하던 기존 핸들러(입장 시 로스터 전송 등)가 그대로 동작한다.</para>
    ///
    /// <para>이름을 <c>SendPacketAsync</c> 로 유지한 이유: 호출부 ~20곳을 건드리지 않기 위해서다
    /// (연결 계층 변경의 위험 표면을 줄인다). await 는 무해하다 — 완료된 Task 다.</para>
    /// </summary>
    public Task SendPacketAsync(Packet packet, CancellationToken ct = default)
    {
        if (!Connected) return Task.CompletedTask;

        byte[] frame;
        try
        {
            byte[] protobufData = PacketSerializer.Serialize(packet);
            int length = protobufData.Length;
            byte[] lengthBytes = BitConverter.GetBytes(length);

            frame = new byte[4 + length];
            Array.Copy(lengthBytes, 0, frame, 0, 4);
            Array.Copy(protobufData, 0, frame, 4, length);
        }
        catch (Exception e)
        {
            // 직렬화 실패는 이 패킷만의 문제 — 세션을 끊지 않는다(예전 코드는 여기서도 끊었다).
            _logger.LogError(e, "Packet serialize failed for session {SessionId}: {PacketType}", SessionId, packet.GetType().Name);
            return Task.CompletedTask;
        }

        // 틱 스레드가 생산자라 절대 블록시키지 않는다 → TryWrite. 실패 = 큐 포화 = 사실상 죽은 연결.
        if (!_sendQueue.Writer.TryWrite(frame))
        {
            _logger.LogWarning(
                "Send queue full ({Capacity}) for session {SessionId} — disconnecting (client cannot keep up)",
                SendQueueCapacity, SessionId);
            Disconnect();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 송신 큐의 <b>단일 소비자</b>. 한 프레임을 끝까지 쓴 뒤 다음 프레임을 쓴다 →
    /// 부분 전송이 일어나도 다른 프레임이 끼어들 수 없다(D1 근본 수정).
    /// </summary>
    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            while (await _sendQueue.Reader.WaitToReadAsync(ct))
            {
                while (_sendQueue.Reader.TryRead(out var frame))
                {
                    int offset = 0;
                    while (offset < frame.Length)
                    {
                        int sent = await Socket.SendAsync(
                            new ArraySegment<byte>(frame, offset, frame.Length - offset),
                            SocketFlags.None,
                            ct);

                        if (sent == 0)
                            throw new SocketException((int)SocketError.ConnectionReset);

                        offset += sent;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 서버 종료 — 정상 경로.
        }
        catch (SocketException e) when (IsExpectedDisconnect(e.SocketErrorCode))
        {
            _logger.LogInformation("Send loop stopped by disconnect for session {SessionId}: {SocketErrorCode}", SessionId, e.SocketErrorCode);
            Disconnect();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Send loop failed for session {SessionId}", SessionId);
            Disconnect();
        }
    }

    public void Disconnect()
    {
        if (!Connected) return;
        Connected = false;

        try
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Socket close failed for session {SessionId}", SessionId);
        }

        _logger.LogInformation("Session {SessionId} disconnected", SessionId);
    }

    private static bool IsExpectedDisconnect(SocketError socketError)
    {
        return socketError == SocketError.ConnectionReset
               || socketError == SocketError.ConnectionAborted
               || socketError == SocketError.OperationAborted
               || socketError == SocketError.Shutdown
               || socketError == SocketError.NotConnected
               || socketError == SocketError.Interrupted;
    }
}

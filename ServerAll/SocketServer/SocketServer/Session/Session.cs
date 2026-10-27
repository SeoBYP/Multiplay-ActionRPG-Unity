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

    // 넘치면 세션을 끊는다 — 방 틱 10Hz 라 1024 ≈ 100초치, 무한 큐는 느린 클라 하나가 메모리를 먹는다(DoS).
    public const int SendQueueCapacity = 1024;

    // 생산자 N(틱·패킷 스레드) → 소비자 1(SendLoop). 여러 스레드가 같은 소켓에 동시에 쓰면 프레임이 섞여 파싱이 깨진다.
    // 틱 스레드가 생산자라 TryWrite 로만 넣는다(블록 금지).
    private readonly Channel<byte[]> _sendQueue = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(SendQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
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
            // 크래시/끊김(C_PlayerLeave 없음) → graceful 퇴장(재접속 유예).
            RoomManager.LeaveRoom(this, graceful: true);
            Disconnect();

            // 큐를 닫아 SendLoop 을 회수한다 — 안 하면 세션마다 Task 누수.
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

    // 완료 = "큐에 들어갔다"이지 "전선에 나갔다"가 아니다 — 보내자마자 Disconnect 하면 유실.
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
            _logger.LogError(e, "Packet serialize failed for session {SessionId}: {PacketType}", SessionId, packet.GetType().Name);
            return Task.CompletedTask;
        }

        // 실패 = 큐 포화 = 사실상 죽은 연결.
        if (!_sendQueue.Writer.TryWrite(frame))
        {
            _logger.LogWarning(
                "Send queue full ({Capacity}) for session {SessionId} — disconnecting (client cannot keep up)",
                SendQueueCapacity, SessionId);
            Disconnect();
        }

        return Task.CompletedTask;
    }

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

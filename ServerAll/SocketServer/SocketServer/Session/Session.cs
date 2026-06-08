using System.Net.Sockets;
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

    public async Task SendPacketAsync(Packet packet, CancellationToken ct = default)
    {
        if (!Connected) return;

        try
        {
            byte[] protobufData = PacketSerializer.Serialize(packet);
            int length = protobufData.Length;
            byte[] lengthBytes = BitConverter.GetBytes(length);

            byte[] finalPacket = new byte[4 + length];
            Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
            Array.Copy(protobufData, 0, finalPacket, 4, length);

            int offset = 0;
            while (offset < finalPacket.Length)
            {
                int sent = await Socket.SendAsync(
                    new ArraySegment<byte>(finalPacket, offset, finalPacket.Length - offset),
                    SocketFlags.None,
                    ct);

                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                offset += sent;
            }
        }
        catch (Exception e)
        {
            if (e is SocketException socketException && IsExpectedDisconnect(socketException.SocketErrorCode))
            {
                _logger.LogInformation("SendPacketAsync canceled by disconnect for session {SessionId}: {SocketErrorCode}", SessionId, socketException.SocketErrorCode);
                Disconnect();
                return;
            }

            _logger.LogError(e, "SendPacketAsync failed for session {SessionId}", SessionId);
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

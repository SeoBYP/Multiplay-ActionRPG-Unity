using System.Net.Sockets;
using Google.Protobuf;
using Server.Packet;
using Server.Room;
using ServerCore.Protocol;


public sealed class Session
{
    public ulong SessionId { get; private set; }
    public bool Connected { get; private set; }
    public DateTime LastRecvAt { get; private set; }
    public DateTime ConnectedAt { get; }
    
    private Socket Socket;
    private Action<ulong> _onDisconnected;
    private RoomManager _roomManager;
    private PacketDispatcher _dispatcher;  // ✅ Dispatcher 추가


    public Session(
        ulong sessionId,
        Socket socket,
        RoomManager roomManager,
        PacketDispatcher dispatcher,  // ✅ Dispatcher 주입
        Action<ulong> onDisconnected = null)
    {
        SessionId = sessionId;
        Socket = socket;
        _roomManager = roomManager;
        _dispatcher = dispatcher;
        _onDisconnected = onDisconnected;

        Connected = true;
        ConnectedAt = DateTime.UtcNow;
        LastRecvAt = ConnectedAt;
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
                    Console.WriteLine($"[Session {SessionId}] Invalid packet length: {length}");
                    break;
                }

                byte[] protobufData = await ReceiveExactAsync(length, ct);
                var packet = ServerCore.Protocol.Packet.Parser.ParseFrom(protobufData);

                LastRecvAt = DateTime.UtcNow;

                // ✅ Dispatcher로 자동 라우팅
                await _dispatcher.Dispatch(this, packet, ct);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Session {SessionId}] Error: {e.Message}");
        }
        finally
        {
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
    
    /// <summary>
    /// 패킷 전송 (범용)
    /// </summary>
    public async Task SendPacketAsync(ServerCore.Protocol.Packet packet, CancellationToken ct = default)
    {
        if (!Connected) return;

        try
        {
            // Protobuf 직렬화
            byte[] protobufData = packet.ToByteArray();

            // Length 추가
            int length = protobufData.Length;
            byte[] lengthBytes = BitConverter.GetBytes(length);

            // 합치기
            byte[] finalPacket = new byte[4 + length];
            Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
            Array.Copy(protobufData, 0, finalPacket, 4, length);

            // 전송
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
            Console.WriteLine($"[Session {SessionId}] SendPacketAsync failed: {e.Message}");
            Disconnect();
        }
    }

    public Room? GetPlayerRoom()
    {
        return _roomManager.GetPlayerRoom(SessionId);
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
        catch(Exception e)
        {
            Console.WriteLine($"[Session {SessionId}] Socket Close Error: {e.Message}");
            throw;
        }

        Console.WriteLine($"[Session {SessionId}] Disconnected");
    }
}
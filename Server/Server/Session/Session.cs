using System.Net.Sockets;
using Google.Protobuf;
using Server.Room;
using ServerCore.Protocol;


public sealed class Session
{
    public ulong SessionId { get; private set; }
    public bool Connected { get; private set; }
    public DateTime LastRecvAt { get; private set; }
    public DateTime ConnectedAt { get; }

    private Socket Socket;
    private int bufferSize = 1024;
    private byte[] _recvBuffer;
    private byte[] _sendBuffer;
    private Action<ulong> _onDisconnected;
    private RoomManager _roomManager;

    public Session(
        ulong sessionId,
        Socket socket,
        RoomManager roomManager,
        int bufferSize = 1024,
        Action<ulong> onDisconnected = null)
    {
        SessionId = sessionId;
        Socket = socket;
        _roomManager = roomManager;

        this.bufferSize = bufferSize;
        _recvBuffer = new byte[bufferSize];
        _sendBuffer = new byte[bufferSize];

        Connected = true;
        ConnectedAt = DateTime.UtcNow;
        LastRecvAt = ConnectedAt;

        _onDisconnected = onDisconnected;
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

                var packet = Packet.Parser.ParseFrom(protobufData);

                LastRecvAt = DateTime.UtcNow;

                HandlePacket(packet, ct);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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

    private void HandlePacket(Packet packet, CancellationToken ct)
    {
        switch (packet.PayloadCase)
        {
            case Packet.PayloadOneofCase.CChat:
                HandleChatPacket(packet.CChat, ct);
                break;

            case Packet.PayloadOneofCase.CCreateRoom:
                HandleCreateRoom(packet.CCreateRoom, ct);
                break;

            case Packet.PayloadOneofCase.CJoinRoom:
                HandleJoinRoom(packet.CJoinRoom, ct);
                break;

            default:
                Console.WriteLine($"[Session {SessionId}] Unknown packet: {packet.PayloadCase}");
                break;
        }
    }


    /// <summary>
    /// 채팅 패킷 처리
    /// </summary>
    private void HandleChatPacket(C_Chat chatPacket, CancellationToken ct)
    {
        Console.WriteLine($"[Session {SessionId}] C_Chat: {chatPacket.Message}");

        var room = _roomManager.GetPlayerRoom(SessionId);

        if (room == null)
        {
            SendSystemMessage("You are not in any room");
            return;
        }

        // S_Chat 패킷 생성
        var packet = new Packet();
        packet.SChat = new S_Chat
        {
            SenderId = SessionId,
            Message = chatPacket.Message
        };

        // 방 내 브로드캐스트
        room.Broadcast(packet);
    }

    /// <summary>
    /// 방 생성 패킷 처리
    /// </summary>
    private void HandleCreateRoom(C_CreateRoom createRoom, CancellationToken ct)
    {
        Console.WriteLine($"[Session {SessionId}] C_CreateRoom: max={createRoom.MaxMembers}");

        var room = _roomManager.CreateRoom(createRoom.MaxMembers);

        if (room != null && _roomManager.JoinRoom(this, room.RoomId))
        {
            // 성공 응답
            var response = new Packet();
            response.SCreateRoom = new S_CreateRoom
            {
                Success = true,
                RoomId = room.RoomId
            };
            _ = SendPacketAsync(response, ct);
        }
        else
        {
            // 실패 응답
            var response = new Packet();
            response.SCreateRoom = new S_CreateRoom
            {
                Success = false,
                RoomId = 0
            };
            _ = SendPacketAsync(response, ct);
        }
    }

    /// <summary>
    /// 방 입장 패킷 처리
    /// </summary>
    private void HandleJoinRoom(C_JoinRoom joinRoom, CancellationToken ct)
    {
        Console.WriteLine($"[Session {SessionId}] C_JoinRoom: room={joinRoom.RoomId}");

        bool success = _roomManager.JoinRoom(this, joinRoom.RoomId);

        // 응답
        var response = new Packet();
        response.SJoinRoom = new S_JoinRoom
        {
            Success = success,
            RoomId = success ? joinRoom.RoomId : 0
        };
        _ = SendPacketAsync(response, ct);
    }

    /// <summary>
    /// 시스템 메시지 전송
    /// </summary>
    private void SendSystemMessage(string message)
    {
        var packet = new Packet();
        packet.SChat = new S_Chat
        {
            SenderId = 0, // 0 = 시스템
            Message = $"[System] {message}"
        };
        _ = SendPacketAsync(packet);
    }

    /// <summary>
    /// 패킷 전송 (범용)
    /// </summary>
    public async Task SendPacketAsync(Packet packet, CancellationToken ct = default)
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

            // 디버깅 로그
            Console.WriteLine($"[Session {SessionId}] Sending {finalPacket.Length} bytes (Length={length})");

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

    public void Disconnect()
    {
        if (!Connected) return;
        Connected = false;

        try
        {
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        try
        {
            Socket.Close();
        }
        catch
        {
        }

        Console.WriteLine($"[Session {SessionId}] Disconnected");
    }
}
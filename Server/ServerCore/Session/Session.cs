using System.Net.Sockets;
using Google.Protobuf;
using ServerCore.Protocol;

namespace ServerCore;

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
    private SessionManager _sessionManager; 
    
    public Session(
        ulong sessionId,
        Socket socket,
        SessionManager sessionManager, 
        int bufferSize = 1024,
        Action<ulong> onDisconnected = null)
    {
        SessionId = sessionId;
        Socket = socket;
        _sessionManager = sessionManager;
        
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

    private async void HandlePacket(Packet packet, CancellationToken ct)
    {
        switch (packet.PayloadCase)
        {
            case Packet.PayloadOneofCase.CChat:
                Console.WriteLine($"[Session {SessionId}] C_Chat: {packet.CChat.Message}");
                // 에코 응답
                _sessionManager.Broadcast(SessionId, packet, ct);
                break;
            case Packet.PayloadOneofCase.SChat:
                Console.WriteLine($"[Session {SessionId}] S_Chat received (unexpected)");
                break;
            default:
                Console.WriteLine($"[Session {SessionId}] Unknown packet type: {packet.PayloadCase}");
                break;
        }
    }
    
    /// <summary>
    /// 채팅 메시지 전송
    /// 
    /// 송신 구조: [4 bytes: Length][Protobuf Data]
    /// </summary>
    public async Task SendChatAsync(ulong senderId, string message, CancellationToken ct = default)
    {
        if (!Connected) return;

        try
        {
            // 1️⃣ Packet 생성 및 S_Chat 설정
            var packet = new Packet();
            packet.SChat = new S_Chat 
            { 
                SenderId = senderId,
                Message = message 
            };
            
            // 2️⃣ Protobuf 직렬화
            byte[] protobufData = packet.ToByteArray();
            
            // 3️⃣ Length 계산 및 직렬화
            int length = protobufData.Length;
            byte[] lengthBytes = BitConverter.GetBytes(length);
            
            // 4️⃣ [Length][Protobuf] 합치기
            byte[] finalPacket = new byte[4 + length];
            Array.Copy(lengthBytes, 0, finalPacket, 0, 4);
            Array.Copy(protobufData, 0, finalPacket, 4, length);
            
            // 🔍 디버깅 로그
            Console.WriteLine($"[Session {SessionId}] Sending {finalPacket.Length} bytes (Length={length})");
            
            // 5️⃣ 전송
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

            Console.WriteLine($"[Session {SessionId}] Sent: {message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Session {SessionId}] SendAsync failed: {e.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        if (!Connected) return;
        Connected = false;

        try { Socket.Shutdown(SocketShutdown.Both); } catch { }
        try { Socket.Close(); } catch { }
    }
}

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

    public Session(
        ulong sessionId,
        Socket socket,
        int bufferSize = 1024,
        Action<ulong> onDisconnected = null)
    {
        SessionId = sessionId;
        Socket = socket;

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
                int received = await Socket.ReceiveAsync(
                    new ArraySegment<byte>(_recvBuffer),
                    SocketFlags.None,
                    ct);

                if (received == 0)
                    break; // EOF

                LastRecvAt = DateTime.UtcNow;
                var msg = C_Chat.Parser.ParseFrom(_recvBuffer, 0, received);
                Console.WriteLine($"Received {msg.Message} from client");
                await SendAsync("[Server]" + msg.Message, ct);
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

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        if (!Connected) return;

        try
        {
            var msg = new C_Chat();
            msg.Message = message;
            var sendBytes = msg.ToByteArray();
            
            Console.WriteLine($"[SendAsync] Sending {sendBytes.Length} bytes: {BitConverter.ToString(sendBytes)}");

            int offset = 0;
            while (offset < sendBytes.Length)
            {
                int sent = await Socket.SendAsync(
                    new ArraySegment<byte>(sendBytes, offset, sendBytes.Length - offset),
                    SocketFlags.None,
                    ct);

                if (sent == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                
                offset += sent;
            }

            Console.WriteLine($"[SendAsync] msg={message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[SendAsync] failed: {e}");
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

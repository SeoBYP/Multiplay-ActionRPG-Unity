using System.Net.Sockets;

namespace ServerCore;

public sealed class Session
{
    public long SessionId { get; private set; }
    public long? PlayerId { get; private set; }
    
    public Socket Socket { get; private set; }
    public bool Connected { get; private set; }
    
    public byte[] RecvBuffer { get; } 
    public byte[] SendBuffer { get; }
    
    public DateTime ConnectedAt { get; }
    public DateTime LastRecvAt { get; private set; }

    private readonly Action<long> _onDisconnected;
    
    public Session(long sessionId, Socket socket, int bufferSize = 1024)
    {
        SessionId = sessionId;
        Socket = socket;
        
        RecvBuffer = new byte[bufferSize];
        SendBuffer = new byte[bufferSize];
        
        Connected = true;
        ConnectedAt = DateTime.Now;
        LastRecvAt = ConnectedAt;
    }
    
    public void BindPlayer(long playerId)
    {
        PlayerId = playerId;
    }
    
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && Connected)
            {
                // Socket.ReceiveAsync(Task) 사용 (버전에 따라 시그니처 다를 수 있음)
                int received = await Socket.ReceiveAsync(RecvBuffer, SocketFlags.None, ct);

                if (received == 0)
                {
                    // 상대 종료
                    break;
                }

                // 지금은 “연결/수신 루프” 단계라 데이터는 그냥 버려도 됨
                // 다음 단계에서 프레이밍/패킷 파서로 전달
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        catch (Exception) { }
        finally
        {
            Disconnect();
            _onDisconnected(SessionId);
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
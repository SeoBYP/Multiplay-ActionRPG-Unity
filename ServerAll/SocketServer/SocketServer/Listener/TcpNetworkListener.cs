using System.Net;
using System.Net.Sockets;
using ServerCore;

namespace Server
{
    public sealed class TcpNetworkListener : INetworkListener, IDisposable
    {
        private readonly IPEndPoint _endpoint;
        private Socket? _listenSocket;
        private volatile int _running;

        private readonly SessionManager _sessionManager;

        private CancellationTokenSource? _cts;
        private Task? _acceptLoopTask;

        public TcpNetworkListener(string ipAddress, int port, SessionManager sessionManager)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IpAddress cannot be null or whitespace", nameof(ipAddress));

            _endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            _sessionManager = sessionManager;
        }

        public void Start(int backlog = 512)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return; 

            _cts = new CancellationTokenSource();

            _listenSocket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.NoDelay = true;
            _listenSocket.Bind(_endpoint);
            _listenSocket.Listen(backlog);
            
            _acceptLoopTask = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _running, 0) == 0)
                return;
            
            try
            {
                _cts?.Cancel();
                
                _listenSocket?.Close();
                _listenSocket?.Dispose(); 
                
                _cts?.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Stop] Exception: {e}");
                
            }
            
            _listenSocket = null;
            _cts = null;
            _acceptLoopTask = null;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            var listen = _listenSocket;
            if (listen is null) return;

            while (Volatile.Read(ref _running) == 1 && !ct.IsCancellationRequested)
            {
                Socket? client = null;

                try
                {
                    client = await listen.AcceptAsync(ct);

                    client.NoDelay = true;

                    Console.WriteLine("Connected : " + client.RemoteEndPoint);
                    _sessionManager.CreateSession(client, ct);
                }
                catch (ObjectDisposedException)
                {
                    client?.Dispose();
                    break;
                }
                catch (SocketException e)
                {
                    client?.Dispose();

                    if (Volatile.Read(ref _running) == 0 || ct.IsCancellationRequested)
                        break;

                    Console.WriteLine($"[AcceptLoop] SocketException: {e.SocketErrorCode} / {e.Message}");
                }
                catch (Exception e)
                {
                    client?.Dispose();
                    if (Volatile.Read(ref _running) == 0 || ct.IsCancellationRequested)
                        break;
                    Console.WriteLine($"[AcceptLoop] Exception: {e}");
                }
            }
        }

        public void Dispose() => Stop();
    }
}

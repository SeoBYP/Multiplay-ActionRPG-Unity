using System.Net;
using System.Net.Sockets;
using Server;

namespace Server
{
    public class TcpNetworkListener : INetworkListener, IDisposable
    {
        private const int DefaultBacklog = 512;

        private readonly IPEndPoint _endpoint;
        private Socket? _socket;
        private bool _active;

        public Socket ServerSocket => _socket ?? throw new ObjectDisposedException(nameof(TcpNetworkListener));
        public bool Active => _active;

        public TcpNetworkListener(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
            _socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public TcpNetworkListener(int port)
        {
            _endpoint = new IPEndPoint(IPAddress.Any, port);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public TcpNetworkListener(string ipAddress, int port)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IpAddress cannot be null or whitespace", nameof(ipAddress));

            _endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start() => Start(DefaultBacklog);

        public void Start(int backlog)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backlog);

            if (_active) return;
            if (_socket is null) throw new ObjectDisposedException(nameof(TcpNetworkListener));

            try
            {
                _socket.Bind(_endpoint);
                _socket.Listen(backlog); // backlog 반영 (중요)
                _active = true;
            }
            catch
            {
                Stop(); // 상태 정리
                throw;
            }
        }

        public void Stop()
        {
            if (!_active && _socket is null) return;

            _active = false;

            try
            {
                _socket?.Close();
            }
            catch
            {
            }

            try
            {
                _socket?.Dispose();
            }
            catch
            {
            }

            _socket = null; // 1회용 정책: Stop 후 재시작 불가
        }

        public Socket AcceptSocket()
        {
            if (!_active) throw new InvalidOperationException("Listener is not active.");
            if (_socket is null) throw new ObjectDisposedException(nameof(TcpNetworkListener));

            return _socket.Accept();
        }

        public void Dispose() => Stop();
    }
}
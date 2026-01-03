using ServerCore;
using ServerCore.Factory;

namespace Server;

public sealed class TcpAcceptLoop
{
    private readonly TcpNetworkListener _listener;
    private readonly ISessionFactory _factory;
    private readonly SessionManager _manager;

    public TcpAcceptLoop(TcpNetworkListener listener, ISessionFactory factory, SessionManager manager)
    {
        _listener = listener;
        _factory = factory;
        _manager = manager;
    }

    public void Run(CancellationToken ct)
    {
        _listener.Start();

        while (!ct.IsCancellationRequested)
        {
            var client = _listener.AcceptSocket();     // 블로킹
            var session = _factory.Create(client);

            if (_manager.Add(session))
                _manager.Start(session, ct);
            else
                client.Close();
        }
    }
}

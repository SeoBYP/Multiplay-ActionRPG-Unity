using System.Net.Sockets;

namespace ServerCore.Factory;

public interface ISessionFactory
{
    Session Create(Socket socket);
}

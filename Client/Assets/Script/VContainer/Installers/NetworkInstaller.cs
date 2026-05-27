using Game.Network.Https;
using Game.Network.Socket;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 네트워크 클라이언트 등록.
    /// gRPC 채널(GameApiClient) + TCP 소켓(SocketApiClient)
    /// </summary>
    public sealed class NetworkInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            new GameApiClient().Install(builder);
            new SocketApiClient().Install(builder);
        }
    }
}

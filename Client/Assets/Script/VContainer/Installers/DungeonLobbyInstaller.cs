using Game.System.DungeonLobby;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 던전 로비 서비스 등록.
    /// DungeonLobbySession + IDungeonLobbyService
    /// </summary>
    public sealed class DungeonLobbyInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<DungeonLobbySession>(Lifetime.Singleton);
            builder.Register<IDungeonLobbyService, DungeonLobbyService>(Lifetime.Singleton);
        }
    }
}

using Game.System.InGame;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 게임 세션 서비스 등록.
    /// GameSessionConnector — OnGameSessionReady 이벤트 구독 후 TCP 연결 → Dungeon 씬 로드 주관.
    /// </summary>
    public sealed class GameSessionInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<GameSessionConnector>(Lifetime.Singleton);
        }
    }
}

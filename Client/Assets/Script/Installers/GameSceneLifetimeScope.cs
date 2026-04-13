using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 인게임 씬 범위 — ProjectLifetimeScope의 자식 스코프
    /// 캐릭터, PlayerSpawner 등 씬에 종속된 객체를 여기에 등록
    /// </summary>
    public class GameSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // TODO: PlayerSpawner, RemotePlayerController 등 추가 예정
        }
    }
}

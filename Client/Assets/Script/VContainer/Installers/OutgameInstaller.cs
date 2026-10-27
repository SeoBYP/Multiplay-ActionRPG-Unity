using Game.GUI.OutGame;
using Game.Gameplay.Input;
using Game.Presentation.DungeonLobby;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    public class OutgameInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // 라우터는 던전과 공용이라 InputInstaller 로 분리.
            builder.Install(new InputInstaller());

            // 범위 내 IInteractable 추적 + Interact 처리
            builder.RegisterEntryPoint<InteractionSystem>(Lifetime.Scoped);

            // ── Lobby MVI ─────────────────────────────
            builder.Register<LobbyRepository>(Lifetime.Scoped);
            builder.RegisterEntryPoint<LobbyModel>(Lifetime.Scoped).AsSelf();

            // ── UI 컨트롤러 ────────────────────────────
            // POCO — MonoBehaviour 아님. IInputHandler로 L키 처리.
            builder.RegisterEntryPoint<LobbyViewController>(Lifetime.Scoped);
        }
    }
}
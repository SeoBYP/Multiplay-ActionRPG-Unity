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
            // ── Input 시스템 ──────────────────────────
            // InputRouter: New Input System 콜백 기반, Initialize/Dispose로 수명 관리
            builder.RegisterEntryPoint<InputRouter>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            // InteractionSystem: F키 처리, 범위 내 IInteractable 추적
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
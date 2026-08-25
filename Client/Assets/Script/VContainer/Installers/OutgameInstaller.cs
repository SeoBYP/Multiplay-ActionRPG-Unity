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
            // 라우터 자체는 던전과 공용이라 InputInstaller 로 분리했다(F13).
            builder.Install(new InputInstaller());

            // InteractionSystem: F키 처리, 범위 내 IInteractable 추적
            builder.RegisterEntryPoint<InteractionSystem>(Lifetime.Scoped);

            // InputContext(UI 점유 시 Player 맵 토글)는 루트(ProjectLifetimeScope) 전역 Singleton으로 이동.
            // PlayerInputActions가 루트 Singleton이라 InputContext도 같은 인스턴스를 공유 → 전 씬에서 동작.

            // ── Lobby MVI ─────────────────────────────
            builder.Register<LobbyRepository>(Lifetime.Scoped);
            builder.RegisterEntryPoint<LobbyModel>(Lifetime.Scoped).AsSelf();

            // ── UI 컨트롤러 ────────────────────────────
            // POCO — MonoBehaviour 아님. IInputHandler로 L키 처리.
            builder.RegisterEntryPoint<LobbyViewController>(Lifetime.Scoped);
        }
    }
}
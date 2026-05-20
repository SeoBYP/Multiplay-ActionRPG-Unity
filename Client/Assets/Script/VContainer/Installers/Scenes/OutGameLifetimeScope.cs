using Game.GUI.OutGame;
using Game.Input;
using Game.OutGame.DungeonLobby;
using Game.Installers.Scenes.Startup;
using Script.System.Auth;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    /// <summary>
    /// OutGame 씬 전용 DI 스코프.
    /// ProjectLifetimeScope(루트)의 자식으로 동작하므로
    /// IDungeonLobbyService 등 전역 서비스를 그대로 주입받는다.
    /// </summary>
    public class OutGameLifetimeScope : LifetimeScope
    {
        [SerializeField] private Canvas uiCanvas;

        protected override void Configure(IContainerBuilder builder)
        {
            // ── UI Canvas 등록 ────────────────────────
            // LobbyViewController가 Addressable 프리팹을 Canvas 하위에 생성한다.
            builder.RegisterInstance(uiCanvas);

            // ── Input 시스템 ──────────────────────────
            // PlayerInputActions: InputActionAsset 래퍼. InputRouter 에 주입된다.
            builder.Register<PlayerInputActions>(Lifetime.Scoped);
            // InputRouter: New Input System 콜백 기반, Initialize/Dispose로 수명 관리
            builder.RegisterEntryPoint<InputRouter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

            // InteractionSystem: F키 처리, 범위 내 IInteractable 추적
            builder.RegisterEntryPoint<InteractionSystem>(Lifetime.Scoped);

            // ── Lobby MVI ─────────────────────────────
            builder.Register<LobbyRepository>(Lifetime.Scoped);
            builder.RegisterEntryPoint<LobbyModel>(Lifetime.Scoped).AsSelf();

            // ── UI 컨트롤러 ────────────────────────────
            // POCO — MonoBehaviour 아님. IInputHandler로 L키 처리.
            builder.RegisterEntryPoint<LobbyViewController>(Lifetime.Scoped);

            builder.RegisterEntryPoint<OutGameSceneStartup>(Lifetime.Scoped);
        }
    }
}

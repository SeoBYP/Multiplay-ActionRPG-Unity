using Game.Network.Https;
using Script.System.Auth;
using Script.System.Startup;
using Game.System.DungeonLobby;
#if UNITY_EDITOR
using Game.System.AuthSystem;
#endif
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 타이틀/인게임 씬이 공통으로 사용하는 전역 서비스 등록 스코프.
    /// 네트워크 클라이언트와 인증 상태처럼 씬을 넘나드는 싱글톤을 여기서 구성한다.
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
        /// <summary>
        /// 루트 스코프에 공통 네트워크 서비스와 인증 서비스를 등록한다.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            // gRPC 채널과 각 API 서비스 등록.
            new GameApiClient().Install(builder);

            // 로그인 후 처리할 시작 인텐트 큐 및 유저 프로필 (AuthService보다 먼저 등록).
            builder.Register<UserProfile>(Lifetime.Singleton);
            builder.Register<StartupIntentQueue>(Lifetime.Singleton);

            // 런타임 인증 상태와 인증 오케스트레이션 서비스 등록.
            builder.Register<AuthSession>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);

            // 던전 로비 런타임 상태와 로비 오케스트레이션 서비스 등록.
            builder.Register<DungeonLobbySession>(Lifetime.Singleton);
            builder.Register<IDungeonLobbyService, DungeonLobbyService>(Lifetime.Singleton);

#if UNITY_EDITOR
            // 에디터에서 Title 씬 없이 직접 실행 시 게스트 자동 로그인.
            // 전역(ProjectLifetimeScope)에 1회만 등록 — 실패 시 Play 모드 즉시 종료.
            builder.RegisterEntryPoint<EditorAutoLoginInitializer>(Lifetime.Singleton);
#endif
        }
    }
}

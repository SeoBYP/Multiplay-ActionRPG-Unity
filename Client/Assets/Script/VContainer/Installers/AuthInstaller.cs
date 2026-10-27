using Game.System.Auth;
using Script.System.Startup;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR
using Game.System.Auth;
#endif

namespace Game.Installers
{
    /// <summary>
    /// 인증 관련 서비스 등록. 에디터에서는 EditorAutoLoginInitializer 추가 등록.
    /// </summary>
    public sealed class AuthInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<UserProfile>(Lifetime.Singleton);
            builder.Register<StartupIntentQueue>(Lifetime.Singleton);
            builder.Register<ITokenStore, PlayerPrefsTokenStore>(Lifetime.Singleton);
            builder.Register<AuthSession>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);

            // 액세스 토큰 만료 전에 스스로 갱신 — 없으면 토큰 수명을 넘긴 플레이에서 전 RPC 가 Unauthenticated 로 죽고,
            // 서버에서는 조용한 클라가 되어 유령 방 리퍼의 오탐 대상이 된다.
            builder.RegisterEntryPoint<SessionKeepAlive>(Lifetime.Singleton);

#if UNITY_EDITOR
            // 에디터에서 Title 씬 없이 직접 실행 시 게스트 자동 로그인.
            builder.RegisterEntryPoint<EditorAutoLoginInitializer>(Lifetime.Singleton);
#endif
        }
    }
}

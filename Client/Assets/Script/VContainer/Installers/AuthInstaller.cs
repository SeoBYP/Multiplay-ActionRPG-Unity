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
    /// 인증 관련 서비스 등록.
    /// AuthSession, IAuthService, UserProfile, StartupIntentQueue
    /// 에디터 환경에서는 EditorAutoLoginInitializer 추가 등록.
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

            // 액세스 토큰이 만료되기 전에 스스로 갱신한다.
            // 콜드스타트에만 갱신하던 탓에 토큰 수명을 넘겨 플레이하면 전 RPC 가 Unauthenticated 로 죽었고,
            // 서버 입장에서도 조용한 클라가 되어 유령 방 리퍼의 오탐 대상이 됐다.
            builder.RegisterEntryPoint<SessionKeepAlive>(Lifetime.Singleton);

#if UNITY_EDITOR
            // 에디터에서 Title 씬 없이 직접 실행 시 게스트 자동 로그인.
            builder.RegisterEntryPoint<EditorAutoLoginInitializer>(Lifetime.Singleton);
#endif
        }
    }
}

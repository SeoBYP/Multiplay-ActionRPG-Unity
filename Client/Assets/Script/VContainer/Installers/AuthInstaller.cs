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
            builder.Register<AuthSession>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);

#if UNITY_EDITOR
            // 에디터에서 Title 씬 없이 직접 실행 시 게스트 자동 로그인.
            builder.RegisterEntryPoint<EditorAutoLoginInitializer>(Lifetime.Singleton);
#endif
        }
    }
}

using Game.Network.Https;
using Script.System.Auth;
using UnityEngine;
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

            // 런타임 인증 상태와 인증 오케스트레이션 서비스 등록.
            builder.Register<AuthSession>(Lifetime.Singleton);
            builder.Register<IAuthService, AuthService>(Lifetime.Singleton);
        }
    }
}

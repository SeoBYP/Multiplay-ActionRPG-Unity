using Game.Gameplay.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 전역(씬 공통) DI 스코프.
    ///
    /// 각 도메인의 등록 책임은 전용 Installer에 위임한다.
    /// 이 파일은 Installer 조합만 담당한다.
    ///
    /// ┌─────────────────────────────────────┐
    /// │  NetworkInstaller     — gRPC + TCP  │
    /// │  AuthInstaller        — 인증 서비스  │
    /// │  DungeonLobbyInstaller — 로비 서비스 │
    /// │  GameSessionInstaller — 세션 서비스  │
    /// └─────────────────────────────────────┘
    /// </summary>
    public class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Install(new NetworkInstaller());
            builder.Install(new AuthInstaller());
            builder.Install(new DungeonLobbyInstaller());
            builder.Install(new GameSessionInstaller());
            
            builder.Register<PlayerInputActions>(Lifetime.Scoped);
        }
    }
}

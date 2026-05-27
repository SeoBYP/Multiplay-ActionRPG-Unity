using Game.OutGame.Title;
using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    /// <summary>
    /// 타이틀 씬 전용 DI 스코프.
    ///
    /// TitleModel이 IAsyncStartable을 구현하므로 VContainer가 자동으로
    /// StartAsync()를 호출한다 — 자동 로그인 완료 대기 및 상태 갱신.
    ///
    /// LoginWindow / MainMenuWindow 는 TitleModel만 주입받는다.
    /// IAuthService를 직접 알지 않는다.
    /// </summary>
    public class TitleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.RegisterEntryPoint<TitleModel>(Lifetime.Scoped).AsSelf();
        }
    }
}

using Game.Gameplay.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers
{
    /// <summary>
    /// 입력 라우팅 등록. **Main·Dungeon 양쪽**이 설치한다.
    ///
    /// GameHud 는 두 씬 모두에서 살아 있는데(원칙 3), 라우터가 Main 에만 있으면
    /// 던전에서 창 토글 키가 죽는다. 그래서 로비 MVI 를 담은 OutgameInstaller 에서 떼어냈다.
    /// </summary>
    public class InputInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // InputRouter: New Input System 콜백 기반, Initialize/Dispose로 수명 관리(구독 해제 포함).
            builder.RegisterEntryPoint<InputRouter>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();
        }
    }
}

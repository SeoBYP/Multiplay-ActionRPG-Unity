using VContainer;

namespace Game.Installers
{
    /// <summary>
    /// 도메인별 DI 등록 단위. LifetimeScope 는 Install 호출만 하고 등록 로직은 구현체가 갖는다.
    /// </summary>
    public interface IInstaller
    {
        void Install(IContainerBuilder builder);
    }
}

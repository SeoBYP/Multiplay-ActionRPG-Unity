using VContainer;

namespace Game.Installers
{
    /// <summary>
    /// 도메인별 DI 등록 단위.
    /// ProjectLifetimeScope는 Install 호출만 담당하고,
    /// 실제 등록 로직은 각 구현체에 위임한다.
    /// </summary>
    public interface IInstaller
    {
        void Install(IContainerBuilder builder);
    }
}

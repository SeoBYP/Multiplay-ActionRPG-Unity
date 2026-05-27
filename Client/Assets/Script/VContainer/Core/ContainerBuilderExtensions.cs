using VContainer;

namespace Game.Installers
{
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// IInstaller를 실행해 builder에 서비스를 등록한다.
        /// ProjectLifetimeScope에서 builder.Install(new XxxInstaller()) 형태로 사용한다.
        /// </summary>
        public static void Install(this IContainerBuilder builder, IInstaller installer)
        {
            installer.Install(builder);
        }
    }
}

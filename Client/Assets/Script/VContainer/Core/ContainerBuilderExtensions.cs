using VContainer;

namespace Game.Installers
{
    public static class ContainerBuilderExtensions
    {
        public static void Install(this IContainerBuilder builder, IInstaller installer)
        {
            installer.Install(builder);
        }
    }
}

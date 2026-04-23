using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    public class TitleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            
            builder.RegisterEntryPoint<TitleSceneInitializer>();
        }
    }
}
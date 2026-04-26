using Game.Main.Character;
using Game.Main.Character.Input;
using Game.Main.Input;
using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    public class MainLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<PlayerInputActions>(Lifetime.Scoped)
                .AsSelf();
                
            builder.RegisterInstance(new LocomotionSettings());
            builder.Register<ILocomotionStateFactory, LocomotionStateFactory>(Lifetime.Scoped);
            
            builder.RegisterEntryPoint<MainSceneInitializer>();
        }
    }
}
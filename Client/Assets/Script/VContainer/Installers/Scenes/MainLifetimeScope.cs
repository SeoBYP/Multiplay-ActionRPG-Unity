using Game.Main.Character;
using Game.Input;
using Game.GUI.OutGame;
using Game.Installers.Scenes.Startup;
using Game.OutGame.DungeonLobby;
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
            builder.RegisterEntryPoint<InputRouter>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<InteractionSystem>(Lifetime.Scoped);

            builder.Register<LobbyRepository>(Lifetime.Scoped);
            builder.RegisterEntryPoint<LobbyModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<LobbyViewController>(Lifetime.Scoped);

            builder.RegisterInstance(new LocomotionSettings());
            builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
            builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

            builder.RegisterEntryPoint<MainSceneInitializer>();
            builder.RegisterEntryPoint<MainSceneStartup>(Lifetime.Scoped);
        }
    }
}

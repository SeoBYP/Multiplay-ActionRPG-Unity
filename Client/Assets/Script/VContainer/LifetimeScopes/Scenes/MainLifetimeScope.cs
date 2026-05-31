using Game.Gameplay.Character;
using Game.Gameplay.Input;
using Game.Installers.Scenes.Startup;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    
    public class MainLifetimeScope : LifetimeScope
    {
        [SerializeField] private Canvas uiCanvas;

        
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            // ── UI Canvas 등록 ────────────────────────
            // LobbyViewController가 Addressable 프리팹을 Canvas 하위에 생성한다.
            builder.RegisterInstance(uiCanvas);

            builder.Register<PlayerInputActions>(Lifetime.Scoped)
                .AsSelf();

            builder.RegisterInstance(new LocomotionSettings());
            builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
            builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

            builder.Install(new OutgameInstaller());

            builder.RegisterEntryPoint<MainSceneInitializer>(Lifetime.Scoped);
            builder.RegisterEntryPoint<MainSceneStartup>(Lifetime.Scoped);
        }
    }
}

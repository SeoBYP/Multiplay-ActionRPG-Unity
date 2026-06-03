using Game.Gameplay.Character;
using Game.Gameplay.Input;
using Game.Gameplay.Spawn;
using Game.Installers.Scenes.Startup;
using Game.System.Player;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Installers.Scenes
{
    public class MainLifetimeScope : LifetimeScope
    {
        [SerializeField] private Canvas uiCanvas;
        [SerializeField] private GameObject localPlayerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            // LobbyViewController가 Addressable 프리팹을 Canvas 하위에 생성한다.
            builder.RegisterInstance(uiCanvas);

            builder.Register<PlayerInputActions>(Lifetime.Scoped).AsSelf();

            builder.RegisterInstance(new LocomotionSettings());
            builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
            builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

            // CharacterSpawner 의존성 — Dungeon 스코프와 동일하게 등록해야 생성에 성공한다.
            // Main 씬은 네트워크 미연결(SocketState != Joined)이라 SpawnLayoutProvider는
            // 생성자 충족용으로만 필요하고 런타임에 Get()이 호출되지는 않는다.
            builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();
            builder.Register<SpawnLayoutProvider>(Lifetime.Scoped).AsSelf();

            // Main 씬은 로컬 플레이어만 스폰. RemotePlayerPrefab 없음.
            builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab));
            builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);

            builder.Install(new OutgameInstaller());

            builder.RegisterEntryPoint<MainSceneInitializer>(Lifetime.Scoped);
            builder.RegisterEntryPoint<MainSceneStartup>(Lifetime.Scoped);
        }
    }
}

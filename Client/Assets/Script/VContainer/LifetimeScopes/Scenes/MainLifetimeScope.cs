using Game.Gameplay.Character;
using Game.Gameplay.Input;
using Game.Gameplay.Spawn;
using Game.GUI.OutGame;
using Game.Installers.Scenes.Startup;
using Game.Presentation.InGame;
using Game.Presentation.Inventory;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
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

            // PlayerInputActions는 루트(ProjectLifetimeScope)의 전역 Singleton을 공유한다 — 여기서 재등록하지 않는다.
            builder.RegisterInstance(new LocomotionSettings());
            builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
            builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

            // CharacterSpawner 의존성 — Dungeon 스코프와 동일하게 등록해야 생성에 성공한다.
            // Main 씬은 네트워크 미연결(SocketState != Joined)이라 SpawnLayoutProvider는
            // 생성자 충족용으로만 필요하고 런타임에 Get()이 호출되지는 않는다.
            builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();
            builder.Register<SpawnLayoutProvider>(Lifetime.Scoped).AsSelf();

            // GameHud(HP/MP/버프)를 Main 씬에서도 표시. 던전 구성과 동일하되,
            // EffectReceiver(서버 권위 수신)는 던전 전용이라 제외(Main은 미연결).
            // EffectIconCatalog는 Resources 기본본 폴백(인스펙터 할당 불요).
            builder.Register<GameplayEffectCatalog>(Lifetime.Scoped).AsSelf();
            builder.RegisterInstance(Resources.Load<EffectIconCatalog>("Effects/EffectIconCatalog")
                                     ?? ScriptableObject.CreateInstance<EffectIconCatalog>());
            builder.Register<InGameModel>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<GameHudController>(Lifetime.Scoped);

            // 인벤토리 MVI — Main(로비)에서도 인벤토리 창 사용(I키·HUD 버튼). 던전 구성과 동일.
            // ItemDisplayCatalog는 Resources 기본본 폴백(인스펙터 할당 불요).
            builder.RegisterInstance(Resources.Load<ItemDisplayCatalog>("ItemDisplayCatalog")
                                     ?? ScriptableObject.CreateInstance<ItemDisplayCatalog>());
            builder.Register<InventoryModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<InventoryViewController>(Lifetime.Scoped);

            // Main 씬은 로컬 플레이어만 스폰. RemotePlayerPrefab 없음.
            builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab));
            builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);

            builder.Install(new OutgameInstaller());

            // 입력 맵 활성화는 루트 GlobalInputInitializer가 전역 1회 담당(씬별 초기화 제거).
            builder.RegisterEntryPoint<MainSceneStartup>(Lifetime.Scoped);
        }
    }
}

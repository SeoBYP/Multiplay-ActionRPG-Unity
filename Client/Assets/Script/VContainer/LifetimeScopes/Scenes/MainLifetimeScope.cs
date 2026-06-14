using Game.Gameplay.Character;
using Game.Gameplay.Input;
using Game.Gameplay.Spawn;
using Game.GUI.OutGame;
using Game.Installers.Scenes.Startup;
using Game.Presentation.InGame;
using Game.Presentation.Inventory;
using Game.Presentation.Progression;
using Game.System.Player;
using Game.System.Progression;
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

        [Header("Main 로컬 몬스터(B-lite) — 콜라이더+LocalMonster 프리팹. 스폰 위치/슬롯은 spawn-layouts 의 mainMapId 맵에서")]
        [SerializeField] private GameObject localMonsterPrefab;
        [SerializeField] private string mainMapId = "main_field_01";

        [Header("Main 전리품 오브 — LocalGroundItem 프리팹(GroundItem.prefab 구성 + LocalGroundItem). 줍기→ClaimKill")]
        [SerializeField] private GameObject localGroundItemPrefab;

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

            // 진행/스탯창(7.3) MVI — 서버 권위 pull(GetProgression). View(스탯창)는 Model만 주입받는다.
            builder.Register<ProgressionModel>(Lifetime.Scoped).AsSelf();

            // Main 클라 스탯 홀더 — GetProgression 캐시(로그인 시 StartAsync, 킬 시 MainMonsterSpawner 가 Refresh).
            // LocalCombat(데미지=AttackPower)·킬 후 레벨/Exp 로그가 동기 읽기. 진실원=서버. 던전 미등록(서버 권위).
            builder.RegisterEntryPoint<PlayerProgressionHolder>(Lifetime.Scoped).AsSelf();

            // 소모품(3.8) — 효과 데이터(클라 SO) + Side Effect 핸들러(OnConsumableUsed→GAS). 미존재 시 빈 SO 폴백.
            builder.RegisterInstance(Resources.Load<ConsumableCatalog>("ConsumableCatalog")
                                     ?? ScriptableObject.CreateInstance<ConsumableCatalog>());
            // 소모품 회복 effect 를 GameplayEffectCatalog 에 등록(SO→카탈로그, 교리 gas-architecture §2.5).
            builder.RegisterEntryPoint<ConsumableCatalogSeeder>(Lifetime.Scoped);
            builder.RegisterEntryPoint<ConsumableEffectHandler>(Lifetime.Scoped);

            // Main 씬은 로컬 플레이어만 스폰. RemotePlayerPrefab 없음.
            builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab));
            builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);

            // Main 로컬 몬스터(B-lite) — 슬롯 기반 클라 스폰·렌더. 드랍 roll·정원·쿨다운은 서버(ClaimKill).
            // 스폰 위치/슬롯은 SpawnLayoutProvider(spawn-layouts.json mainMapId 맵)에서 읽는다. 프리팹 미할당이면 무해.
            builder.RegisterInstance(new MainMonsterSettings(localMonsterPrefab, mainMapId, localGroundItemPrefab));
            builder.RegisterEntryPoint<MainMonsterSpawner>(Lifetime.Scoped);

            // Main 타이머 리스폰(2.5.1) — Main 전용. 던전은 미등록 → 다운잠금 유지(의도된 비대칭).
            builder.RegisterEntryPoint<LocalRespawnController>(Lifetime.Scoped);

            builder.Install(new OutgameInstaller());

            // 입력 맵 활성화는 루트 GlobalInputInitializer가 전역 1회 담당(씬별 초기화 제거).
            builder.RegisterEntryPoint<MainSceneStartup>(Lifetime.Scoped);
        }
    }
}

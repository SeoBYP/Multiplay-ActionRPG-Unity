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
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;
using Game.GUI;

namespace Game.Installers.Scenes
{
    public class MainLifetimeScope : LifetimeScope
    {
        // 게임 데이터 SO를 Addressables(로컬 번들)에서 동기 로드. Resources 폐기 — 빌드 항상포함 회피.
        // 씬 수명 카탈로그라 핸들 의도적 보존. 미등록 주소면 null → 호출부가 빈 SO 폴백.
        private static T LoadData<T>(string address) where T : Object
            => Addressables.LoadAssetAsync<T>(address).WaitForCompletion();

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
            // EffectIconCatalog는 Addressables 로드(인스펙터 할당 불요).
            builder.Register<GameplayEffectCatalog>(Lifetime.Scoped).AsSelf();
            builder.RegisterInstance(LoadData<EffectIconCatalog>(AddressKeys.Data.EffectIconCatalog)
                                     ?? ScriptableObject.CreateInstance<EffectIconCatalog>());
            builder.Register<InGameModel>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterEntryPoint<GameHudController>(Lifetime.Scoped);

            // 인벤토리 MVI — Main(로비)에서도 인벤토리 창 사용(I키·HUD 버튼). 던전 구성과 동일.
            // ItemDisplayCatalog는 Resources 기본본 폴백(인스펙터 할당 불요).
            builder.RegisterInstance(LoadData<ItemDisplayCatalog>(AddressKeys.Data.ItemDisplayCatalog)
                                     ?? ScriptableObject.CreateInstance<ItemDisplayCatalog>());
            // 등급 배경 스프라이트 카탈로그(3.7) — 인벤/상점/장비 슬롯 공유. Addressables(미등록이면 빈 SO=배경 없음).
            builder.RegisterInstance(LoadData<GradeSpriteCatalog>(AddressKeys.Data.GradeSpriteCatalog)
                                     ?? ScriptableObject.CreateInstance<GradeSpriteCatalog>());
            builder.Register<InventoryModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<InventoryViewController>(Lifetime.Scoped);

            // 장비 MVI(3.2/7.2) — 던전과 동일. EquipmentViewController는 InventoryViewController가 쌍 토글로 참조.
            builder.Register<Game.Presentation.Equipment.EquipmentModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<Game.GUI.OutGame.EquipmentViewController>(Lifetime.Scoped).AsSelf();

            // 상점 MVI(3.5/7.6) — S키/HUD 상점버튼 단독 토글. Main 전용(던전 미등록 = 던전에선 S키 무반응).
            builder.Register<Game.Presentation.Shop.ShopModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<Game.GUI.Shop.ShopViewController>(Lifetime.Scoped).AsSelf();

            // 퀘스트 MVI(4.4) — HUD 퀘스트버튼 단독 토글. 진행 저널(목록). 수주/보상은 NPC 대화로 일원화.
            // QuestNotifier=수락/완료/보상 알림 단일 소스(QuestModel·DialogueModel 공유) → Presenter 가 AlertPopup 표시.
            builder.Register<Game.Presentation.Quest.QuestNotifier>(Lifetime.Scoped).AsSelf();
            builder.Register<Game.Presentation.Quest.QuestModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<Game.GUI.OutGame.QuestViewController>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<Game.GUI.Quest.QuestNotificationPresenter>(Lifetime.Scoped);

            // 캐릭터 정보/스탯창(7.3) — HUD Ability버튼·G키 단독 토글. 서버 권위 GetProgression pull.
            builder.Register<Game.Presentation.Progression.ProgressionModel>(Lifetime.Scoped).AsSelf();
            builder.RegisterEntryPoint<Game.GUI.Stat.StatViewController>(Lifetime.Scoped).AsSelf();

            // 대화/NPC(4.5 A1) — NPC(IInteractable) E 상호작용 → IDialogueLauncher.Open(npcId) → 대화창.
            // 콘텐츠=DialogueCatalog(SO, Resources 폴백). DialogueViewController=IDialogueLauncher 구현(창 로드+Start).
            // NPCDialogueBinder=씬 NPC 일괄 바인딩. 서버 0(A1).
            builder.RegisterInstance(LoadData<Game.Presentation.Dialogue.DialogueCatalog>(AddressKeys.Data.DialogueCatalog)
                                     ?? ScriptableObject.CreateInstance<Game.Presentation.Dialogue.DialogueCatalog>());
            // 대화 카메라(A3) — 씬의 DialogueCameraController(전용 vcam Priority 승격)를 IDialogueCamera 로 노출.
            // 씬에 컨트롤러가 있어야 DialogueModel/NPCBinder 의 IDialogueCamera 가 해소됨(없으면 주입 실패).
            builder.RegisterComponentInHierarchy<Game.Gameplay.Camera.DialogueCameraController>()
                   .As<Game.System.Dialogue.IDialogueCamera>();
            builder.Register<Game.Presentation.Dialogue.DialogueModel>(Lifetime.Scoped)
                   .As<Game.System.Dialogue.IDialogueLauncher>().AsSelf();
            builder.RegisterEntryPoint<Game.GUI.Dialogue.DialogueViewController>(Lifetime.Scoped);
            builder.RegisterEntryPoint<Game.Gameplay.Character.NPCDialogueBinder>(Lifetime.Scoped);

            // 진행/스탯창(7.3) MVI — 서버 권위 pull(GetProgression). View(스탯창)는 Model만 주입받는다.
            builder.Register<ProgressionModel>(Lifetime.Scoped).AsSelf();

            // Main 클라 스탯 홀더 — GetProgression 캐시(로그인 시 StartAsync, 킬 시 MainMonsterSpawner 가 Refresh).
            // LocalCombat(데미지=AttackPower)·킬 후 레벨/Exp 로그가 동기 읽기. 진실원=서버. 던전 미등록(서버 권위).
            builder.RegisterEntryPoint<PlayerProgressionHolder>(Lifetime.Scoped).AsSelf();

            // 소모품(3.8) — 효과 데이터(클라 SO) + Side Effect 핸들러(OnConsumableUsed→GAS). 미존재 시 빈 SO 폴백.
            builder.RegisterInstance(LoadData<ConsumableCatalog>(AddressKeys.Data.ConsumableCatalog)
                                     ?? ScriptableObject.CreateInstance<ConsumableCatalog>());
            // 소모품 회복 effect 를 GameplayEffectCatalog 에 등록(SO→카탈로그, 교리 gas-architecture §2.5).
            builder.RegisterEntryPoint<ConsumableCatalogSeeder>(Lifetime.Scoped);
            builder.RegisterEntryPoint<ConsumableEffectHandler>(Lifetime.Scoped);

            // 스킬 데이터(2.2) — SkillDefinition SO 카탈로그 → SkillCatalogProvider(id→SkillTimeline).
            // LocalCombat(Main hitbox)이 사용. 서버는 같은 데이터를 bake skills.json 으로 읽음(데이터 진실원=SO, §2.5).
            builder.RegisterInstance(new Game.Gameplay.Abilities.SkillCatalogProvider(
                LoadData<Game.Gameplay.Abilities.SkillCatalogDefinition>(AddressKeys.Data.SkillCatalog)
                ?? ScriptableObject.CreateInstance<Game.Gameplay.Abilities.SkillCatalogDefinition>()));

            // Main 씬은 로컬 플레이어만 스폰. RemotePlayerPrefab 없음.
            builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab));
            builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);

            // 3인칭 카메라 Follow 런타임 바인딩 — 씬의 GameplayCameraRig 가 LocalPlayerContext.OnSet 구독 →
            // 스폰된 로컬 플레이어 CameraFollowTarget 으로 vcam.Follow 세팅. 씬에 컴포넌트가 있어야 주입 해소됨.
            builder.RegisterComponentInHierarchy<Game.Gameplay.Camera.GameplayCameraRig>();

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

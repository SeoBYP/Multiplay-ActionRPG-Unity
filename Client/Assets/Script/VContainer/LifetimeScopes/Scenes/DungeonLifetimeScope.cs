using Game.Gameplay.Character;
using Game.Gameplay.Spawn;
using Game.GUI.OutGame;
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
using Game.Gameplay.Input;
using Game.GUI;

public class DungeonLifetimeScope : LifetimeScope
{
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private GameObject monsterPrefab; // M3 ⑥ 서버 권위 몬스터(기본/폴백)
    [SerializeField] private Game.Gameplay.Monster.MonsterVisualCatalog monsterVisualCatalog; // monsterId → 표시 프리팹
    [SerializeField] private GameObject groundItemPrefab; // 3.3 서버 권위 드랍(바닥 아이템)
    [SerializeField] private EffectIconCatalog effectIconCatalog; // 버프 표시 매핑(표시 전용)
    [SerializeField] private ItemDisplayCatalog itemDisplayCatalog; // 인벤토리 아이템 표시 매핑(itemId→이름·아이콘·분류)

    // 게임 데이터 SO를 Addressables(로컬 번들)에서 동기 로드. Resources 폐기 — 빌드 항상포함 회피.
    // 씬 수명 카탈로그라 핸들은 의도적으로 보존(앱 내내 필요). 미등록 주소면 null → 호출부가 빈 SO 폴백.
    private static T LoadData<T>(string address) where T : Object
        => Addressables.LoadAssetAsync<T>(address).WaitForCompletion();

    protected override void Configure(IContainerBuilder builder)
    {
        // 로컬 플레이어 ASC 공유 컨텍스트 — CharacterSpawner(생산)·InGameModel(소비)이 동일 인스턴스 공유.
        builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();

        // 파티(로컬+원격) ASC 레지스트리 — CharacterSpawner(생산) · EffectReceiver(타겟 라우팅) · PartyModel(집계)이 공유.
        builder.Register<PartyAscRegistry>(Lifetime.Scoped).AsSelf();

        // 아이템 획득 알림 허브 — 던전은 소켓 경로라 미사용이나 InGameModel 주입 충족용(Main 과 동일 구성).
        builder.Register<ItemPickupNotifier>(Lifetime.Scoped).AsSelf();
        builder.Register<InteractionPromptNotifier>(Lifetime.Scoped).AsSelf(); // 상호작용 안내(HUD)

        // Effect/버프 — 정의 카탈로그(GAS) + 표시 카탈로그.
        // 표시 카탈로그: 인스펙터 할당 우선 → Resources 기본본 → 빈 인스턴스 순으로 폴백.
        builder.Register<GameplayEffectCatalog>(Lifetime.Scoped).AsSelf();
        builder.RegisterInstance(effectIconCatalog != null
            ? effectIconCatalog
            : LoadData<EffectIconCatalog>(AddressKeys.Data.EffectIconCatalog)
              ?? ScriptableObject.CreateInstance<EffectIconCatalog>());

        // InGame MVI Model
        builder.Register<InGameModel>(Lifetime.Scoped)
            .AsImplementedInterfaces()
            .AsSelf();

        // EF-2d: 서버 권위 Effect 수신 → 대상 ASC 적용.
        builder.RegisterEntryPoint<EffectReceiver>(Lifetime.Scoped);

        // GameHud는 씬에 미리 배치하지 않고 Addressable로 로드·생성한다.
        builder.RegisterEntryPoint<GameHudController>(Lifetime.Scoped);

        // 파티 HP HUD(좌상단) — Model(레지스트리+로스터 집계) + View(코드로 Canvas 생성).
        // 신규 패킷 없음: 기존 S_ApplyEffect 방 브로드캐스트 + 원격 ASC 레지스트리로 HP 추적.
        builder.RegisterEntryPoint<PartyModel>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<PartyHpView>(Lifetime.Scoped);

        // 인벤토리 MVI — Model + 표시 카탈로그(인스펙터 → Resources 폴백 → 빈 인스턴스) + 창 컨트롤러.
        builder.RegisterInstance(itemDisplayCatalog != null
            ? itemDisplayCatalog
            : LoadData<ItemDisplayCatalog>(AddressKeys.Data.ItemDisplayCatalog)
              ?? ScriptableObject.CreateInstance<ItemDisplayCatalog>());
        // 등급 배경 스프라이트 카탈로그(3.7) — 인벤/장비 슬롯 공유. Resources 폴백(미할당이면 빈 SO=배경 없음).
        builder.RegisterInstance(LoadData<GradeSpriteCatalog>(AddressKeys.Data.GradeSpriteCatalog)
                                 ?? ScriptableObject.CreateInstance<GradeSpriteCatalog>());
        builder.Register<InventoryModel>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<InventoryViewController>(Lifetime.Scoped);

        // 장비 MVI(3.2/7.2) — Model + 창 컨트롤러. ItemDisplayCatalog는 인벤토리와 공유(위에서 등록).
        // EquipmentViewController는 InventoryViewController가 쌍 토글로 참조(Show/Hide).
        builder.Register<Game.Presentation.Equipment.EquipmentModel>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<Game.GUI.OutGame.EquipmentViewController>(Lifetime.Scoped).AsSelf();

        // 진행/스탯창(7.3) MVI — 서버 권위 pull(GetProgression). View(스탯창)는 Model만 주입받는다.
        builder.Register<ProgressionModel>(Lifetime.Scoped).AsSelf();

        // 진행 캐시 홀더 — HUD exp 게이지용(던전 입장 시 StartAsync로 현재 레벨/Exp 1회 pull, InGameModel이 중계).
        // 던전 전투 데미지는 서버 권위라 홀더 미사용 — 표시 전용. (Main은 추가로 LocalCombat 데미지에도 사용.)
        builder.RegisterEntryPoint<PlayerProgressionHolder>(Lifetime.Scoped).AsSelf();

        // 소모품(3.8) — 효과 데이터(클라 SO, 토스트/표시용). 미존재 시 빈 SO 폴백.
        builder.RegisterInstance(LoadData<ConsumableCatalog>(AddressKeys.Data.ConsumableCatalog)
                                 ?? ScriptableObject.CreateInstance<ConsumableCatalog>());
        // 소모품 회복 effect 를 GameplayEffectCatalog 에 등록(SO→카탈로그) → EffectReceiver 회복 미러가 effectId 로 조회 가능.
        // 데이터 진실원 교리 = gas-architecture §2.5(소모품 수치는 코드 시드 아닌 SO 저작).
        builder.RegisterEntryPoint<ConsumableCatalogSeeder>(Lifetime.Scoped);
        // ※ 던전은 ConsumableEffectHandler(로컬 회복 적용)를 등록하지 않는다 — 플레이어 HP 서버 권위(§4):
        //   ConsumeItem 성공 → GameServer→SocketServer 통지 → 서버가 회복 적용 + S_ApplyEffect 브로드캐스트 →
        //   EffectReceiver 가 로컬 ASC 에 적용(서버 미러). 여기서 로컬 적용하면 이중 회복.
        //   Main(솔로)은 SocketServer 없음 → MainLifetimeScope 가 ConsumableEffectHandler 로 클라 로컬 적용(§2).

        // CharacterAgent.Construct(IStateMachineBuilder)에 주입되는 게임플레이 의존성.
        // PlayerInputActions는 루트(ProjectLifetimeScope)의 전역 Singleton을 공유한다 — 재등록 금지.
        // (InjectGameObject가 부모 스코프에서 resolve하므로 씬 재등록 없이도 주입된다.)
        builder.RegisterInstance(new LocomotionSettings());
        builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
        builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

        // 어빌리티 데이터(AC-B) — 클라 쿨다운·hitbox 예측 + Cue 조회(서버와 동일 abilities.json 저작 소스).
        builder.RegisterInstance(new Game.Gameplay.Abilities.AbilityCatalogProvider(
            LoadData<Game.Gameplay.Abilities.AbilityCatalogDefinition>(AddressKeys.Data.AbilityCatalog)
            ?? ScriptableObject.CreateInstance<Game.Gameplay.Abilities.AbilityCatalogDefinition>()));

        // Dungeon 씬은 로컬 + 원격 플레이어 + 몬스터 + 바닥 아이템(드랍) 스폰.
        builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab, remotePlayerPrefab, monsterPrefab, groundItemPrefab));
        // 결정론 스폰 레이아웃 제공자 (spawn-layouts.json 로드).
        builder.Register<SpawnLayoutProvider>(Lifetime.Scoped).AsSelf();
        // 던전 맵 배경 모델 로드 (MapDefinition.visualPrefab).
        builder.RegisterEntryPoint<MapLoader>(Lifetime.Scoped);
        builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);
        // 3인칭 카메라 Follow 런타임 바인딩 — 씬의 GameplayCameraRig 가 LocalPlayerContext.OnSet 구독 →
        // 스폰된 로컬 플레이어 CameraFollowTarget 으로 vcam.Follow 세팅(Main 과 동일 구성).
        builder.RegisterComponentInHierarchy<Game.Gameplay.Camera.GameplayCameraRig>();
        // AC: ActorId→IActorView 레지스트리(발동 신호 라우팅) + 라우터. MonsterSpawner 가 몬스터를 등록, 라우터가 S_AbilityActivated 를 Cue 로.
        builder.Register<ActorRegistry>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<AbilityCueRouter>(Lifetime.Scoped);
        // M3 ⑥: 서버 권위 몬스터 스폰/보간/디스폰. monsterId → 표시 프리팹은 MonsterVisualCatalog(있으면).
        if (monsterVisualCatalog != null)
            builder.RegisterInstance(monsterVisualCatalog);
        builder.RegisterEntryPoint<MonsterSpawner>(Lifetime.Scoped);
        // 3.3: 서버 권위 드랍(바닥 아이템) 스폰/디스폰 + 줍기.
        builder.RegisterEntryPoint<GroundItemSpawner>(Lifetime.Scoped);
    }
}

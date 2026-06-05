using Game.Gameplay.Character;
using Game.Gameplay.Spawn;
using Game.GUI.OutGame;
using Game.Presentation.InGame;
using Game.System.Player;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Gameplay.Input;

public class DungeonLifetimeScope : LifetimeScope
{
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject remotePlayerPrefab;
    [SerializeField] private GameObject monsterPrefab; // M3 ⑥ 서버 권위 몬스터
    [SerializeField] private EffectIconCatalog effectIconCatalog; // 버프 표시 매핑(표시 전용)

    protected override void Configure(IContainerBuilder builder)
    {
        // 로컬 플레이어 ASC 공유 컨텍스트 — CharacterSpawner(생산)·InGameModel(소비)이 동일 인스턴스 공유.
        builder.Register<LocalPlayerContext>(Lifetime.Scoped).AsSelf();

        // Effect/버프 — 정의 카탈로그(GAS) + 표시 카탈로그.
        // 표시 카탈로그: 인스펙터 할당 우선 → Resources 기본본 → 빈 인스턴스 순으로 폴백.
        builder.Register<GameplayEffectCatalog>(Lifetime.Scoped).AsSelf();
        builder.RegisterInstance(effectIconCatalog != null
            ? effectIconCatalog
            : Resources.Load<EffectIconCatalog>("Effects/EffectIconCatalog")
              ?? ScriptableObject.CreateInstance<EffectIconCatalog>());

        // InGame MVI Model
        builder.Register<InGameModel>(Lifetime.Scoped)
            .AsImplementedInterfaces()
            .AsSelf();

        // EF-2d: 서버 권위 Effect 수신 → 대상 ASC 적용.
        builder.RegisterEntryPoint<EffectReceiver>(Lifetime.Scoped);

        // GameHud는 씬에 미리 배치하지 않고 Addressable로 로드·생성한다.
        builder.RegisterEntryPoint<GameHudController>(Lifetime.Scoped);

        // CharacterAgent.Construct(IStateMachineBuilder)에 주입되는 게임플레이 의존성.
        // PlayerInputActions는 루트(ProjectLifetimeScope)의 전역 Singleton을 공유한다 — 재등록 금지.
        // (InjectGameObject가 부모 스코프에서 resolve하므로 씬 재등록 없이도 주입된다.)
        builder.RegisterInstance(new LocomotionSettings());
        builder.Register<IStateFactory, StateFactory>(Lifetime.Scoped);
        builder.Register<IStateMachineBuilder, StateMachineBuilder>(Lifetime.Scoped);

        // Dungeon 씬은 로컬 + 원격 플레이어 + 몬스터 스폰.
        builder.RegisterInstance(new CharacterPrefabSettings(localPlayerPrefab, remotePlayerPrefab, monsterPrefab));
        // 결정론 스폰 레이아웃 제공자 (spawn-layouts.json 로드).
        builder.Register<SpawnLayoutProvider>(Lifetime.Scoped).AsSelf();
        // 던전 맵 배경 모델 로드 (MapDefinition.visualPrefab).
        builder.RegisterEntryPoint<MapLoader>(Lifetime.Scoped);
        builder.RegisterEntryPoint<CharacterSpawner>(Lifetime.Scoped);
        // M3 ⑥: 서버 권위 몬스터 스폰/보간/디스폰.
        builder.RegisterEntryPoint<MonsterSpawner>(Lifetime.Scoped);
    }
}

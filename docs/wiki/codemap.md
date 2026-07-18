# 코드맵 & 설계 결정 로그 (codemap)

> **목적**: "코드가 어디 있나(위치)"와 "왜 이렇게 했나(이유)"를 한곳에 박제한다.
> 코드를 통째로 다시 읽어 재추론하는 토큰 낭비를 막는다.
>
> **읽는 순서 (에이전트용)**: ① 이 파일의 도메인 인덱스로 위치 파악 → ② 결정 로그로 "왜" 파악 → ③ 필요한 **그 파일만** 읽는다. 전체 스캔 금지.
>
> **갱신 규칙 (필수)**: 서버/클라 기능 소스를 바꾸면 이 파일의 해당 항목을 갱신한다.
> 최상위 결정·정식 경로 변경이면 auto-memory `MEMORY.md`도 갱신한다.
> (Stop 훅 `check-codemap-freshness.ps1`이 미갱신을 감지해 알려준다.)

---

## 0. 설계 문서 지도 (어떤 문서를 언제 보나 — 먼저 여기서 찾는다)

> 프로젝트가 커질수록 "그거 어디 적었지?"가 비싸진다. **새 작업 전 이 표에서 해당 문서를 먼저 연다.**

| 보고 싶은 것 | 문서 |
|--------------|------|
| **지금 뭘 작업 중 / 다음 할 일 / WBS** | [plan.md](plan.md) |
| **코드 위치 찾기 + "왜 이렇게 했나" 결정 로그** | codemap.md (이 문서) — §1 위치 인덱스, §2 결정 로그 |
| **클라 vs 서버 권위 (전투·수치·연출 = 누가 소유?)** | [authority-model.md](authority-model.md) ← 전투/스킬/아이템 설계 전 필독 |
| **Character 구조 (Locomotion/Action 두 축·GAS·Driver)** | [character-architecture.md](character-architecture.md) |
| **GameplayEffect 버프/디버프 + HUD 연동** | [effect-system.md](effect-system.md) |
| **서버 Clean Architecture·의존성 방향·도메인 경계** | [architecture.md](architecture.md), [.claude/rules/architecture-server.md](../../.claude/rules/architecture-server.md) |
| **서버 연동 흐름 (게임 시작 E2E·세션)** | [gameflow.md](gameflow.md) |
| **패킷/Union 추가 규칙** | [packets.md](packets.md), [.claude/rules/networking.md](../../.claude/rules/networking.md) |
| **SocketServer (TCP·방·세션·핸들러)** | [socketserver.md](socketserver.md) |
| **Redis (스트림·캐시·컨슈머)** | [redis.md](redis.md) |
| **Unity 클라 (gRPC·VContainer·MVI 레이어)** | [unity-client.md](unity-client.md), [.claude/rules/unity-client.md](../../.claude/rules/unity-client.md) |
| **입력 시스템 (버퍼·라우터·전역화)** | [.claude/rules/unity-input.md](../../.claude/rules/unity-input.md) + 아래 §2.10 |
| **멀티플레이 테스트 (MPPM 2-창 / E2E)** | [mppm-testing.md](mppm-testing.md), [.claude/rules/testing.md](../../.claude/rules/testing.md) |
| **Actor 통합 전투(플레이어·몬스터 단일 파이프·6축 통합지도·서버분리 seam)** | [actor-combat-architecture.md](actor-combat-architecture.md) |
| **Ability SO 단일 저작(스킬 추가 절차 = SO+Export, 코드 0)** | [ability-so-authoring.md](ability-so-authoring.md) |
| **전투 진단(트레이스 2축·D1/D2·C1c 측정 결과)** | [combat-diagnostics.md](combat-diagnostics.md) |
| **몬스터 레벨링·변종(ID)·드롭 방침** | [monster-leveling.md](monster-leveling.md) |

> 신규 설계 문서를 만들면 **이 표에 한 줄 추가**한다(= 발견성 유지의 핵심).

---

## 1. 도메인 → 정식 위치 인덱스 (위치 찾기용)

| 도메인 | 정식 위치 | 심화 문서 |
|--------|-----------|-----------|
| 서버 아키텍처/의존성 | `ServerAll/GameServer/{API,Application,Infrastructure,Domain}` | [architecture.md](architecture.md), `.claude/rules/architecture-server.md` |
| 인증/세션 | `GameServer.Application/Domains/Auth/`, `GameServer.Application/Security/` | MEMORY.md(인증 현황) |
| 던전 로비(방 CRUD/시작) | `GameServer.Application/Domains/DungeonLobby/DungeonLobbyService.cs` | [gameflow.md](gameflow.md) |
| 방 생명주기(닫기) | 아래 §2.1 | [redis.md](redis.md) |
| 게임 세션 | `GameServer.Application/Domains/GameSession/` | [gameflow.md](gameflow.md) |
| 패킷/Union | `ServerAll/Shared/Shared.Packet/Packets/`, `Packet.cs` | [packets.md](packets.md), `.claude/rules/networking.md` |
| **공유 결정론 코어(전투 수식·히트박스·스킬)** | `ServerAll/Shared/Shared.Gameplay/`(서버 ProjectReference) + 클라 `Client/Assets/Plugins/Shared.Gameplay.dll`(동일 ns, 단일 소스) | [authority-model.md](authority-model.md) §2, §2.6 |
| **전투 흐름(입력→판정→데미지→연출)** | 클라 `Gameplay/Character/`(`PlayerCharacterAgent`·`CombatSyncSender`) → 서버 `SocketServer/.../Handler/CombatHandler` → `Room.DamageMonster` | [authority-model.md](authority-model.md), §2.7 |
| **회피(Dodge) — 대시+무적프레임** | 클라 `Gameplay/Character/{DodgeDriver,DodgeSyncSender}`·`PlayerCharacterAgent.HandleDodgeInput` → 서버 `SocketServer/.../Handler/DodgeHandler`·`PlayerState.TryBeginDodge`·`Room.TickMonsters`(iframe 게이트). 수치=`Shared.Gameplay/Combat/DodgeConfig`. 아래 §2.47 | [authority-model.md](authority-model.md) |
| **어빌리티 데이터(스킬·공격 단일 저작)** | 저작=클라 `Gameplay/Abilities/{AbilityDefinition,AbilityCatalogDefinition,AbilityCatalogProvider}` + `Editor/AbilityCatalogExporter` → bake `Shared.Infrastructure/Abilities/abilities.json` → 서버 `AbilityCatalog` → `CombatHandler.ResolveAbility(networkId)`. 자산 `Assets/GameData/Ability/`. ※구 Skill 계열(skills.json·SkillCatalog·SkillDefinition)은 AC-B 에서 전량 삭제 — §2.65~2.70 | [ability-so-authoring.md](ability-so-authoring.md) |
| **Actor 통합 전투(GAS) — ✅ 전 트랙 완료** | `Shared.Gameplay/Actors/ActorIds.cs`(+UserId/−InstanceId) · 발동 파이프=`S_AbilityActivated`(1604)→클라 `ActorRegistry`+`AbilityCueRouter`→`IActorView.PlayAbilityCue` · 서버 게이트=`CombatHandler`(쿨다운·cadence·마나)+`AbilityActivationMath`. 연대기 = §2.64~2.80 | [actor-combat-architecture.md](actor-combat-architecture.md) |
| **몬스터 카탈로그·레벨링·변종** | `Shared.Infrastructure/Monsters/{MonsterCatalog+monsters.json,MonsterTier,MonsterLevelScaling}`(상수 0 — `LevelTable` 직독) · 저작=클라 `Gameplay/Monster/MonsterCatalogDefinition`+`Editor/MonsterCatalogExporter` · **변종=별개 ID 직접 저작**(leviathan_boss). 레벨=`MapDefinition.monsterLevel`→`MapSpawnLayout.ResolveLevel`→스폰 1회 확정. §2.80 | [monster-leveling.md](monster-leveling.md) |
| **전투 진단(트레이스)** | 서버 `SocketServer/Diagnostics/CombatTrace`(Serilog Override 로 on/off) · 클라 `Network/Socket/Diagnostics/{CombatTraceRecorder,CombatTraceJoin}`(링 4096·무할당) · 창 `Gameplay/Editor/CombatTraceWindow`(`Tools/Combat/Combat Trace`). §2.74~2.76·2.79 | [combat-diagnostics.md](combat-diagnostics.md) |
| **상태이상(CC) — 스턴·슬로우·넉백** | 정의=`Shared.Gameplay` `GameplayTags.Stun/Slow`+`GameplayEffectCatalog`(stun_1_5s/slow_3s,GrantedTags)+`Combat/CcConfig`. 게이트=클라 `PlayerCharacterAgent`(스턴)·`GroundState`(슬로우). 부여=던전 **어빌리티 `OnHitEffectIds`**(abilities.json, AC-B)→`Room.TickMonsters`·`CombatHandler` 가 S_ApplyEffect(Amount=0) / Main `LocalMonster.onHitCcId`. **넉백**=`Gameplay/Character/KnockbackDriver`+`PlayerCharacterAgent.ApplyKnockback`(public, Ability 융합용). 아래 §2.48 | [authority-model.md](authority-model.md) |
| **게임플레이 카메라(3인칭 Follow)** | `Gameplay/Camera/{GameplayCameraRig,CharacterCameraFollow}` — rig가 `LocalPlayerContext.OnSet`→vcam.Follow 런타임 바인딩. 아래 §2.47 | — |
| SocketServer(TCP/방/세션) | `ServerAll/SocketServer/SocketServer/{Room,Session,PacketHandler}` | [socketserver.md](socketserver.md) |
| Redis 스트림/큐 | `Shared/Shared.Infrastructure/MessageQueue/`, `Messages/` | [redis.md](redis.md) |
| **루트/드랍(던전 경로)** | 드랍 롤 = `Shared.Gameplay/Loot/DropTable`(순수)+`Shared.Infrastructure/Loot/DropTableCatalog`(drop-tables.json, 레벨 반영 §2.80) · 줍기 = `SocketServer/Loot/GroundItem`·`Handler/{CombatHandler.SpawnDrops,LootHandler}`·`Room`(GroundItem·TryPickup) / 지급 = `GameServer.Infrastructure/Common/{Consumer/LootGrantConsumer,MessageQueue/LootPickupMessageQueue}` → `IInventoryService.GrantItemAsync`. 아래 §2.16. **Main(싱글) 경로 지급 = `GameServer.API/Services/InventoryGrpcService.GrantItem`(gRPC+가드, §2.18)** | [loot-drop.md](loot-drop.md) |
| 클라 gRPC | `Client/Assets/Script/Network/Https/` | [unity-client.md](unity-client.md) |
| 클라 소켓 | `Client/Assets/Script/Network/Socket/` | `.claude/rules/networking.md` |
| 클라 MVI 모델 (타이틀·로비·인게임) | `Client/Assets/Script/Presentation/{Title,DungeonLobby,InGame}` (asmdef `Game.Presentation`, ns `Game.Presentation.*`) — GUI가 바인딩하는 MVI 모델 레이어 | `.claude/rules/unity-client.md` |
| 클라 게임플레이/캐릭터/입력 | `Client/Assets/Script/Gameplay/` (asmdef `Game.Gameplay`, ns `Game.Gameplay.*`) — Character(`CharacterSpawner`·`MoveSyncSender`·`RemoteDriver`)·Camera·**Input**·**Spawn**(결정론 리졸버, §2.3) | `.claude/rules/unity-gameplay-state.md` |
| 스폰 레이아웃/맵(서버·클라 공용) | 진실원 `MapDefinition`(SO, `Gameplay/Spawn/`, 에셋 `Assets/GameData/Maps/`) → **bake** → 서버 `Shared/Shared.Infrastructure/Spawn/spawn-layouts.json`(임베디드)·클라 `Gameplay/Resources/spawn-layouts.json`. 맵 비주얼=`MapLoader`. 툴 `Gameplay/Editor/`: `MapDataExporter`(Export/Import/BakeAll)·`MapEditorWindow`(프리뷰 저작) | 아래 §2.3 |
| 클라 인증 | `Client/Assets/Script/System/Auth/` (ns `Game.System.Auth`) | — |
| 클라 GUI/HUD | `Client/Assets/Script/GUI/` | — |
| 클라 DI(VContainer) | `Client/Assets/Script/VContainer/` | `.claude/rules/unity-client.md` |
| 테스트 하네스 | 아래 §3 | `.claude/rules/testing.md` |

---

## 2. 설계 결정 로그 (왜 — append-only, 최신이 위)

> **AC 트랙 연대기 (2.64~2.80)** — 이 블록만 예외적으로 **오래된 것→새 것**(오름차순)이다: B1→B6→C→E~H 의 증분 서사를 보존한다.
> ⚠️ **재번호(2026-07-17)** — 원래 2.60~2.76 으로 매겨져 기존 항목(2.60 회피·2.61 콤보·2.62 로스터·2.63 캡슐)과 **번호가 충돌**했다. 과거 커밋 메시지·PR 본문의 참조는 아래 대조표로 읽는다:
> 구2.60(애니)→**2.64** · 2.61(B1)→**2.65** · 2.62(B2)→**2.66** · 2.63(B3)→**2.67** · 2.64(B4)→**2.68** · 2.65(B5)→**2.69** · 2.66(B6)→**2.70** · 2.67(C3-hotfix)→**2.71** · 2.68(C3)→**2.72** · 2.69(infra)→**2.73** · 2.70(C1a)→**2.74** · 2.71(C1b)→**2.75** · 2.72(C1c준비)→**2.76** · 2.73(사망체력바)→**2.77** · 2.74(C2)→**2.78** · 2.75(링포화)→**2.79** · 2.76(E~H)→**2.80**

### 2.64 몬스터 애니 파라미터 구동 전환 (Walk 버그 근본 수정, 2026-07-16)

- **버그**: 던전/Main 몬스터의 **Walk 애니가 안 나옴**. 근본 원인 = **컨트롤러와 코드의 구동 방식 불일치**.
  - 컨트롤러(`GameResources/Animations/Monster/*_AC.controller`)는 `Speed`(float)·`Attack`/`Die`(Trigger) 파라미터 전이로 저작됨 — `Walk→Idle [Speed Less 0.1] noExit`.
  - 그런데 `MonsterEntity`/`LocalMonster` 는 **상태이름 `CrossFadeInFixedTime`** 으로 구동하고 **`Speed` 를 한 번도 세팅하지 않음(항상 0)** → `CrossFade("Walk")` 로 Walk 진입 즉시 `Speed(0)<0.1` 이 참이라 **Idle 로 튕김**. (Die=나가는 전이 없음·Attack=exitTime 전이라 우연히 동작 → Walk 만 깨짐.)
- **수정**: 몬스터 애니 구동을 **RemoteDriver(플레이어)와 동일한 파라미터 방식**으로 통일.
  - `MonsterEntity`/`LocalMonster`: `idleState`/`walkState`/`dieState`/`attackState` 문자열 필드 + `PlayState()` + attack lock(`attackLockSec`/`_attackLockUntil`) **전부 제거**.
  - 대신 `CharacterAgentAnimations`(기존 플레이어 어댑터, 파라미터명이 **프리팹에 직렬화**) 재사용: 이동=`SetFloat(Speed, 평활화 속도)`(RemoteDriver 와 동일 역산·smoothing) · 공격=`SetTrigger(Attack)` · 사망=`SetTrigger(Dead)`.
  - 프리팹 9종(던전 8 + `CreepyDemonLocal`)에 `CharacterAgentAnimations` 추가 + 배선 `Speed`→"Speed" / `Attack`→"Attack" / `Dead`→**"Die"**(몬스터 컨트롤러 파라미터명).
  - → 컨트롤러 전이가 설계대로 동작(Idle↔Walk 는 Speed, 스윙 복귀는 Attack→Idle exitTime) = **잠금 해킹 불필요**.
- **회귀 고정**: `MonsterEntityAnimTests.서버_이동_수신하면_몬스터_Animator가_Walk상태로_전이한다`(신규) — 서버 이동 통지→보간→Speed 상승→Walk 전이.
- 검증: Unity 0오류 · PlayMode 애니 **6/6**(Walk 신규 + Attack 던전/Main + 원격 회피·콤보) · EditMode 172/172. 클라 전용 변경(서버 무관)이라 Docker E2E 영향 없음.
- **교훈**: Animator 컨트롤러를 파라미터 전이로 저작하면 **코드도 반드시 파라미터로 구동**해야 한다. 상태이름 CrossFade 와 섞으면 파라미터 조건(기본값)이 즉시 되돌린다.
- **후속 = AC-B**: 공격·스킬 저작을 **Ability SO 로 통합**. 설계 = [ability-so-authoring.md](ability-so-authoring.md), 진행 = plan.md M5 "AC-B".

### 2.65 AC-B B1 — Ability SO 저작 인프라 (2026-07-16)

- **목적**: 스킬 하나 추가에 **SO + 서버 switch + 클라 combo switch + 프리팹 4곳**을 고쳐야 하던 분산 저작을 **Ability SO 한 곳**으로. 데미지 출처도 단일화(**안B 확정**: `ability.baseDamage`, effect 는 태그/CC 전용).
- **신규**:
  - 클라 저작 `Gameplay/Abilities/AbilityDefinition.cs`(id·**networkId**·타임라인·hitbox·**baseDamage**·**activationRange**·onHitEffectIds(CC전용)·콤보 + **Cue**: `cueTrigger`(AnimationTriggerType)·`cueComboStep`) · `AbilityCatalogDefinition.cs`(목록, `Get(id)`/`GetByNetworkId`).
  - 에디터 `Gameplay/Editor/AbilityCatalogExporter.cs` → `Tools/Ability/Export`. **Cue 는 bake 제외**(서버는 연출을 모른다 — gas §2). id·**networkId 중복**·콤보 불변식 검증.
  - 서버 `Shared.Infrastructure/Abilities/AbilityCatalog.cs`(+`AbilityDef` record: Id/NetworkId/**SkillTimeline 재사용**/BaseDamage/ActivationRange) ← 임베디드 `Abilities/abilities.json`(csproj 등록).
  - 에셋 `Assets/GameData/Ability/`: `Ability_{BasicSwing,HeavySwing,ComboA,ComboB,ComboC}.asset` + `AbilityCatalogDefinition.asset`.
- **이관 규칙(밸런스 무변경)**: networkId = 기존 `ResolveSkill` 매핑 보존(0=basic/1=heavy/2·3·4=combo_a·b·c) · baseDamage = 기존 effect 실효값(10/10/10/15/25) · cueComboStep = 기존 `RemoteDriver` switch(3→1, 4→2, else 0) · onHitEffectIds = 비움(`*_dmg` 는 baseDamage 로 이관).
- **아직 아무도 안 씀**(설계대로): 기존 `skills.json`·`ResolveSkill` switch 그대로 동작. **B2 에서 원자적 전환**.
- 검증: `AbilityCatalogTests` 7(로드·networkId 매핑·수치 동일성·콤보 리치/불변식·baseDamage·미등록 null) → SocketServer.Tests **141/141** · ServerAll.sln 0오류 · Unity 0오류.
- ⚠️ 신규 클라 .cs 3개 + 에셋 6개 커밋 시 `.meta` `git add -f`.

### 2.66 AC-B B2 — ResolveSkill 카탈로그화 · skills.json 제거 (2026-07-16)

- **핵심 성과**: `CombatHandler.ResolveSkill` 의 **하드코딩 switch 제거** → `AbilityCatalog.Get(networkId)`.
  `public static AbilityDef? ResolveAbility(int)` 신설 + `ResolveSkill(int)` 은 `ResolveAbility(id)?.Timeline` 축약(기존 호출부 무변경).
  → **스킬 추가에 서버 코드 수정이 더는 필요 없다**(SO 저작 + Export + 서버 재빌드). int→어빌리티 매핑이 **데이터**(`AbilityDefinition.networkId`)로 이동.
- **삭제(dead)**: `Shared.Infrastructure/Skills/SkillCatalog.cs` · `Skills/skills.json` · csproj EmbeddedResource · 클라 `Editor/SkillCatalogExporter.cs`(삭제된 파일을 bake 하던 툴) · `SkillCatalogTests`(→`AbilityCatalogTests` 로 대체).
- **전환**: `ComboCadenceTests` 의 진실원을 `Skills.SkillCatalog` → `Abilities.AbilityCatalog.Get(id)!.Timeline` 으로 이관.
- **⚠️ 증분 경계 교훈(B1 수정)**: B1 에서 `onHitEffectIds` 의 `*_dmg` 를 미리 비웠더니, B2 가 카탈로그를 물리는 순간 **데미지가 0** 이 됐다(`ScaleDamageByStats` 는 B5 전까지 onHit 를 읽음) → `CombatHandlerStatDamageTests` 3건 실패로 조기 검출. **`*_dmg` 복원 + 재bake** 로 B2 를 *동작 무변경* 으로 되돌림. `baseDamage`(10/10/10/15/25)는 저작만 된 채 **B5 까지 미사용 대기**.
  → 원칙: **각 증분은 그 자체로 동작 보존**이어야 한다. 데이터 선반영이 증분 경계를 깨뜨렸다.
- **잔존(B3 에서 해소됨)**: 클라 `SkillDefinition`/`SkillCatalogDefinition`/`SkillCatalogProvider` → §2.67 참조.
- 검증: SocketServer.Tests **137/137**(−5 삭제 +1 신규) · ServerAll.sln 0오류 · **Docker E2E SocketE2ETests 31/31**(stale-image guard 경고 반영해 **gameserver·socketserver 둘 다** 리빌드 — Shared.Infrastructure 변경은 양 서버 이미지에 영향).

### 2.67 AC-B B3 — 클라 Cue 데이터화 + 저작 단일화 (2026-07-16)

- **동기(발견)**: B1 이후 같은 스킬이 **두 SO 에 중복 저작**(`GameData/Skill/Skill_*` + `GameData/Ability/Ability_*`)돼, 서버는 abilities.json(B2)·클라는 SkillCatalogDefinition 을 읽는 **드리프트 위험**이 생겼다 → B3 에서 클라도 Ability 로 일원화하며 Skill 계열 전량 제거.
- **신규**: `Gameplay/Abilities/AbilityCatalogProvider.cs` — `Get(id)`/`Get(networkId)`(→`AbilityDefinition`, Cue 포함) + `GetTimeline(id)`/`GetTimeline(networkId)`(→`SkillTimeline`, 게임플레이). 구 `SkillCatalogProvider` 대체.
- **Cue 계약 변경**: `IActorView.PlayAbilityCue(int skillId)` → **`PlayAbilityCue(AnimationTriggerType trigger, int comboStep)`**. **networkId→Cue 해석은 `AbilityCueRouter` 한 곳**(카탈로그 조회, 미등록이면 Attack/0 폴백 — 몬스터 주공격은 B4 까지 미등록).
  → `RemoteDriver` 의 하드코딩 콤보 switch(`3=>1, 4=>2, _=>0`) **제거**. 뷰는 카탈로그를 모른다(뷰=재생만).
- **하드코딩 매핑 제거(클라 미러)**: `LocalCombat`·`PlayerCharacterAgent` 의 `SkillName(int)` switch(0=basic/1=heavy/2·3·4=combo) 삭제 → `AbilityCatalogProvider.GetTimeline(networkId)` 데이터 조회. **서버 `ResolveSkill`(B2)과 대칭 — 이제 클라·서버 어디에도 int→스킬 하드코딩이 없다.**
- **삭제**: `Abilities/{SkillDefinition,SkillCatalogDefinition,SkillCatalogProvider}.cs` · `GameData/Skill/`(에셋 6) · `Editor/SkillCatalogExporter.cs`(B2) · `Tests/EditMode/Gameplay/SkillCatalogProviderTests.cs`.
- **DI/주소**: `AddressKeys.Data.SkillCatalog` → `AbilityCatalog`(`Assets/GameData/Ability/AbilityCatalogDefinition.asset`). Dungeon·Main 스코프가 `AbilityCatalogProvider` 등록. `AbilityCueRouter` 생성자에 provider 추가.
- 검증: Unity 0오류 · EditMode **170/170**(−2 삭제) · PlayMode 애니 **6/6**(**콤보 A→B→C 체인·늦은 패킷 안전망 회귀** = 연출 데이터화의 핵심 회귀) · **Docker E2E 31/31**.
- **잔여(B4 에서 해소됨)**: 몬스터 주공격이 카탈로그에 등록돼 정식 networkId(100+)로 해석된다 → §2.68.

### 2.68 AC-B B4 — 몬스터 어빌리티화 · 보스 다중 스킬 개방 (2026-07-16)

- **데이터 스키마**: `MonsterDefinition`(클라 SO)·`MonsterDef`(서버)에서 **`attackRange`/`attackCooldownMs`/`attackDamage`/`onHitEffectId` 4필드 제거** → **`abilityIds: List<string>`** 추가(저작 순서=발동 우선순위). 몬스터는 "무엇인가"(maxHp·moveSpeed·aggroRange·expReward)만 갖고 **공격은 전부 Ability SO 소유**. `MonsterCatalogExporter` Export/Import 양방향 갱신 → `monsters.json` 재bake.
- **에셋**: 몬스터 9종 `Ability_{monsterId}_attack.asset` — **networkId 100~108**(플레이어 0~4와 대역 분리, 겹치면 서버가 엉뚱한 어빌리티 발동). 쿨다운/사거리/데미지/CC 는 기존 monsters.json 값 **그대로 이관**(밸런스 무변경). abilities.json = 14종(플레이어 5 + 몬스터 9).
- **서버 어댑터**(`Server.Monster.MonsterCatalog`): `MonsterStats` 축소(MaxHp·MoveSpeed·AggroRange·**AttackRange**) — **AttackRange 는 이제 저작값이 아니라 그 몬스터 어빌리티들의 `max(ActivationRange)` 파생**(MonsterAiMath 의 Attack 페이즈 진입 판정용, Step 시그니처 무변경). `GetAbilities(monsterId)` 신설(미등록 id 는 조용히 skip).
- **상태**: `MonsterState.LastAttackAt`(단일) → **`GetLastCast(abilityId)`/`MarkCast(abilityId, now)`**(Dictionary) — 보스가 스킬별 쿨다운을 독립 추적.
- **선택 로직**: `Room.SelectMonsterAbility(m, target, nowMs)` — 저작 순서대로 **사거리 안 + `AbilityActivationMath.CanActivate` 통과인 첫 어빌리티** 반환(없으면 발동 없음). 데미지=`ability.BaseDamage`(→`StatCombatMath.MeleeDamage`), CC=`ability.Timeline.OnHitEffectIds` 순회. `S_AbilityActivated.SkillId` = `ability.NetworkId`(클라 라우터가 이 값으로 Cue 조회).
  → **보스 다중 스킬 = abilityIds 에 2개 이상 넣기만 하면 동작**(코드 변경 0). 앞의 강스킬 → 쿨다운이면 뒤의 평타로 폴백하는 식으로 저작.
- **안전 degrade**: 어빌리티 미저작/오타 몬스터 → `AttackRange=0` → 접근만 하고 공격 안 함(크래시 아님). 테스트로 고정.
- 검증: `MonsterAbilitySelectionTests` 5 신규(사거리 밖 미발동·쿨다운=어빌리티값·networkId 실림·데미지/CC 저작값·어빌리티 없으면 미공격) + `MonsterCatalogTests`/`MonsterAiMathTests`/`MonsterAttackTests` 개편 → **SocketServer.Tests 145/145** · ServerAll.sln 0오류 · Unity 0오류 · **Docker E2E 31/31**(양 서버 리빌드).

### 2.69 AC-B B5 — 데미지 출처 일원화(안B 완결) (2026-07-16)

- **결과**: **데미지 수치는 `ability.BaseDamage` 한 곳에서만 편집**한다(플레이어·몬스터 공통). effect 는 **CC/태그 전용**으로 역할 축소.
- **폐기**: `basic_attack_dmg` · `combo_a/b/c_dmg` · `monster_attack_dmg`(Shared `GameplayEffectCatalog` 코드 시드에서 제거).
  → 대체 = **`ability_damage` 단일 라벨**(Instant Health **placeholder −1**). **수치는 이 effect 가 정하지 않는다** — 서버가 `S_ApplyEffect.Amount`(권위 델타)로 실어 보내고 클라 `ApplyEffectAuthoritative(healthOverride)` 가 덮어쓴다.
- **서버**: `CombatHandler.ScaleDamageByStats`(effect 카탈로그 Health → AP 스케일) → **`BuildDamageMods(AbilityDef, ap, def)`** = `StatCombatMath.MeleeDamage(ability.BaseDamage, ap, def)`. `HandleAttack` 이 `ResolveAbility` 로 `AbilityDef` 확보 → 플레이어 피격에 `ability_damage`+`Amount=-BaseDamage` 브로드캐스트 + CC 는 별도 `Amount=0`. `Room.TickMonsters` 의 EffectId 도 `ability_damage` 로 통일.
- **밸런스 무변경**: baseDamage(10/10/10/15/25 + 몬스터 8~9999)가 구 effect 실효값과 동일. ※플레이어→플레이어는 **기존대로 플랫 피해**(AP·Defense 미반영) — 스탯 스케일로 바꾸는 건 별도 밸런스 결정이라 B5 범위 밖(주석에 명시).
- **⚠️ 함정(테스트에서 검출)**: `ability_damage` 의 placeholder(−1)는 **Amount 를 안 보내면 그대로 적용**된다(EffectReceiverTests 가 100→99 로 실패해 발견). 프로덕션은 항상 Amount 를 싣지만, **데미지 S_ApplyEffect 에 Amount 누락 시 조용히 1 피해**가 되는 구조 → 테스트를 프로덕션과 동일하게 `amount:` 포함으로 정렬해 고정.
- **테스트 이관**: `CombatHandlerStatDamageTests`(BuildDamageMods 기준·어빌리티별 저작값 검증 추가) · `MonsterDamageTests`(effect Resolve → BuildDamageMods, 단일소스 위임 가드는 CC 로) · `ConsumableEffectCatalogTests`(전투 조회 가드 → CC) · `AbilityCatalogTests`(onHit=CC 전용으로 반전) · E2E 3건(`ability_damage`+Amount 어설션) · `EffectReceiverTests`/`EffectSystemTests`.
- 검증: SocketServer.Tests **146/146** · Shared.Gameplay.Tests **50/50** · EditMode **170/170** · Unity 0오류 · **Docker E2E 31/31**(양 서버 리빌드).
### 2.70 AC-B B6 — 보스 다중스킬 실증 · **AC-B 트랙 완료** (2026-07-16)

- **실증**: `leviathan.abilityIds = [leviathan_slam, leviathan_attack]` — **코드 변경 0, 데이터 저작만으로** 다중 스킬 동작.
  - `Ability_leviathan_slam`(netId **109**, cd 6000, range 3.5, dmg 90, cc `stun_1_5s`) = 강스킬 우선 / `leviathan_attack`(cd 1800, range 3.0, dmg 40, `slow_3s`) = 폴백 평타.
  - 파생 확인: `MonsterStats.AttackRange` = max(3.5, 3.0) = **3.5**(slam 사거리) → AI 가 더 멀리서 Attack 페이즈 진입.
- **고정된 동작**(`BossMultiAbilityTests` 8): 저작 2개 확인 · 사거리 최대값 파생 · 첫 발동=강스킬 · **강스킬 쿨다운 중 평타 폴백** · 강 쿨다운 후 재사용 · **어빌리티별 독립 쿨다운**(평타를 여러 번 써도 slam 시계는 자기대로) · 강스킬이 더 아프고 CC 도 어빌리티별(stun vs slow) · 평타 사거리 밖/강 사거리 안이면 강스킬만.
- **⚠️ 테스트 픽스처 함정(검출)**: 보스 실데미지(slam 90 + 평타 40 = 130 > 기본 HP 100)로 **테스트 중 플레이어가 다운** → AI 타깃에서 빠져 이후 발동이 사라짐 → 선택 로직이 아니라 픽스처 때문에 실패. `maxHealth: 100_000` 로 해소(주석에 사유 명시).
- 검증: SocketServer.Tests **154/154** · ServerAll.sln 0오류 · **Docker E2E 31/31**(양 서버 리빌드).

### ✅ AC-B 트랙 완료 (B1~B6)

- **스킬 추가 절차(최종)**: `Ability_*.asset` 저작 → `Tools/Ability/Export` → 서버 재빌드. **코드 수정 없음.**
- int→스킬 하드코딩이 **클라·서버 어디에도 없음**(`ResolveSkill`/`SkillName`/`RemoteDriver` 콤보 switch 전부 데이터 조회로 대체).
- 데미지 = `ability.BaseDamage` 단일 출처 / effect = CC·태그 전용 / 연출 = `cueTrigger`+프리팹 파라미터명.
- **남은 확장점**: ① 어빌리티별 **전용 애니**(현재 보스 강스킬도 `Attack` 트리거 공유 — `AnimationTriggerType` enum + `CharacterAgentAnimations` 필드 + 컨트롤러 상태 추가 필요. leviathan FBX 엔 AttackSpecial/AttackHard/Roar 클립 존재) ② 플레이어→플레이어 데미지 플랫 유지(스탯 스케일 전환은 밸런스 결정) ③ VFX/SFX Cue.
- **⚠️ 함정(발견·해소)**: `AssetDatabase.CreateAsset` 로 만든 SO 는 **Addressable 로 자동 등록되지 않는다**. 미등록이면 `LifetimeScope.LoadData` 가 null → `?? CreateInstance<...>()` **빈 SO 폴백** → 클라 쿨다운·Cue·hitbox 가 조용히 전부 죽는다. **테스트로는 안 잡힌다**(E2E=raw socket, 애니 테스트=직접 생성 → Addressables 미경유).
  → `settings.CreateOrMoveEntry(guid, DefaultGroup)` + `address = 에셋경로`(이 repo 규약: 주소=경로) 로 등록 후, **Addressables 로드 → Provider 구성 → netId 0~4 조회**를 런타임 경로로 실증(cd/mana/cue 전부 기존값 일치, 콤보 0/1/2).
  → 교훈: **SO 를 코드로 생성하면 Addressable 등록·로드까지 확인**해야 한다.
- **PartyHpView 글씨색**: 흰색→**검은색**(사용자 요청).
- **검증**: 서버 build0 · **SocketServer.Tests 5**(신규 `ToJoinedPacket은_PlayerState의_HP기준선을_S_PlayerJoined에_싣는다` — `ToJoinedPacket` internal+`InternalsVisibleTo("SocketServer.Tests")` 시임) · 클라 컴파일0 · EditMode **157**(신규 `PlayerJoined_HP기준선이_스냅샷에_실리고_Move후에도_보존된다`) · PlayMode 7(신규 `원격_스폰시_서버_HP기준선으로_ASC가_초기화된다` + `PartyModelTests` 3) · **E2E `SocketE2ETests` 28/28**(신규: `두_클라이언트_입장` 이 S_PlayerJoined MaxHp>0·Hp==MaxHp 검증, Docker 리빌드 후 201s).
- **잔여 한계**: MaxHp는 입장 시점값(던전 중 레벨업 미반영 — 던전 내 레벨업 없음 YAGNI). 최초 구현(§2.58)의 "prefab 근사" 한계는 이 수정으로 해소.

### 2.71 AC-C3-hotfix — 데미지 경로의 송신마킹 제거(D2 회귀 봉합) (2026-07-17)

- **증상(D2)**: 몬스터를 때리면 클라 HP 가 옛 값으로 되돌아가고 **그대로 고착**(다음 틱이 정정하지 않음).
- **원인 — 내가 AC 증분7(dirty-flag)에서 만든 회귀**: `CombatHandler.ApplyAttackToMonsters` 가 즉시 브로드캐스트 후 `monster.MarkStateSent()` 를 호출했다.
  틱은 패킷을 **만든 뒤 나중에 송신**하므로(`Room.TickMonsters` 생성 → `RoomTickService` 송신) 그 사이 데미지가 들어가면 **옛 HP 패킷이 새 HP 뒤에 도착**한다.
  마킹까지 해두면 다음 틱이 `StateDirty()==false` → **정정 포기** → 영구 고착. 증분7 이전엔 매 틱 재전송이라 조용히 자가 교정되던 것이 dirty-flag 도입으로 드러났다.
- **수정**: `MarkStateSent()` **한 줄 제거** + 이유 주석. 마킹은 **틱만** 한다 → HP 변화는 다음 틱이 무조건 재전송해 **자가 교정**.
  비용 = **피격당 1패킷**. 증분7 의 목적(Idle 트래픽 0)은 유지 — 위치·회전·페이즈 dirty 판정은 무변경.
- **한계(안전망일 뿐)**: 순서 역전 자체는 못 막는다. 한 틱 동안 옛 HP 가 보였다가 정정된다(짧은 HP 튐).
  **근본해법 = AC-C3(`S_MonsterState.Seq` + 클라 스테일 드롭, 공개계약 변경이라 승인 대기)**. 설계 = `docs/wiki/combat-diagnostics.md` §4·§5.
- **테스트 — 인과를 양방향으로 고정** (`SocketServer.Tests/Monster/MonsterTickDirtyStateTests.cs`):
  - `HP가_바뀌면_이동이_없어도_다음틱이_재전송한다_자가교정` — 불변식(프로덕션 경로).
  - `데미지_경로가_송신마킹하면_자가교정이_깨진다_회귀가드` — **구 동작 재현**으로 "마킹하면 정정 패킷이 사라진다"를 못 박음.
  ⚠️ **교훈**: 첫 시도의 테스트는 `DamageMonster` 를 직접 불러서 **hotfix 유무와 무관하게 통과**했다(마킹은 CombatHandler 만 했으므로). 회귀 테스트는 **버그 동작을 실제로 재현**해야 가드가 된다 — 불변식만 쓰면 "통과하지만 못 잡는" 테스트가 된다.
- **검증**: SocketServer.Tests **156/156** · ServerAll.sln 0오류 · Docker(socketserver 리빌드) **E2E SocketE2ETests 31/31**.

### 2.72 AC-C3 — S_MonsterState.Seq + 클라 스테일 드롭 (D2 근본 해결) (2026-07-17)

- **공개계약 변경(승인받음)**: `S_MonsterState` 에 `int Seq` 추가(Union 1811 유지, 필드 추가만). 클라 미러는 **ClientCodegen 재생성**(`dotnet run --project ServerAll/Tools/ClientCodegen -- <repoRoot>`) — 손편집 금지.
- **핵심 계약 — Seq 는 스냅샷(생성) 시점에 찍는다**(`MonsterState.NextSeq()`). 송신 시점에 찍으면 Seq 가 도착 순서와 같아져 **아무것도 못 거른다**(막으려는 게 바로 생성≠송신 순서).
- **생산자 2곳**: `Room.TickMonsters`(lock 안) · `CombatHandler.ApplyAttackToMonsters`(**lock 밖**) → 서로 다른 컨텍스트라 `Interlocked.Increment` 로 발급. 첫 발급 1 + 클라 baseline 0 = "첫 상태 항상 통과".
- **소비자 1곳**: `SocketPacketState.UpdateMonster` 에서 `seq <= existing.Seq` → **드롭**(보간 이벤트도 억제). 상태 저장소 = 단일 초크포인트, 핸들러는 전달만.
  `SocketMonsterSnapshot.Seq` 추가(+`WithState(..., seq)`). 생성자 `seq` 는 **기본값 0** — 스폰 baseline.
- **범위 결정**: `S_SpawnMonster` 엔 Seq 를 **넣지 않았다**. baseline 0 이라 첫 상태가 통과하고, 신규 입장자 로스터 경합은 다음 틱에 자가 교정되는 일시적 건이라 계약을 넓힐 이유가 없다(YAGNI).
- **C3-hotfix 는 유지**: Seq 도입 후에도 `CombatHandler` 의 `MarkStateSent()` 생략을 되돌리지 않았다 → **Seq(순서 무효화) + 무조건 재전송(자가 교정)** 이중 안전망. 비용은 피격당 1패킷.
- **⚠️ 함정 — 손으로 만든 `S_MonsterState` 는 Seq=0 이라 드롭된다**: 기존 `SocketApiClientTest.MonsterState_Dispatch시...` 가 이 때문에 깨져 `Seq = 1` 을 넣어 고쳤다. 테스트에서 패킷을 직접 만들 땐 **Seq 필수**(서버는 항상 ≥1 을 찍음).
- **테스트가 진짜 잡는지 실측 검증함**(§2.71 교훈 적용): 가드(`if (seq <= existing.Seq) return;`)를 임시 제거 → `뒤늦게_도착한_옛_상태는_Seq로_버려진다_AC_C3` 가 **`Expected: 18, But was: 30`** 으로 실패(= D2 증상 그 자체) → 복원 후 그린. **추론이 아니라 실패를 확인**했다.
- **위치**: 계약 `Shared.Packet/Packets/Domains/MonsterPackets.cs` · 발급 `SocketServer/Monster/MonsterState.cs`(`NextSeq`) · 생산 `Room/Room.cs`·`PacketHandler/Handler/CombatHandler.cs` · 소비 `Client/.../Network/Socket/SocketApiClient.cs`(`UpdateMonster`)·`Handler/Contents/MonsterPacketHandler.cs`.
- **테스트**: 서버 `Monster/MonsterStateSeqTests.cs` 4종(첫 발급 1 · 단조 증가 · 몬스터별 독립 · 데미지 스냅샷 Seq > 틱 스냅샷 Seq) + 직렬화 라운드트립에 Seq 추가 → **160/160**. 클라 `SocketApiClientTest` 3종(스테일 드롭 · 동일 Seq 드롭 · 정상 반영) → EditMode **174/174**. E2E **31/31**.
- **남은 한계**: 순서 역전은 무효화하지만 **재전송을 앞당기진 않는다** → 체감 지연 자체는 C1c 측정 후 판단(C2b).

### 2.73 검증 인프라 결함 2건 수정 — stale-image guard 오탐 · 클라 검증 명령 사망 (2026-07-17)

**① stale-image guard 오탐(영구) — `.claude/hooks/check-stale-server-image.ps1`**
- **증상**: AC-C3 에서 `Shared.Packet` 을 고치자 E2E 마다 "infra-gameserver 이미지가 소스보다 오래됨" 경고. 리빌드해도 **영원히 안 사라짐**.
- **원인 2겹**:
  1. 매핑이 거칠었다 — `Dirs = @('GameServer','Shared')`. 그런데 `Shared.Packet` 은 **SocketServer 전용**(GameServer.API 는 참조 안 함).
  2. Dockerfile 이 `COPY Shared/ Shared/` 라 레이어 캐시는 깨져 **리빌드는 실제로 돈다**. 하지만 산출물이 **바이트 동일** → 도커가 기존 이미지 ID 재사용 → **`.Created` 가 안 올라감** → 경고 영구 지속 → alarm fatigue → **진짜 stale 을 놓치게 됨**(guard 존재 이유가 무력화).
- **수정**: 이미지→소스 매핑을 **진입 csproj 의 ProjectReference 폐포**에서 유도(`Get-ProjectDirs`). 하드코딩 목록은 참조가 늘면 **false negative**(위험한 방향)로 썩는다 — csproj 그래프는 컴파일러와 같은 진실원이라 드리프트 불가.
  진입점: gameserver=`GameServer/GameServer.API/GameServer.API.csproj`, socketserver=`SocketServer/SocketServer/SocketServer.csproj`.
- **추가 수정(false negative)**: 파일 필터에 `*.json` 포함. `Shared.Infrastructure/Abilities/abilities.json` 같은 **임베드 카탈로그는 동작을 바꾸는데** 기존 필터(`*.cs,*.csproj,*.proto`)가 못 봐서, json 만 고치고 리빌드 안 하면 **경고 없이 옛 서버를 검증**했다.
- **실측 검증(양방향)**: ① `Shared.Packet` touch → **socketserver 만** 경고(gameserver 안 걸림) ② `GameServer.API/Program.cs` touch → **gameserver 경고**(진짜 stale 탐지 유지) ③ mtime 복원 → 무경고. EditMode 입력은 무시.
- **남은 한계(주석에 명시)**: 주석/공백만 바꿔 산출물이 동일하면 `.Created` 가 안 올라가 경고가 남는다. 근본해결은 빌드시 소스 해시를 이미지 LABEL 로 박는 것 — 빌드캐시 비용 대비 가치 없어 보류.

**② 클라 검증 명령이 죽어 있었다 — `CLAUDE.md` §검증 명령**
- **증상**: `dotnet build Client\Game.Main.csproj` 가 CS2001 14건으로 실패.
- **원인**: `Client/*.csproj` 는 **Unity 생성물**(gitignore)인데, Unity 는 **asmdef 있는 것만 재생성하고 고아는 지우지 않는다.**
  `Game.Main`/`Game.Input`/`Game.OutGame`/`Game.InGame`/`Game.System.DungeonLobby` 는 **asmdef 가 없는 2026-05-30 화석**(asmdef 재편 이전 레이아웃) → 옮겨진 `Assets/Script/Input/*` 을 참조. 살아있는 csproj 는 07-16 까지 갱신됨(대조).
  → **CLAUDE.md 의 클라 검증은 asmdef 재편 이후 계속 깨진 채였다.**
- **더 나쁜 사실**: 화석을 치워도 `dotnet` 은 답이 아니다. `Game.Gameplay`/`Game.GUI` 는 Unity 패키지(RenderPipelines.Core) 소스를 `dotnet` 컴파일러 설정으로 빌드해 **Unity 코드에서** CS8168/CS8347 이 나고, `Client.sln` 은 **MSB5004**(Unity.Timeline 중복)로 열리지도 않는다.
- **수정**: CLAUDE.md 를 **"클라는 Unity 가 유일한 권위"** 로 교정(`refresh_unity` → `read_console(errors)` → `run_tests(EditMode)`). `dotnet build Client\*.csproj` 는 **금지**로 명시하고 위 3가지 이유를 함께 적어 재도입을 막았다.
- **화석 csproj 삭제 완료(승인 2026-07-17)** — **5개가 아니라 7개**(13파일, `.Player` 변형 포함)였다.
  ⚠️ **검사법 교훈**: 처음엔 asmdef **파일명**으로 대조해 5개만 찾았는데, **Unity 는 asmdef 의 내부 `name` 필드로 csproj 이름을 정한다**(파일명 ≠ 어셈블리명). `name` 필드로 다시 대조하니 `Game.System.Editor`(07-05)·`Game`(05-29, Compile 0개 빈 껍데기)가 추가로 드러났다.
  삭제 전 4중 확인: ① asmdef `name` 목록에 없음 ② 살아있는 csproj 가 참조 안 함 ③ `Client.sln` 미포함(고정문자열 대조 — `grep -n` 정규식이 오탐을 냈다) ④ 백업 후 삭제.
  삭제 후: Unity 재컴파일해도 **되살아나지 않음**(= 진짜 고아 증명) · 컴파일 0오류 · EditMode 174/174 · csproj 는 gitignore 라 git 무영향.
  대상: `Game.Main` `Game.Input` `Game.OutGame` `Game.InGame` `Game.System.DungeonLobby` `Game.System.Editor` `Game`.

### 2.74 AC-C1a — 서버 전투 트레이스 `[CombatTrace]` (2026-07-17)

- **무엇**: "어떤 공격이 **어떤 공식·입력으로** 이 숫자를 냈나" + "왜 발동이 거부됐나"를 구조적 로그로 남긴다. 기본 Off.
- **위치**: `SocketServer/Diagnostics/CombatTrace.cs`(+`CombatPath`/`CombatGate` enum). 배선 = `Program.cs`(host.Build() 직후 `Configure`).
- **왜 static 인가**: 패킷 핸들러(`CombatHandler`)가 static 이라 DI 가 닿지 않는다. `Configure(ILogger)` 로 주입 가능하게 둬 테스트가 fake 로거를 꽂는다.
- **호출 지점**: 데미지 3경로(`CombatHandler.ApplyAttackToMonsters`=P→M / `HandleAttack` 플레이어 피격=P→P / `Room.TickMonsters`=M→P) + gate 4종(UnknownAbility·NoMana·ComboCadence·OnCooldown).
- **`formula` 는 호출부가 전달**: P→P 만 `flat(base)` — **산식 미경유**가 표기로 드러난다(AC-D2 비대칭의 증거). 나머지는 `max(1, base+AP-DEF)`.
  ⚠ 문자열은 `CombatTrace.FormulaMelee` 상수 — 진실원 `StatCombatMath.MeleeDamage` 가 바뀌면 **같이 바꿔야 한다**(리뷰 대상). 트레이스가 거짓말하면 진단이 아니라 오도다.
- **P→M 트레이스는 브로드캐스트 뒤에**: 이 HP 를 실어 나른 `S_MonsterState.Seq` 를 상관키로 실어야 클라 로그와 조인된다(직전 Seq 를 찍으면 어긋남). 사망 시엔 상태 패킷이 없어 seq=0.
- **플레이어 HP before/after 는 0**: 플레이어 HP 권위는 클라(결정론 lite)라 서버가 모른다. 몬스터만 실제 before→after 를 싣는다.
- **⚠️ 스위치는 Serilog 다 — 설계 문서가 틀렸다(구현 중 발견)**: 이 호스트는 `UseSerilog` + `ReadFrom.Configuration` 이라 **`Serilog:` 섹션만 읽고 `Logging:LogLevel` 은 무시**한다.
  → 처음에 `Logging:LogLevel:CombatTrace` 로 넣었더니 **단위 테스트는 통과했는데 Docker 로그가 0건**이었다(육안 검증이 아니었으면 못 잡았다).
  실제: `Serilog:MinimumLevel:Override:CombatTrace`(appsettings 기본 `Information` = Debug 트레이스 Off) / 켜기 `Serilog__MinimumLevel__Override__CombatTrace=Debug`.
  **부수 발견(미수정)**: appsettings 의 `Logging:LogLevel` 블록 전체가 **죽은 설정**(Serilog 가 무시) — `Microsoft: Warning` 도 실효 없음.
- **검증**: 단위 4종(`SocketServer.Tests/Diagnostics/CombatTraceTests.cs`) — **Off 무호출을 실측**(가드 제거 시 해당 테스트만 실패 확인 후 복원). SocketServer.Tests **164/164** · 솔루션 0오류 · **Docker 육안 64건**(`path=MonsterToPlayer formula=max(1, base+AP-DEF) actor=-7 ability=arachnya_attack(101) base=14 ap=0 def=5 final=9`) · 오버라이드 제거 시 **0건**(기본 Off 실증) · E2E **31/31**.

### 2.75 AC-C1b/C1b' — 클라 트레이스 링버퍼 + Combat Trace 창 (2026-07-17)

- **단일 소스** = `Network/Socket/Diagnostics/CombatTraceRecorder.cs`(링 512·구조체·무할당·기본 Off) + `CombatTraceJoin.cs`(순수 함수: 스윙 단위 병합·구간 delta). 창은 그 위의 **뷰**(로직 0).
- **배치가 Game.Network 인 이유**: 기록자가 Network(패킷 수신)와 Gameplay(HP 반영) 양쪽인데 `Game.Network.asmdef` 는 우리 어셈블리를 하나도 참조하지 않는다 → Core 에 두면 참조 추가 필요. Network 에 두면 `Gameplay→Network` 가 허용 방향이라 asmdef 무변경. 서버 `SocketServer/Diagnostics` 와 대칭.
- **static `Shared` + `NowMs`**: 에디터 창은 VContainer 스코프 밖이라 DI 로 같은 객체를 못 본다(스코프 Resolve 는 씬·이름 결합이라 더 취약). 서버 `CombatTrace` 와 같은 이유. 시각은 `Stopwatch`(단조·스레드 안전) — 소켓 수신 스레드가 기록하므로 `Time.time`(메인 스레드 전용) 은 쓸 수 없다.
- **배선 4곳**: `CombatSyncSender`(t_send, ActorId=`AuthSession.UserId`) · `AbilityActivatedPacketHandler` · `EffectPacketHandler`(**Amount≠0 만** — CC 는 태그라 제외) · `SocketPacketState.UpdateMonster`(HP델타 + 스테일드롭; 기록은 **lock 밖**).
- **⚠️ 배선이 C1b 설계 결함을 드러냈다(가장 중요)**: **몬스터 피해는 `S_ApplyEffect` 로 오지 않는다** — 그건 **플레이어가 대상일 때만**이고, 몬스터 HP 는 서버 권위로 계산돼 `S_MonsterState` 로만 온다.
  최초 `Join` 은 `DamageReceived` 가 있어야 대상을 정했으므로 **던전 주 시나리오(= 원 관측 "몬스터 HP 가 느리다")에서 아무 레코드도 못 만들었다.**
  → 전송 경로가 둘임을 반영: 몬스터는 **HP 델타가 유일한 데미지 신호**(`amount<0`)이고, 델타 0 인 이동 틱은 자연히 배제된다. 회귀 테스트 2종 추가.
  **교훈: 배선 없는 순수 로직 테스트는 "실제로 오는 패킷"을 검증하지 못한다.**
- **⚠️ stale 어셈블리 위의 거짓 그린**: `CombatSyncSender` 의 CS0246(AuthSession using 누락)으로 컴파일이 깨지자 Unity 가 **옛 어셈블리로 테스트를 돌려 184/184 "통과"** 가 나왔다(신규 2건이 없는데도). → **테스트 건수 증가를 반드시 확인**해야 한다(184→186). `read_console(filter_text="error CS")` 로 CS 만 걸러 봐야 환경성 경고에 묻히지 않는다.
- **창**: `Gameplay/Editor/CombatTraceWindow.cs`, 메뉴 `Tools/Combat/Combat Trace`. **IMGUI 채택**(§2.4 초안의 UI Toolkit 대신) — 매 프레임 갱신되는 진단 덤프라 즉시모드가 맞고 상주 UI 트리·바인딩이 불필요. 선례 `MapEditorWindow` 도 IMGUI.
  `Total > Count` 면 "N건 덮임" 경고(측정 유실). 상세는 서버 조인 키(actor·seq)를 안내 — AP/DEF 분해는 서버 로그 몫(§2.4 정정).
- **asmdef 변경(승인)**: `Game.Gameplay.Editor` references 에 `Game.Network` 1줄 추가. 하향이라 레이어 위반 아님. 대안(`Game.Network.Editor` 신설)은 원칙2(asmdef 과도 분리 금지)와 충돌해 기각.
- **함정**: 창 네임스페이스가 `Game.Gameplay.Editor` 라 `System.IO` 가 **`Game.System.IO`** 로 해석됨(CS0234) → `global::System.IO` 필수. (`.claude/rules/testing.md` 의 "System 세그먼트 금지"와 같은 뿌리)
- **검증**: 컴파일 0오류 · EditMode **186/186**(184 + 신규 2) · Docker E2E **31/31**(**리빌드 후** — 내가 고친 guard 가 `appsettings.json` 변경을 stale 로 잡았다. `*.json` 을 필터에 넣은 §2.73 수정이 실제로 false negative 를 막은 첫 사례).

### 2.76 AC-C1c 준비 — 트레이스가 **모든 액터**를 낸다 (2026-07-17)

- **사용자 관측**: "다른 플레이어가 한 건 기록이 안 되네" — 사실이었다.
- **원인**: `CombatTraceJoin.Build` 가 **`AttackSent` 만 스윙 시작점**으로 봤는데, `AttackSent` 는 로컬 `CombatSyncSender` 만 남긴다.
  원격 플레이어·몬스터의 발동은 **엔트리로는 쌓이는데 레코드가 안 만들어져** 창에서 사라졌다. (서버는 이미 몬스터 발동도 `S_AbilityActivated{ActorId=-instanceId}` 로 보내고 있었다 — `Room.cs:618`. **데이터는 다 오는데 내가 버리고 있었다.**)
- **수정**: 시작점을 액터별로 나눴다 — 로컬=`AttackSent`(t_send 를 안다) / 원격·몬스터=`AbilityActivated`(그들의 입력 시각은 클라가 알 방법이 없다. 서버 통지가 첫 관측).
  `SwingOrigin{LocalPlayer,RemotePlayer,Monster}` 추가. 로컬 스윙이 가져간 발동 통지는 `consumed` 로 표시해 중복 레코드를 막는다.
- **구간 지표가 origin 마다 다르다(중요)**: `SendToHpMs` 는 **로컬 전용**(원격은 -1). 모든 액터 공통 지표는 **`ActivateToHpMs`**(발동 통지→HP 반영). 창의 요약도 `[내]`/`[전체]` 로 갈라 표시한다.
  `LikelyGated` 도 **로컬 전용** — 원격·몬스터는 발동 통지가 곧 시작점이라 정의상 거부가 보이지 않는다.
- **동기화 검수**: `BuildMonsterSync` 추가 — 스윙과 무관하게 **상태를 받은 모든 몬스터**를 집계(HP·seq·갱신수·**스테일 드롭수**·**누적 피해**). 누적 피해는 서버 `[CombatTrace]` final 합과 대조하는 **데미지 검수** 근거. 창에 "몬스터 동기화" 탭 + CSV 2섹션.
- **ActorIds 를 못 쓴 이유**: `Game.Network.asmdef` 가 `overrideReferences: true` 이고 `precompiledReferences` 에 `Shared.Gameplay.dll` 이 없다 → 부호 규약을 `CombatTraceJoin.IsMonster` 로 국소화하고 진실원을 주석에 명시(asmdef 추가 변경 회피).
- **알려진 한계**: 여러 플레이어가 **동시에** 한 몬스터를 때리면 HP 델타의 주인을 클라가 구분할 수 없다(P→M 은 S_ApplyEffect 가 없어 SourceId 가 없다) → 확정은 서버 로그 조인.
- **회귀를 실측 확인**: 원격 레코드 생성을 끄니 `다른_플레이어의_스윙도_기록된다` 가 **Expected: 2, But was: 1**(= 사용자가 본 증상 그대로), `몬스터의_공격도_기록된다` 는 0건 → 복원 후 그린.
- **검증**: EditMode **189/189**(186 + 신규 3) · E2E **31/31**. ⚠ 첫 E2E 에서 `NICKNAME_ALREADY_TAKEN` 1건 실패 → 단독 재실행 통과 + 전체 재실행 31/31 = **테스트 격리 플래키**(반복 E2E 로 닉네임 누적), 내 회귀 아님.

### 2.77 몬스터 사망 시 체력바가 안 비는 버그 (2026-07-17)

- **사용자 관측**: "체력바가 남아 있는데 죽는 모션이 나와 — 죽을 때 체력이랑 UI 동기화가 안 되는 것 같아". 사실이었다.
- **원인(두 겹)**:
  1. **서버**: `CombatHandler.ApplyAttackToMonsters` 는 `dead` 면 `S_MonsterDead` **만** 보낸다 — 죽는 순간의 `S_MonsterState{Hp=0}` 은 없다(살아있을 때만 상태를 보냄).
  2. **클라**: `MonsterEntity.HandleDead` 가 die 트리거 + 지연 디스폰만 하고 **HP 를 0 으로 만들지 않았다**.
  → 체력바(`MonsterHealthBar`, `HpChanged` 구독)가 **치명타 직전 HP** 에 멈춘 채 `deathDespawnDelay`(2s) 동안 죽는 모션이 재생됐다.
- **수정**: `HandleDead` 에서 `Hp = 0; HpChanged?.Invoke(this);` 후 die 트리거.
- **왜 서버가 `Hp=0` 을 추가 전송하지 않고 클라가 유도하나**: `S_MonsterDead` 와 `S_MonsterState` 는 **송신 직렬화가 없어(D1) 순서가 뒤집힐 수 있다.** Dead 가 먼저 도착하면 `RemoveMonster` 로 스냅샷이 사라져 뒤이은 `Hp=0` 이 `UpdateMonster` 에서 **최소 스냅샷만 만들고 `OnMonsterMoved` 를 발행하지 않아 버려진다** → 간헐 재발. 반면 "사망 = HP 0" 은 **서버가 이미 내린 판정**이라 클라 유도가 권위를 해치지 않고 도착 순서와 무관하게 항상 맞다. (C2 로 D1 을 고쳐도 이 유도가 더 단순하고 안전하다.)
- **플레이어엔 왜 없나(비대칭)**: 플레이어 HP 는 `S_ApplyEffect.Amount` 로 클라 ASC 가 직접 차감한다(결정론 lite) → **죽인 그 타격이 이미 HP 를 0 으로 만든다.** 몬스터만 HP 가 `S_MonsterState` 로만 오는데 그 마지막 패킷이 없어서 생긴 문제.
- **회귀를 실측 확인**: 수정을 끄니 `사망시_체력바가_0으로_내려간_뒤_죽는모션이_나온다` 가 **Expected: 0, But was: 12**(= 관측된 증상 그대로) → 복원 후 그린.
- **위치**: `Client/.../Gameplay/Character/MonsterEntity.cs`(`HandleDead`) · 테스트 `Tests/PlayMode/InGame/MonsterEntityAnimTests.cs`.
- **검증**: PlayMode anim **3/3**(2 + 신규 1) · EditMode **189/189**.

### 2.78 AC-C2 — 세션 송신 큐 (D1 수정) (2026-07-17)

- **무엇**: `Session` 에 `Channel<byte[]>`(Bounded 1024) + 단일 소비자 `SendLoopAsync`. `SendPacketAsync` 는 **직렬화 + 큐잉만** 하고 실제 소켓 write 는 SendLoop 이 전담 → 프레임 단위 원자성 + FIFO.
- **기동/정리**: `RunAsync` 가 SendLoop 을 함께 띄우고, finally 에서 `Writer.TryComplete()` → `await sendLoop` 로 회수(안 하면 세션마다 Task 누수).
- **시그니처 유지 결정**: `Task SendPacketAsync(Packet, ct)` 그대로 → 호출부 ~20곳 무변경(연결 계층 변경의 위험 표면 축소). **의미는 바뀜** — Task 완료 = "큐에 들어갔다"이지 "전선에 나갔다"가 아니다. 송신 직후 `Disconnect()` 하면 유실되지만 **그런 호출부는 없다**(Disconnect 는 하트비트 타임아웃·에러·종료 경로뿐 — 확인함).
- **Bounded + 포화 시 끊김**: 무한 큐면 느린 클라 1명이 서버 메모리를 계속 먹는다(DoS 벡터). 1024 = 10Hz 틱 기준 ~100초치 = 사실상 죽은 연결. `FullMode.Wait` + **`TryWrite` 만 사용**(대기 모드는 TryWrite 가 실패를 알려주는 유일한 모드) — 생산자가 틱 스레드라 **절대 블록시키면 안 된다**.
- **직렬화 실패 처리 변경**: 예전엔 serialize 예외에도 세션을 끊었다 → 이제 그 패킷만 버리고 로깅(패킷 하나의 문제로 연결을 죽이지 않는다).
- **⚠️⚠️ 가장 중요 — D1 의 "치명적 프레임 인터리브" 주장은 플랫폼 의존이었다(실측)**:
  설계 §1.1 은 코드 리딩만으로 "부분전송 시 프레임 인터리브 = 파싱 desync(치명)" 이라 단정했는데, **Windows 에선 재현 불가**다.
  overlapped `WSASend` 는 **버퍼 전체 소비 후에만 완료**돼 `sent == frame.Length` 가 보장 → `while (offset < len)` 이 1회만 돈다 → **부분 전송이 없어 섞일 수가 없다.**
  큐를 우회한 채 **20KB 프레임 × 512B 송신버퍼 × 4스레드 동시 송신**을 돌려도 통과했다(두 번 시도: 첫 시도는 프레임이 버퍼보다 작아 무의미했고, 크게 키운 두 번째도 통과).
  **그러나 서버는 Linux 컨테이너에서 돈다** — Linux `send()` 는 부분 반환이 정상이라 거기선 실재한다. → **수정은 필요하되 프레임 원자성은 구조적 보장(단일 소비자)이지 테스트로 증명된 게 아니다.**
  **교훈: "코드 리딩으로 확인된 결함"도 재현 조건은 플랫폼에 달렸다. 단정 전에 돌려봐야 한다.**
- **테스트**(`SocketServer.Tests/Session/SessionSendQueueTests.cs`, 실제 루프백 소켓): 동시 송신 프레임 무결 · **순서 보존** · **포화 시 끊김** · 끊긴 세션 무시. 뒤 3개는 진짜 가드, 첫 번째는 Windows 에선 큐 없이도 통과함을 명시.
  ⚠ 네임스페이스는 `Server.Tests.Sessions` — `...Session` 으로 두면 전역 `Session` 타입을 가려 `TestSessionFactory` 등이 **CS0118 로 깨진다**(testing.md 의 'System' 금지와 같은 뿌리).
- **검증**: SocketServer.Tests **168/168**(164+4) · 솔루션 0오류 · Docker(리빌드) **E2E 31/31**. ※ 중간에 E2E 가 25/31 에서 멈췄으나 `isPlaying=False` 로 **플러그인 끊김 좀비** 확인(서버 로그에 큐 포화·SendLoop 에러 0건) → 도메인 리로드 후 재실행 31/31.

### 2.79 AC-C1c 후속 — 트레이스 링 포화 해소(안ⓒ) (2026-07-17)

- **측정이 드러낸 결함**: 링 **508/512 포화**, 그중 **451건(89%)이 이동 틱**(HP 델타 0). 정작 볼 스윙이 덮여 측정이 최근 수 초로 잘렸다. 집계도 링에서 유도해 **m3 가 seq 234 vs updates 185 = 49건 증발**.
- **왜 필터만으론 안 됐나(중요)**: 델타 0 을 그냥 안 찍으면 **한 대도 안 맞은 몬스터가 동기화 탭에서 사라진다** — 사용자가 명시한 "모든 몬스터가 다 나와야 한다"가 깨진다. → **자료구조를 목적에 맞게 분리**했다:
  - **링 = 이벤트 로그**(전투 관련만): AttackSent · AbilityActivated · DamageReceived · **MonsterHpApplied(델타≠0만)** · StaleDropped
  - **맵 = 몬스터당 1행 동기화 집계**(`_monsterSync`, `MonsterSync()`): **모든 갱신** 반영. 몬스터당 1행이라 폭증하지 않고 링 회전과 무관해 유실이 없다.
- **삭제**: `CombatTraceJoin.BuildMonsterSync`(링에서 유도 = 유실의 원인). 창은 `recorder.MonsterSync()` 를 쓴다.
- **용량**: 512 → **4096**(구조체 ~48B → ~200KB). 필터와 **함께** 해야 의미가 있다 — 둘 중 하나만으론 부족.
- **테스트 파장(정직히)**: 필터가 기존 테스트 2건을 깼다 — `RecordMonsterHpApplied` 에 amount 기본값 0 을 쓰던 것들이 "HP 반영"을 의도했는데 이제 링에 안 들어가 `SendToHpMs=-1` 이 됐다. 실제 P→M 은 HP 가 변할 때만 이 이벤트가 의미를 가지므로 **테스트를 현실에 맞춰 델타를 넣었다**(구현을 되돌리지 않음).
- **검증**: EditMode **192/192**(189+4: 이동 틱 링 제외 · 델타 있는 틱은 링 보존 · 집계 무유실 · 모든 몬스터 노출).

### 2.80 AC-E~H — 몬스터 레벨링·데이터 SO화·등급→ID·Main 체력바 (2026-07-17, PR #60)

한 흐름의 종착: C1c 측정(몬스터 피해 1~5 바닥) → 레벨링(E) → 데이터 SO화+던전 5개(F) → 등급을 ID 로(G) → Main 체력바(H). 설계 = [monster-leveling.md](monster-leveling.md)(§3 은 AC-G 로 폐기 주석).

- **레벨 스케일** = `Shared.Infrastructure/Monsters/MonsterLevelScaling.cs` — **상수 0개**, 플레이어 곡선을 `LevelTable`(SO 저작)에서 직접 읽는다: `base(L)=net₁·HP(L)/HP(1)+DEF(L)` · `maxHp(L)=maxHp₁·AP(L)/AP(1)`. 유도 근거 = 역할 보존(곱셈은 slam 폭발·단순가산은 중간 수렴). `StatCombatMath` 는 무변경(산식은 옳았고 틀린 건 base).
- **레벨 저작** = `MapDefinition.monsterLevel`(맵 기본) + `MonsterSpawn.level`(스폰 override, 현재 미사용) → `MapSpawnLayout.ResolveLevel`(단일 구현, 스폰>맵>1) → `Room.SpawnMonsters` 에서 **스폰 시 1회 확정**. 대역: dungeon_01=L1·02=L6·03=L12·04=L20·05=L30(보상·등급 구성 단조 증가, 테스트 고정).
- **⚠️ 등급의 최종형(AC-G)** — `spawn.tier`+배율 테이블(AC-F2)을 **같은 날 폐기**: enum 서버·클라 미러링·스폰 필드 2개·이중 조회 비용. 최종: `monsters.json` 의 `tier` **문자열**("Normal"/"Elite"/"Boss") = **분류일 뿐 스탯에 곱해지지 않는다**. 강한 개체 = **변종 행 직접 저작**(`leviathan` 500 / `leviathan_boss` 3000, `*_elite` 3종) — **스폰은 monsterId 하나만 처리**. `MonsterState.Tier` 는 카탈로그(monsterId 행)에서 읽는다(표시·연출 분기용, 아직 소비자 없음).
- **드롭** = `DropTableRoll.Roll(entries, rng, chanceMultiplier, quantityMultiplier)` 순수 오버로드(배율은 **인자** — Shared.Gameplay 는 Infrastructure 를 못 부른다) + `DropTableCatalog.Roll(id, rng, level)`. 수량 배율은 **가변수량(MaxQty>1, gold)에만**(장비 1~1 에 걸면 검 2자루). 8마리 전수 + `goblin` 유령 제거 + **변종별 자기 테이블**(배율이 없으니 없으면 안 떨군다). test_brute 는 픽스처라 제외.
- **Main 체력바(H)** = `IMonsterHealth`(Hp/MaxHp/HpChanged) — 구현체 둘(던전 `MonsterEntity`=서버 권위 / Main `LocalMonster`=클라 권위)이라 인터페이스 도입 기준 충족. `MonsterHealthBar` 는 계약만 봐서 **던전·Main 공용**. LocalMonster 도 **사망 시 HP 0 확정**(§2.77 버그의 Main 판 예방). 프리팹은 던전 것 서브트리 복제(`CreepyDemonLocal.prefab`).
- **저작 파이프 함정 3건(재발 방지)**: ① `spawn-layouts.json`/`drop-tables.json`/`monsters.json` 은 **exporter 생성물** — 직접 편집하면 다음 Export 에 덮인다(→ SO 저작 후 Export 가 유일 경로). ② exporter 의 `Export()` 는 끝에 `DisplayDialog`(모달) → **MCP/자동화가 무기한 블록**(Unity 멈춤의 원인) — 팝업 없는 `BakeAll()` 을 쓴다. ③ Import 왕복 배선 누락 시 bootstrap Import 가 저작값을 0 으로 지운다.
- **⚠️ 데이터 저작에도 계약 테스트**: leviathan base 를 65 로 착각(그건 arachnya)해 boss 390 = **원본(500)보다 약한 보스**를 저작 → `변종은_별개_ID_로_저작된다_AC_G`(boss.MaxHp > normal×4)가 잡았다. 오타 monsterId 는 Default 폴백으로 **공격 안 하는 유령**이 되므로 `스폰이_지목한_변종이_카탈로그에_존재한다_AC_G` 로 전수 검증.
- 잔여: dungeon_03~05 `visualPrefab` 없음(에셋) · `tier` 연출 소비자 없음(보스 체력바·등장 연출 후보) · AC-D(전용 애니·P→P 스케일·VFX Cue) · **`Shared.Gameplay/Abilities/SkillCatalog.cs` 죽은코드 삭제됨**(승인 후 2026-07-17 — 실호출 0, AC-B 가 AbilityCatalog 로 대체. Release 재빌드 209/209·50/50. ⚠ 클라 `Plugins/Shared.Gameplay.dll` 복사는 권한 정책으로 차단 → 사용자 수동 1회 필요).
- 검증(최종): SocketServer **209/209** · Shared.Gameplay 50/50 · EditMode **192/192** · PlayMode(anim 3/3 · Main 체력바 3/3) · Docker E2E **31/31**. PR #60 → main `972991e5`.

### 2.81 CA-5/AC-D3 Phase 1 — 어빌리티 연출 타임라인 데이터+런타임 (SFX/VFX) (2026-07-18)

전투가 "소리·이펙트 0"이던 것을 데이터 주도 연출 타임라인으로 연다. 사용자 요청 = "언제 Sound/VFX/판정을 편집할지 Timeline 툴로"(이미지 첨부). 설계 합의: **UI=커스텀 창 / 착수=Phase 1(데이터+런타임) 먼저**. **Phase 1a = 코드**(창은 Phase 2).

- **왜 두 갈래인가(핵심 제약)** — 판정창은 서버가 읽어야 하고(권위·치팅), SFX/VFX 는 클라 로컬(권위 없음). gas-architecture §2.5 그대로: **게임플레이 필드만 bake, 연출은 SO 에만**. 시계는 ms 오프셋 단일(서버 헤드리스도 같은 값 읽음).
- **데이터(신규, 클라 전용)**: `Gameplay/Abilities/AbilityCueEvent.cs`(`{timeMs, kind=Sfx|Vfx|Anim, id, socket}`, 순수) + `AbilityCuePlan.cs`(순수 플래너 — 빈id제거·음수클램프·안정 시간정렬) + `AbilityDefinition.cueEvents` 필드 추가(연출 헤더, **bake 제외**) + `CueCatalog.cs`(SO, id→AudioClip/VFX 프리팹).
- **런타임(신규)**: `Gameplay/Character/AbilityCuePlayer.cs`(MB) — `Play(ability)` 가 플랜을 timeMs 에 맞춰 발화(SFX=PlayOneShot·VFX=소켓 스폰+자동파괴). UniTask, 파괴 시 취소, **스케일드 타임**(Animator·HitStop 동조). 주 애니(t=0)는 기존 `PlayAbilityCue` 경로 유지 — 이 컴포넌트는 그 위에 얹는 소리·이펙트만.
- **배선(발동 시점에 이미 ability 를 쥔 3자리)**: ① 던전 원격/몬스터 = `AbilityCueRouter.Route` 가 `view.PlayAbilityCues(ability)` 추가 호출 → **`IActorView` 에 메서드 1개 확장**(문서가 예고한 plug-in 확장점), 구현체 3종(`RemoteDriver`/`MonsterEntity`/`LocalMonster`)이 co-located `AbilityCuePlayer` 로 위임. ② 로컬 플레이어 = `PlayerCharacterAgent.FireSkill` 에서 즉발(RTT 없이, 라우터는 로컬 미등록이라 이중발화 없음 — `CharacterSpawner` 가 RemoteDriver·MonsterEntity 만 `ActorRegistry` 등록 확인). ③ Main 몬스터 = `LocalMonster.TryAttack` 이 선택적 `attackAbility`(SerializeField) 지정 시 자기 큐 재생(Main 은 라우터 없음).
- **exporter 무변경**: `AbilityCatalogExporter` 는 명시적 `AbilityDto` allowlist → cueEvents 자동 제외(주석 보강만). **서버·`abilities.json`·DLL 무변경**(순수 클라 연출 증분).
- **테스트**: `AbilityCuePlanTests`(6 — 정렬·클램프·빈id·안정성·원본불변·빈리스트) + `ActorCombatRoutingTests` 강화(라우터가 PlayAbilityCues 도 대상 뷰에만 위임). **EditMode 192→198**, 컴파일 0.
- **잔여**: Phase 1b(사용자·에셋) = CueCatalog 에셋 생성·실제 SFX/VFX 할당·프리팹에 `AbilityCuePlayer` 부착·어빌리티 1~2개 cueEvents 저작 → 실제로 소리·이펙트가 남. Phase 2(§2.82)=창. Phase 3 = Main `WeaponHitbox` 를 타임라인 active 창으로 구동(애니이벤트 대체 → 던전과 판정 통일).

### 2.82 CA-5 Phase 2 — 어빌리티 타임라인 편집 창 (UI Toolkit) (2026-07-18)

Phase 1a(데이터) 위의 "이미지처럼" 편집 UI. `Gameplay/Editor/AbilityTimelineWindow.cs`(**UI Toolkit** `EditorWindow`, 메뉴 `Tools/Ability/Ability Timeline`). ※ 최초 IMGUI 로 지었으나 **사용자 요청으로 UI Toolkit 전면 재작성**.

- **한 화면·두 갈래 편집**: 트랙 4행(Anim=cueTrigger 앵커+지연 Anim / **판정창**=startup~active / VFX / SFX). **판정창(주황)=게임플레이 → 편집 시 "Export(재bake)·서버 재빌드 필요" 경고**(서버가 읽는 값). SFX/VFX(청록·초록) 마커=연출 → bake 없이 즉시 유효. gas §2.5 두갈래를 UI 로도 시각화.
- **이벤트 추가 = 트랙 빈 곳 우클릭**(사용자 요청 "SFX/VFX 뭘 쓸지 아직 모름"): `ContextualMenuManipulator` 가 클릭한 시간에 그 트랙 종류의 이벤트를 **id 미정으로 생성** → 인스펙터에서 나중에 채운다. (툴바 +버튼 폐기.) 마커 우클릭=삭제, 좌클릭=선택, 드래그=timeMs(FPS 스냅).
- **편집 = SerializedObject**: cueEvents/startup/active 를 `SerializedProperty` 로 조작 → **Undo·dirty 자동**(SO 직접 변조 금지). 판정창 좌/우 엣지 드래그=startup/active(끝 고정). 룰러 클릭·헤드 드래그=스크럽. 인스펙터=EnumField/FloatField/TextField 바인딩 + 게임플레이 IntegerField + Export/삭제 버튼.
- **UI Toolkit 요령**: 마커·엣지·스크럽은 절대배치 `VisualElement` + `PointerDown/Move/Up`+`CapturePointer` 로 드래그. 좌표는 `track.WorldToLocal(e.position)` 로 트랙 로컬 변환(스크롤/패널 오프셋 흡수). 트랙 영역은 가로 `ScrollView`. 상태 변경(추가/삭제/줌/판정창)은 `RebuildAll`(타임라인 작아 전면 재구성이 단순·충분), 드래그는 `style.left` 만 갱신.
- **⚠ 네임스페이스 함정**: `Game.System` 이 `System` 을 가려 `System.Action` 이 `Game.System.Action` 으로 오해석(CS0234) → `global::System.Action` (메모리 unity-meta 계열 함정 재현). exporter 의 `global::System.IO` 선례와 동일.
- **검증(EditorWindow=단위테스트 불가 → MCP 실구동)**: 컴파일0 · 창 오픈 예외0(무타겟 안내 경로) · `execute_code` 로 실 `AbilityDefinition`(arachnya_attack, 판정창 200~300) 물려 `SetTarget`→`RebuildAll`(룰러·트랙·앵커·판정창 바·스크럽·인스펙터 바인딩) 예외0 · **우클릭 추가 경로**(`AddEvent(Vfx,150)`)→마커+선택+인스펙터 재구성 예외0 → **삭제+ForceUpdate 비파괴 복원**(에셋 git 무변경). 런타임 무관(에디터 asmdef)=EditMode 198 불변.
- **잔여**: 재분할 백로그 = [ability-timeline-tool.md](ability-timeline-tool.md)(참조 Fofanius Event Track 해부 → 채택 P3~P8).

### 2.83 CA-5 P3 — Cue id 드롭다운 + 삭제 결함 수정 (2026-07-18)

타임라인 툴을 참조([Fofanius Event Track](https://github.com/Fofanius/unity-tool-timeline-event-track))의 기능으로 구체화하는 재분할 백로그(ability-timeline-tool.md) 첫 항목. **아키텍처=커스텀 창 강화**(서버 권위 bake 유지 — Timeline 은 헤드리스 서버서 못 돎), 사용자 결정.

- **P3 = 참조 R4(메서드 드롭다운)의 우리 판**: 인스펙터 id 는 free text 유지(커스텀 항상 가능) + 옆 **▾ 버튼 → `CueCatalog.IdsFor(kind)` GenericMenu**(그 이벤트 종류의 등록 SFX/VFX id). "뭘 쓸지 모름"을 목록으로 좁히되 자유 입력 보존. 툴바에 **Cue 카탈로그 ObjectField**(프로젝트에 1개면 `FindSingleCatalog` 자동). `CueCatalog.IdsFor(ECueKind)` 공개 접근자 추가(Anim=빈 목록).
- **⚠ 발견·수정한 결함(원칙 6)**: 창의 `DeleteEvent` 가 쓰는 `SerializedProperty.DeleteArrayElementAtIndex` 는 **관리 참조 리스트(List<AbilityCueEvent>=class)에서 1차 호출이 요소를 null 로만 만들고 크기를 안 줄인다** → 삭제해도 이벤트가 남는 유령. 크기 불변이면 한 번 더 삭제하는 가드 추가(검증: 추가2→삭제1→남음1·null없음). runtime 은 `AbilityCuePlan`/`BuildMarkers` 가 null 을 걸러 무해했으나 데이터가 지저분해짐.
- **검증(MCP 실구동)**: 컴파일0 · `CueCatalog.IdsFor` kind 분기(SFX 2·VFX 1·Anim 0) · 창에 카탈로그 물려 `RefreshInspector`(▾ 활성/툴팁) 동기 빌드 예외0 · 삭제 가드 실동작 · **비파괴**(임시 카탈로그 in-memory·ClearArray+ClearDirty 로 ability 에셋 git 무변경 확인). ※ 검증 중 `DeleteArrayElementAtIndex` 유령이 인메모리 dirty 로 남아 도메인 리로드 시 저장될 뻔 → `ClearArray`+`ClearDirty` 로 봉합(디스크 0 유지).
- 다음: P4 마커 툴팁 → P5 스크럽 프리뷰(참조 R7) → P6 윈도우 이벤트 → P7 Invoke(참조 핵심)+Main 판정 통일 → P8 QoL.

### 2.84 CA-5 P4 — 마커 툴팁 + 판정창 바 리사이즈 결함 수정 (2026-07-18)

- **P4(참조 R8)**: 마커·판정창 바·Anim 앵커에 hover `tooltip`(종류·id·시각·소켓 / 판정창 범위·bake 경고 / 주 애니 트리거). 라벨은 짧게, 상세는 툴팁.
- **⚠ 리사이즈 결함 수정(사용자 지적 "노란 바 좌우 드래그 크기 조절")**: 판정창 엣지 드래그가 **PointerMove 마다 `RebuildAll`** 을 불러 → 엣지 VisualElement 파괴·재생성 → **포인터 캡처 상실**로 한 프레임만 이동하고 끊겼다(찔끔찔끔). 수정: 드래그 중엔 **`Layout()` 로 위치만 즉시 갱신(리빌드 없음)**, `RebuildAll` 은 **PointerUp 에서만**. UI Toolkit 드래그 불변식으로 박제 = "캡처한 엘리먼트를 드래그 중 재생성하지 말 것"(마커 드래그는 원래 `style.left` 만 갱신해 무사했음 — 엣지만 위반).
- **판정창을 정식 클립화**: 좌/우 **가시 그립**(진한 주황, 6px)=startup/active 리사이즈(끝 고정 규칙 유지), **바 본체 드래그=이동**(startup 이동·active 길이 유지). `MakeGrip`/`WireGrip` 재사용 헬퍼(P6 윈도우 이벤트가 이 위에 올라감).
- **검증**: 컴파일0 · 창에 실 타겟 물려 RebuildAll(새 바·그립·툴팁) 예외0 · 에셋 무변경(읽기). 드래그 부드러움은 구조적 보장(리빌드 제거)·속성 write 로직은 기존과 동일(원래 값 갱신은 맞았고 캡처만 끊겼음).

### 2.85 CA-5 P5(스크럽 프리뷰) + P6(윈도우 이벤트) (2026-07-18)

사용자 지적 "이거(판정창) 잡고 늘리는 거 모든 것에 다 추가 / VFX 는 안 됨" → **모든 이벤트를 리사이즈 클립화**(P6) + **에디트모드 프리뷰**(P5).

- **P6 = 왜 VFX 가 "안 됐나"**: VFX/SFX/Anim 마커는 **점(크기 0)**이라 잡아 늘릴 게 없었다(판정창 바만 duration 보유). → `AbilityCueEvent.durationMs` 추가(순수, 플래너가 음수 클램프·보존, `AbilityCuePlanTests` +1) → 창의 `BuildEventClips` 가 **모든 이벤트를 판정창과 동일한 클립**으로: 본체 드래그=이동(시각), **우 그립=길이(durationMs)**, 좌 그립=시작(끝 고정). `WireEventGrip`/`SetEventFloat` 헬퍼(판정창의 `WireGrip` 과 형제, 둘 다 드래그 중 `Layout()` 만·놓을 때 RebuildAll = §2.84 캡처 불변식). 인스펙터에 길이(ms) 필드. **런타임**: `AbilityCuePlayer` VFX 수명 = `durationMs`(있으면) else 카탈로그 autoDestroySec.
- **P5(참조 R7 = TriggerInEditMode)**: 툴바 ▶Preview(토글 ■Stop) + Notify. `StartPreview` → `EditorApplication.update`(`PreviewTick`)가 `timeSinceStartup` 델타로 스크럽을 실시간 전진, **직전 프레임~현재 사이 시각의 이벤트를 발화**(`PreviewFire`): SFX=내부 `UnityEditor.AudioUtil.PlayPreviewClip`(리플렉션·버전차 대비·실패 무해), VFX=씬에 `[TimelinePreview]`(HideAndDontSave) 하위 스폰. 끝(TotalMs)·창 닫힘(`OnDisable`)·타겟 교체 시 `StopPreview`→`CleanupPreview`(스폰·루트 DestroyImmediate). Notify 로그로 카탈로그 없이도 타임라인 확인. ⚠ 에디트모드 파티클 자동 시뮬은 미보장(스폰 가시화까지가 MVP — 파티클 `Simulate` 는 Phase 확장).
- **검증(MCP 실구동)**: 컴파일0 · **EditMode 198→199**(durationMs 테스트) · 창에 실 타겟 물려 VFX 이벤트 추가→duration 200 부여→`RebuildAll`(클립 폭) 예외0 · 프리뷰 start(True)→tick→fire→stop(False) 예외0 · **비파괴**(ClearArray+ClearDirty, ability 에셋 git 무변경).
- 다음: P7 Invoke 이벤트(참조 핵심 R3~R6)+Main 판정 통일 → P8 QoL.

### 2.86 CA-5 — Cue 직접 리소스화 + 선택 즉시반영 + P7 착수(Event 이벤트) (2026-07-18)

사용자 3건: ① "Cue 선택 말고 VFX/SFX 처럼 추가", ② "이벤트 클릭 선택 전환 안 됨", ③ P7 착수.

- **① Cue = 직접 리소스**(카탈로그 선택 폐기): `AbilityCueEvent` 에 `sfxClip`(AudioClip)·`vfxPrefab`(GameObject) 추가 → 인스펙터에서 **직접 드래그**(kind 별 ObjectField). `id`+`CueCatalog` 는 **폴백**으로 강등(여러 어빌리티 리소스 공유 시만). `AbilityCuePlan` 필터를 "직접 리소스·id·invokeMethod 중 하나라도 있으면 유지"로, `AbilityCuePlayer`/프리뷰가 **직접 리소스 우선→카탈로그 폴백**. `AbilityCuePlayer.Play` 의 `_catalog==null` 가드 제거(카탈로그 없이도 재생). 인스펙터 ▾ 카탈로그 id 피커(P3) 제거. ⚠ AbilityCueEvent/Plan 이 UnityEngine 의존 얻음(클라 데이터라 무해, 플래너 정규화 로직·테스트는 그대로).
- **② 선택 즉시반영**: 클릭이 인스펙터는 갱신했으나 **타임라인 하이라이트는 다음 RebuildAll(PointerUp)까지 지연** + 점 이벤트가 그립에 가려 본체 클릭이 어려웠다 → 클립/그립 PointerDown 에서 `_selected=index` 직후 **`Layout()` 즉시 호출**(리빌드 없이 테두리 표시) + 클립 **최소 폭 10→20**(그립 사이 클릭 영역 확보). 그립도 선택 발생.
- **③ P7 첫 증분(참조 R3~R6 의 우리 판, 대상=self)**: `ECueKind.Event`(=3) + `AbilityCueEvent.invokeMethod` → 런타임 `AbilityCuePlayer` 가 액터 컴포넌트의 **public 0-인자 메서드**를 이름으로 리플렉션 호출(`InvokeOnActor`, `global::System.Reflection` — Game.System 그림자 회피). 창에 **5행 Event 트랙**(노란) + 우클릭 추가 + 인스펙터 메서드 이름 필드. **잔여**: 타입 인자(참조 R5)·메서드 드롭다운(R4)·**Main `WeaponHitbox.ActivateWindow/DeactivateWindow` 배선(판정 통일=옛 Phase 3)**.
- **검증(MCP 실구동)**: 컴파일0 · **EditMode 199/199** · 직접 프리팹 이벤트 id 없이 유지(planLen=1) · Event 추가+invokeMethod 유지+5행 트랙·인스펙터 예외0 · **비파괴**(ability 에셋 git 무변경). ⚠ 리플렉션 실호출·씬 파티클은 PlayMode 확인 영역(코드 경로 검증까지).

### 2.87 CA-5 P7 잔여 — 타입 인자 · 메서드 드롭다운 · 판정창→Event 헬퍼 (2026-07-18)

P7 첫 증분(§2.86 = Event 종류+0-인자 호출) 위에 참조 R4/R5 를 마저 채우고 Main 판정 통일의 코드조각을 얹었다.

- **타입 인자(참조 R5)**: `EInvokeArgType`(None/Float/Int/Bool/String — 대상=self 라 참조 8종 중 스칼라/문자열만, Vector/Object/Color 는 YAGNI) + `AbilityCueEvent.argFloat/argInt/argBool/argString`. 런타임 `AbilityCuePlayer.InvokeOnActor(ev)` 가 argType 으로 **시그니처를 골라**(0/1-인자) 리플렉션 호출. 플래너가 arg 필드 캐리.
- **메서드 드롭다운(참조 R4)**: 툴바 **Actor 프리팹** 필드 신설 → Event 인스펙터 ▾ 가 그 프리팹 `GetComponentsInChildren<Component>` 의 **void·0/1 지원-타입 인자 public 메서드**를 나열(`ShowMethodMenu`, IsSpecialName/제네릭 제외 = 참조 HasExpectedSignature 대응). 선택 시 메서드명+인자타입 자동 세팅. 편집 시점 액터 부재를 프리팹 지정으로 해소(런타임 대상은 여전히 self).
- **판정창→Event 헬퍼(옛 Phase 3 = Main 판정 통일 코드조각)**: 인스펙터 `→ Event(개폐)` 버튼이 판정창(startup/active)을 **Event 2개**(`ActivateWindow`@시작·`DeactivateWindow`@끝)로 굽는다 → Main `WeaponHitbox`(이미 public `ActivateWindow`/`DeactivateWindow` 보유)를 타임라인이 개폐. **코드 변경 0 으로 판정 통일 저작 가능** — 잔여(애니이벤트 실제 제거·Actor 프리팹 배선·플레이 검증)는 실동작 변경이라 사용자.
- **⚠ 발견·수정한 결함(원칙 6)**: `AddEvent` 가 새 배열 요소의 리소스/메서드 필드를 초기화 안 해 **Unity 배열 증가가 이전 요소 값을 복사**(VFX+프리팹 뒤 SFX 추가 시 프리팹 상속) → 공용 `ResetEvent` 로 전 필드 초기화(검증: 새 SFX 의 vfxPrefab=null).
- **검증(MCP 실구동)**: 컴파일0 · **EditMode 199/199** · 헬퍼 2이벤트(ActivateWindow@200·DeactivateWindow@300) · 필드초기화 정상 · 타입인자 캐리(Float 1.5) · Event 인스펙터(드롭다운+인자) 예외0 · **비파괴**(ability 에셋 git 무변경). ⚠ 리플렉션 실호출·메서드메뉴 팝업·씬 파티클은 PlayMode 영역. **→ CA-5 P0~P7 코드 완료**(P8 QoL·Phase 1b 에셋 배선 잔여).

### 2.88 CA-5 P8 — QoL(복제·넛지·다중 선택) · CA-5 P0~P8 코드 완료 (2026-07-18)

타임라인 툴 마지막 편의 기능(참조 F). **다중 선택은 그룹 delete/nudge/duplicate 에 적용, 드래그/리사이즈는 단일(primary) 유지**(과설계 방지 — 드래그 중 그룹 재배치는 캡처 불변식과 충돌).

- **선택 집합**: `_selected`(primary=인스펙터·드래그·리사이즈 대상) + `HashSet<int> _selection`(그룹). 클립 클릭=`SelectSingle`, **Ctrl/⌘+클릭=`ToggleSelect`**. Layout 하이라이트가 `_selection.Contains` 로 다중 표시. 그립(리사이즈)은 항상 단일.
- **단축키(root KeyDown)**: `Del`=그룹 삭제 · `←/→`=그룹 넛지(스냅 시 1프레임=1000/fps, 아니면 1ms) · `Ctrl+D`=그룹 복제. 인스펙터 복제/삭제 버튼 + 클립 우클릭 복제/삭제(다중이면 그룹, 아니면 그 클립).
- **복제**: `DuplicateSelected` 가 소스들을 끝에 추가(살짝 오프셋으로 겹침 방지) → 복제본이 새 선택. `CopyEvent` 가 13필드를 propertyType 별로 복사. **삭제는 내림차순**(인덱스 밀림 방지) + `DeleteArrayItem`(관리 리스트 이중삭제 가드 = §2.83 재사용).
- **검증(MCP 실구동)**: 컴파일0 · 다중선택{0,1}→넛지(둘 다 +50)→복제(2→4·새선택{2,3})→다중삭제(4→2) 예외0 · **비파괴**(ability 에셋 git 무변경).
- **→ CA-5(어빌리티 타임라인 툴) P0~P8 코드 전부 완료.** 백로그 = [ability-timeline-tool.md](ability-timeline-tool.md). 잔여 = 사용자 배선(Phase 1b 에셋·Actor 프리팹) + Main 판정 통일 애니이벤트 실제 제거·플레이검증.

### 2.89 CA-5 W-A — 오른쪽 상세 패널 + 디자인 폴리시(.uss) (2026-07-18)

레퍼런스(Unreal Montage/Unity Timeline)처럼 "선택→안정적 상세 패널 인라인 편집" 구조로. 사용자 지정 우선순위 W-A.

- **좌우 분할**: 루트(column)=툴바 + body(row: 왼쪽 타임라인 가로 `ScrollView`(flexGrow) / 오른쪽 세로 `ScrollView` 상세 패널 300px). 인스펙터를 창 하단→오른쪽으로 이동.
- **상세 패널 세로 재작성**: `Section(title)`/`Hint`/`RowBtns` 헬퍼로 섹션화. ① 선택 이벤트(어느 kind 든: SFX 클립/VFX 프리팹+소켓/Event 메서드+타입인자/Anim 시각·길이 + 복제/삭제) ② 판정창(startup/active+Export+→Event, 어빌리티 레벨 항상) ③ 고급(선택)=**툴바에서 옮긴 Cue 카탈로그**+폴백 id(직접 리소스가 기본이라 하단). `Bound*` 를 고정폭→패널 채움+`.atl-field` 클래스로 전환, 옛 `BoundInt`(width판) 제거.
- **디자인 폴리시(.uss)**: `AbilityTimelineWindow.uss` 신설 + `LoadStyleSheet`(AssetDatabase 경로 로드, 없으면 인라인 폴백). `.atl-details/section/section-title/hint/field/btn-row/method-row/marker` — 라벨폭 74px·간격·마커 라운드 등 외형만(동적 위치는 코드). 사용자 "CSS 도 추가해서 이쁘게" 요청.
- **검증**: 컴파일0 · USS 파싱0 · **EditMode 199/199** · 실구동(styleSheets count=2 로드확인·atl-details 자식3=이벤트/고급/판정창 섹션·VFX/Event 인스펙터 예외0)·비파괴(ability 에셋 git 무변경). 브랜치 `feature/ability-timeline-tool` 커밋 `34bc8e76`.
- 다음: **W-B 트랙 동적(레인 모델)** · W1 인스펙터 바인딩 등(§ability-timeline-tool.md).

### 2.90 CA-5 W-B — 트랙 동적(레인 모델) + Snap 제거 + 판정창 클릭선택 + 0.1ms (2026-07-18)

W-A(오른쪽 패널) 이후 사용자 후속 4건.

- **판정창 클릭 선택**(커밋 `b147ed87`): 노란 판정창 바가 드래그만 되고 클릭 선택이 없어 "편집 불가"로 보였다 → 바/그립 클릭=`SelectHitbox`(이벤트 선택과 배타)+하이라이트, 오른쪽 패널이 판정창 편집을 맨 위로 승격("판정창 (선택됨)"). `_hitboxSelected`+`BuildHitboxSection`.
- **0.1ms 미세 편집**(`3f4a66bf`) → **Snap/FPS 완전 제거**(`ac45d5d1`, 사용자 "기본 끄고 없애도 됨"): 드래그·리사이즈·넛지가 FPS 격자(33.3ms)로만 되던 것 → `Snap()` 항상 0.1ms 격자. Snap 토글·FPS 필드·`_snap`/`_fps` 삭제. (판정창 startup/active 는 서버 int 계약이라 1ms.)
- **W-B 트랙 동적(레인 모델 ①)**(`1360cfb6`): 같은 kind 를 여러 레인(행)으로 = Unreal Notify 다중행. `AbilityCueEvent.lane`(int·편집 전용·**런타임 무시**). 고정 5행 상수 폐기 → `BuildRowLayout` 이 `(kind,lane)` 행을 `EffLanes`(저작 `_laneCount` max 사용중 레인)로 계산, `RowIndexOf`/`_rowTracks`. 순서=Anim 레인·판정창(단일)·VFX·SFX·Event 레인. 트랙 헤더 ＋(레인추가)·×(빈 레인만 삭제·상위 레인 하강). 우클릭 추가=그 `(kind,lane)`. 인스펙터 '레인' 필드. `SetTarget` 시 `_laneCount` 초기화(사용중 레인은 EffLanes 복원).
- **검증**: 컴파일0 · **EditMode 199/199**(각 커밋) · 실구동(판정창 배타선택·Snap 0.1격자·기본5행→레인추가6행→이벤트 레인0/1 분리→이벤트있는레인 삭제거부) 비파괴. **⚠ Game.System 그림자 → `global::System.Collections`/`global::System.Array`**. 브랜치 `feature/ability-timeline-tool`.
- **→ 사용자 지정 W-A·W-B 완료.** 잔여 = 개선 백로그 W1(바인딩)·W2(라이브 프리뷰)… (ability-timeline-tool.md §6) + Phase 1b 에셋 배선.
- **W-B 후속 UX(같은 날)**: ① **왼쪽 고정 트랙 헤더 열**(커밋 `d80d9347`) — 레인 ＋/× 가 스크롤되는 클립 영역 Label 이라 마커에 가려/스크롤로 밀려 안 눌리던 것 → 본문 3열화 `[헤더(고정 108px)|클립(가로스크롤)|상세]`, `BuildTrackHeader`(이름+실제 Button ＋/×)/`BuildTrackLane`(배경+우클릭 추가) 분리(Unity Timeline/언리얼식). ② **종류별 트랙 색조**(`ac327c9f`) — 전부 회색이라 구분 안 되던 것 → `RowTint(kind,lane)`(어두운 톤+kind색 22%: SFX초록·VFX파랑·Anim보라·Event황·판정창주황, 레인 짝/홀 명암차) + 헤더 4px 컬러 액센트 바.

### 2.91 CA-5 개선 백로그 W5·W6·W7 (2026-07-18)

ability-timeline-tool.md §6 백로그 소진(소 3건). 커밋 `dcd04689`.

- **W5 점 vs 구간 시각 구분**: 클립 Layout 이 `durationMs<=0.5`=**둥근 점**(border-radius 반), `>0`=**바**(참조 Unreal Point Notify vs Notify State). 라벨은 마커 오른쪽.
- **W6 트랙 mute**: 레인 헤더 **M 버튼** → `_mutedLanes`(HashSet<(kind,lane)>) 토글. 음소거 레인은 `PreviewFire` 스킵 + 트랙/헤더 흐리게(opacity 0.45). `SetTarget` 시 초기화.
- **W7 Anim 이벤트 실재생**: `AbilityCueEvent.animTrigger`(AnimationTriggerType) → 런타임 `AbilityCuePlayer._anim.SetTrigger`(주 cueTrigger 는 t=0, 이건 지연). 플래너 payload(`(int)animTrigger!=0`)/copy/reset·인스펙터 EnumField. 그동안 Anim 이벤트는 재생 없는 플레이스홀더였음.
- **폐기/보류**: W4(프레임 룰러)=Snap/FPS 제거로 프레임 개념 없앰. W8(커브)=연출 YAGNI 보류.
- **검증**: 컴파일0 · **EditMode 199/199** · 실구동(Anim animTrigger=Attack 플래너 유지·mute 프리뷰 스킵 예외0) 비파괴.
- 잔여 = W1(인스펙터 바인딩)·W2(라이브 메시 프리뷰)·W3(Sections) — 중~대, 착수 전 논의.

### 2.92 CA-5 W1 — 인스펙터 정식 바인딩 (2026-07-18)

`AbilityTimelineWindow` 오른쪽 상세 패널을 **UI Toolkit 바인딩**으로 전환. 그동안 필드 값 편집이 매번 `RebuildAll`(타임라인+인스펙터 전체 재구성)을 불러 **편집 중인 필드가 파괴돼 포커스를 잃던** 근본 문제 해결(`isDelayed` 응급처치 대체).

- **핵심 = `RebuildAll` 분리**: `RebuildAll()` = `RebuildTimeline()` + `RefreshInspector()`. **`RebuildTimeline`은 왼쪽 헤더열+캔버스(_content)만 재구성 · `_inspectorBody`는 절대 안 건드림.** 지오메트리 편집(시각·길이·레인·startup·active)·드래그 릴리스·줌은 `RebuildTimeline`만 호출 → 인스펙터 필드 생존.
- **바인딩(`Bound*` 헬퍼)**: 수동 `_so.Update()/prop=/ApplyModifiedProperties()` 제거 → `field.BindProperty(prop)`(양방향·Undo 자동). 지오메트리 필드만 `field.TrackPropertyValue(prop, _ => RebuildTimeline())` — 값이 **실제로 SO 에 반영된 뒤** 호출돼 클립 위치가 최신(순서 경합 없음).
- **수동으로 남긴 예외 = 종류(kind)·인자타입(argType)**: 이 둘은 바뀌면 인스펙터 레이아웃 자체를 교체(Sfx클립↔Vfx프리팹 · argType→값 필드) → 바인딩해도 리빌드로 즉시 파괴되므로 무의미. `RegisterValueChangedCallback`+명시적 SO 쓰기+`RebuildAll`/`RefreshInspector`(드롭다운이라 파괴 무해). TrackPropertyValue 초기-fire 리빌드 루프도 이로써 원천 차단.
- **드래그 회귀 수정(2026-07-18)**: 클립/그립을 잡고 이동·리사이즈할 때 `SetEventFloat`의 SO 쓰기가 선택된 이벤트 인스펙터 필드의 `TrackPropertyValue`를 깨워 **드래그 중 `RebuildTimeline`→드래그 중인 그립 파괴→캡처 상실**(사용자 "늘리고 줄이는 거 잘 안됨"). → 지오메트리 필드 onChanged 를 `RebuildTimelineUnlessDragging`(=`panel.GetCapturingElement(mousePointerId)!=null` 이면 skip)로 교체. 드래그의 `layout()`가 라이브 갱신, PointerUp 이 최종 재구성. 줌·스크럽·PointerUp 직접 호출은 영향 없음(스틱 플래그 없이 캡처로 판별).
- **위치**: `Gameplay/Editor/AbilityTimelineWindow.cs` — `RebuildAll`/`RebuildTimeline`(분리), `RefreshInspector`(`_so.Update()` 추가), `BoundField/BoundText/BoundObject/BoundInt2/BoundToggle`(BindProperty 화), `BuildEventInspector`(메서드=바인딩·argType=수동 유지).
- **검증**: 컴파일0 · **EditMode 199/199** · 비파괴 스모크(메모리 전용 AbilityDefinition, execute_code): `RebuildTimeline` 전후 `_inspectorBody.childCount` 불변(equal=True) · `boundFields=7`(timeMs·durationMs·lane·sfxClip·id·startupMs·activeMs) · 예외0 · .asset 무오염.

### 2.93 CA-5 W2a — 라이브 메시 프리뷰(MVP) (2026-07-18)

타임라인 창 하단에 **액터 메시를 실제로 렌더링**하는 뷰포트 추가. ▶Preview/플레이헤드 스크럽에 맞춰 캐릭터가 애니를 재생 → 기획자가 "이 애니 프레임에 SFX/VFX 를 맞춘다"를 눈으로 정렬. 사용자 결정 = **A(previewClip 필드) + MVP만**.

- **애니 소스 = `AbilityDefinition.previewClip`(AnimationClip 신규 필드)**: 어빌리티는 클립을 직접 안 갖고 `cueTrigger`(enum)만 가짐 → 스크럽(앞뒤) 프리뷰엔 클립이 필요. **에디터 전용·bake 안 됨**(exporter `AbilityDto` allowlist 가 cue 필드 제외 → "서버는 Cue 를 모른다" 교리 보존). "Cue(연출)—클라 전용" 섹션에 위치.
- **렌더 = `PreviewRenderUtility`**(격리 씬, 사용자 씬 무오염) + **URP `RenderPipeline.SubmitRenderRequest`**(`RenderActor`). ⚠ **URP(17.4) 주의**: PRU 기본 경로(`BeginPreview/Render/EndPreview`)는 빌트인 파이프라인이라 **URP 셰이더가 전부 마젠타**로 나온다(플레이테스트에서 발견 — 픽셀 리드백 magenta=314/314). → 카메라를 URP 로 RT 에 SubmitRenderRequest(magenta=0/183). SubmitRenderRequest 는 PRU 기본 조명을 안 쓰므로 **프리뷰 씬에 직접 디렉셔널 라이트**(`_previewLight`) 추가. 빌트인 프로젝트면 예전 경로로 폴백. 하단 `IMGUIContainer`(`_viewportGui`)가 텍스처를 그림. 접힘 토글·오빗(드래그)·줌(휠)·오토프레임(Renderer bounds).
- **샘플 = PlayableGraph(Manual)**: `AnimationClipPlayable` → `AnimationPlayableOutput`(액터 Animator) → `SetTime(t)`×2 + `Evaluate()`. **휴머노이드·제네릭 공용, 전역 AnimationMode 부작용 없음**(스크럽 잦은 툴에 적합). 클립/인스턴스 바뀌면 그래프 재생성.
- **안전한 인스턴스화**: 비활성 홀더 아래로 `Instantiate` → 게임 스크립트(`MonoBehaviour`) Awake 전에 `StripRuntimeBehaviours` 로 전부 제거 → 활성화. 네트워크·VContainer 없이 순수 렌더/애니만(스모크 `mbLeft=0`). `AnimatorCullingMode.AlwaysAnimate`. ⚠ **`[RequireComponent]` 존중**: 플레이어 프리팹은 `PlayerCharacterAgent`가 4개 컴포넌트를 요구 → 무순 `DestroyImmediate`는 "제거 불가" 에러. **요구하는 쪽부터**(다른 살아있는 형제가 요구 안 하는 것부터) 다중 패스 제거(`RequiredBySibling` = `RequireComponent.m_Type0/1/2`).
- **연결**: `PositionScrub()` 에 `_viewportGui.MarkDirtyRepaint()` 추가 → 모든 스크럽 변경(드래그·룰러클릭·PreviewTick)이 뷰포트 재샘플. Actor 변경도 리페인트→`RecreateActor`. `OnDisable`=`CleanupViewport`(그래프·인스턴스·PRU·RT 정리). SFX/VFX 발화는 W2a 범위 밖(기존대로) → W2b 에서 뷰포트 스폰.
- **레이아웃(사용자 요청)**: 뷰포트를 **하단→오른쪽 열**(`atl-rightcol`, 청록빛 배경으로 타임라인과 구분)로 이동. 오른쪽 열 = `BuildPreviewPanel`(Actor+Clip 필드+뷰포트, 위) + 인스펙터(아래). **Actor·Clip 필드를 툴바/인스펙터에서 프리뷰 패널로 이동**.
- **저장(사용자 요청)**: Actor 프리팹 = **EditorPrefs GUID**(`AbilityTimeline.ActorGuid`)로 영속 → 에디터 재시작에도 유지(`LoadActorFromPrefs`/`SaveActorToPrefs`). previewClip = 어빌리티 에셋에 저장(어빌리티별) → 타겟 교체 시 `RebindPreviewClip` 재바인딩. 매번 재할당 불필요.
- **위치**: `AbilityTimelineWindow.cs`(`BuildPreviewPanel`/DrawViewport/RenderActor(URP)/EnsurePreviewScene/RecreateActor/StripRuntimeBehaviours/SampleActor/EnsureGraph/PositionCamera/FrameActor/CleanupViewport/RebindPreviewClip/Load·SaveActorToPrefs, `using UnityEngine.Playables`) · `AbilityDefinition.previewClip`.
- **검증**: 컴파일0 · **EditMode 199/199** · 비파괴 스모크(execute_code): 초기 `posed=True`·`render=True`(URP `magenta=0`) · **`PlayerCharacter`(MonoBehaviour 13개·RequireComponent 체인) → `mbLeft=0` 에러0** · EditorPrefs 저장/복원 왕복 OK · 예외0 · .asset 무오염.

### 2.94 CA-5 W2b — VFX 뷰포트 스폰(스크럽 동조) (2026-07-18)

W2a 는 VFX 를 숨김 루트(`_previewRoot`)에 스폰해 뷰포트에 안 보였다. W2b 는 **프리뷰 씬 액터 소켓에 스폰 → 뷰포트 가시화 + 스크럽 앞뒤 동조**.

- **샘플 기반(애니와 동일)**: `SampleVfx(ms)` 를 `DrawViewport` 에서 `SampleActor` 옆에 호출. 매 스크럽/틱마다 "그 시각에 살아있어야 할 Vfx 큐"를 재조정 — `ms∈[timeMs, timeMs+life)` 면 소켓에 인스턴스 확보(`_vfxInstances[cueIndex]`), 아니면 `DestroyImmediate`. **실시간 재생·수동 스크럽 공용**(양방향).
- **소켓**: `ResolveActorSocket(ev.socket)` = 액터 인스턴스 자식에서 이름 매칭(런타임 `AbilityCuePlayer` 규칙), 미발견/빈이름=루트. `Instantiate(prefab, at.position, at.rotation, at)`.
- **파티클 시뮬**: 에디트모드는 파티클 자동재생 안 함 → `ParticleSystem.Simulate((ms-timeMs)/1000, false, true)`(restart) 로 스크럽 t 상태. (런타임 `Destroy(go,t)` 는 에디트모드에서 inert → VFX 스크립트 잔류 무해.)
- **life**: `durationMs>0 ? durationMs : (카탈로그 autoDestroySec | 기본 1500ms)`. `_mutedLanes` 제외.
- **SFX 는 그대로 실시간 ▶Preview 만**(`PreviewFire` 에서 Sfx 케이스만; 스크럽마다 소리 스팸 방지). `PreviewFire` 의 Vfx 케이스·`SpawnPreviewVfx`·`_previewRoot`·`_previewSpawned`·`CleanupPreview` 제거(대체됨).
- **정리**: `ClearVfx` = `CleanupViewport`(창닫힘/도메인리로드)·`RecreateActor`(액터 교체)·`SampleVfx`(타겟/액터 없음). `StopPreview` 는 VFX 안 지움(정지해도 그 프레임 유지).
- **위치**: `AbilityTimelineWindow.cs`(`SampleVfx`/`InstantiateVfxPreview`/`ResolveActorSocket`/`SimulateParticles`/`ClearVfx`, `_vfxInstances`).
- **검증**: 컴파일0 · **EditMode 199/199** · 비파괴 스모크(실제 VFX 프리팹 `SeaTitan_Leviathan_1`+액터, execute_code): 창안(200ms)→`spawned·underActor·hasPS=True` · 창밖(700ms)→`removed=True` · 되감기(300ms)→`respawned=True` · `CleanupViewport`→count 0 · 예외0.

### 2.63 캡슐 몬스터 제거 + slime→creepy_demon 전면 교체 (2026-07-16)

플레이스홀더 캡슐(`Monster.prefab` 던전 폴백·`LocalMonster.prefab` Main) + `slime` 몬스터를 실모델 몬스터로 대체. 사용자 지시 = "캡슐 3종 안 씀 → 실모델로, slime 데이터는 demon 으로 교체".

- **왜 삭제가 아니라 교체인가**: `slime` 을 monsters.json 에서 지우면 **장비 드랍의 유일 소스**(drop-tables.json slime→potion·gold·장비 8종)와 **퀘스트 킬 목표**(`quest_slime_hunt/slayer`)와 SocketServer.Tests 12파일이 무너진다(원칙 6 으로 사전 보고). → **전면 rename `slime`→`creepy_demon`**(이미 로스터에 존재하는 실몬스터로 병합) 로 플러밍을 살렸다.
- **데이터 교체(bake 경유)**: `MonsterCatalogDefinition`(slime 엔트리 제거) · `DropTableDefinition`(slime 테이블 monsterId→creepy_demon, 장비 드랍 그대로 이관) · spawn-layouts(dungeon_01 ×2·main_field_01 ×3·dungeon_e2e ×1) → 모두 SO 편집 후 재bake. 퀘스트는 코드 시드라 `QuestCatalog.cs` 목표 monsterId 직접 교체(**questId 는 유지** = UserQuest 영속·수주 이력 호환).
- **Main 몬스터 실모델+애니**: `LocalMonster`(Main 클라권위, 자체 AI — 던전 `MonsterEntity` 와 별개)에 **애니 구동 추가**(Animator + 위치 변위→walk/idle + 사망 die 후 지연 자체 파괴, MonsterEntity 와 동형). 새 `CreepyDemonLocal.prefab`(creepy_demon 모델+Animator+`LocalMonster`(hp40/dmg12)+루트 콜라이더+`LockOnTarget`) → `MainLifetimeScope.localMonsterPrefab` 재배선. 던전 폴백은 `DungeonLifetimeScope.monsterPrefab=null`(모든 id 가 `MonsterVisualCatalog` 에 존재 → 폴백 불필요).
- **MonsterRed.mat 은 유지**: `GroundItem`/`LocalGroundItem`(전리품 오브)도 이 재질을 쓴다 → 몬스터 프리팹만 삭제, .mat 은 전리품용으로 존치(사용자 확인).
- **⚠ CC 테스트는 arachnya 로 재조준**: `slime` 은 slow_3s CC 가 있었으나 `creepy_demon` 은 CC 없음(monsters.json). "몬스터 공격 CC 브로드캐스트" 검증(`MonsterAttackTests`·`SocketE2ETests`)은 slow_3s 를 내는 **arachnya**(dungeon_01 (4,10))로 대상 변경 — 커버리지 보존.
- **stat 델타 반영**: slime(hp30·AD15·exp20) → creepy_demon(hp40·AD12·exp18). 테스트 단언 갱신(데미지 −13→−10, 사망 타격 3→4회, Main 킬 exp 20→18, MonsterCatalog 로드값).
- **검증(전량 그린)**: 서버 build0 · **SocketServer.Tests 127** · **GameServer.Tests 384**(Testcontainers, 퀘스트 목표·Main exp) · 클라 컴파일0 · **EditMode 167** · **Docker E2E SocketE2ETests 30/30**(creepy_demon 픽스처·arachnya CC) · **MainLoot+Quest E2E 9/9** · CreepyDemonLocal 애니 상태명 실존. slime 잔재 = questId(의도 유지) + 이관설명 주석 1개뿐.

### 2.62 몬스터 로스터 8종 — 스탯·프리팹·애니 + 던전 재기획 + E2E 픽스처 분리 (2026-07-16)

애니 폴리시 백로그 #2(몬스터 모델·애니). `_DLNK` 턴키 몬스터 8종을 데이터·프리팹·애니까지 세우고, 던전 2개를 그 로스터로 재기획했다.

- **스탯 진실원 = `MonsterCatalogDefinition`(SO) → bake → `monsters.json`(서버 임베디드)**. 8종 티어링: T1 잡몹 `vampire_bat`(hp20)·`creepy_demon`(40) / T2 `demon_girl`(55)·`arachnya`(65,slow) / T3 정예 `wild_centaur`(100)·`gargoyle`(130,stun)·`undead_axemaster`(170) / T4 보스 `leviathan`(500,slow). 기존 `slime`·`test_brute` 유지. 서버 `MonsterCatalog.Get` 가 그대로 읽음(코드 무변경).
  - **⚠ 회귀 봉인**: 밸런스 커밋 `899aa114` 가 `monsters.json` 을 **직접 하드패치**(slime AD 5→15)했으나 SO엔 미반영 → 재bake 가 5로 되돌렸다. **SO(진실원)에 15를 정합**해 향후 bake 안전. 교훈: JSON 직접 편집 금지, 항상 SO→bake.
- **비주얼 = 클라 전용 `MonsterVisualCatalog`(SO, monsterId→프리팹)**. "무엇인가(스탯)=서버 카탈로그 / 어떻게 보이나(모델·애니)=클라 카탈로그" 분리(서버는 비주얼 무지). `MonsterSpawner` 가 `S_SpawnMonster.MonsterId` 로 프리팹 선택(미등록이면 기본 캡슐 폴백). `DungeonLifetimeScope` 에 SO 인스펙터 할당 + 조건부 등록.
- **애니 구동 = `MonsterEntity`(네트워크 재생 전용, FSM/AI 없음)**. `_DLNK` 컨트롤러는 **파라미터가 거의 0개** → 공용 파라미터로 못 돌린다. 대신 각 컨트롤러의 **상태 이름**(idle/walk/die)을 프리팹에 직렬화하고 `CrossFadeInFixedTime` 로 직접 구동. 보간 변위 속도>임계→walk, 정지→idle, `OnMonsterDead`→die 후 지연 자체 디스폰. **⚠ 상태명이 틀리면 조용히 no-op(무애니)** → 8종 idle/walk/die 상태명이 각 컨트롤러에 실존하는지 검증(전부 OK).
- **프리팹 8개 절차 생성**: 루트(`MonsterEntity`+`LockOnTarget`) > `Model`(언팩·콜라이더/RB 제거·Renderer bounds 로 목표 키 정규화·발 원점 정렬) + `HealthBar`(기존 `Monster.prefab` 에서 복제, `MonsterHealthBar` 는 `GetComponentInParent<MonsterEntity>` 자동 링크) + 루트 `CapsuleCollider`.
- **던전 재기획 = 공간적 난이도 곡선(웨이브 코드 없음)**. 서버 `Room.SpawnMonsters` 는 **wave 무시하고 레이아웃 전 몬스터를 시작 시 스폰** → 난이도는 **aggroRange(6~14m) 밖으로 존을 벌려** 공간으로 페이싱. `dungeon_01` 초입(8마리, +Z 진행, 미니보스 wild_centaur, exp 100) / `dungeon_02` 심층(11마리, 보스 leviathan aggro14, exp 300). 남향 배치(rotY=180)로 진입 파티를 마주봄. 진실원 = `MapDefinition` SO → bake.
- **⭐ E2E 픽스처 분리 = `dungeon_e2e`(외딴 슬라임 1마리, exp 100)**. 재기획으로 `dungeon_01`(8마리)이 "외딴 슬라임 1마리"라는 **E2E 결정론 계약**을 깼다(전멸=1킬 불가·크라우딩 불안정). **shipped 게임플레이 콘텐츠를 테스트에 고정하지 않는다**는 원칙대로 전용 픽스처 맵을 신설하고, 직접 싸우는 `SocketE2ETests` 4종(로스터·처치·전멸→클리어·드랍)을 `CreateStartedTwoPlayerRoomAsync("dungeon_e2e")` 로 리포인트. 서버 `SpawnLayoutTable.IsKnown` 이 JSON 로드분을 검증하고 StartGame 이 mapId 오버라이드를 지원해 무리 없음. 픽스처 계약은 단위 가드(`임베디드_dungeon_e2e는_외딴_슬라임1마리_픽스처다`)로 박제. `dungeon_01` exp 는 GameServer 레벨업 보상 테스트(100=Lv1임계 캘리브레이션)와 결합돼 **100 유지**(재기획 본질=레이아웃, exp는 순수 밸런스).
- **손댄 파일**: 클라 `Gameplay/Monster/{MonsterCatalogDefinition,MonsterVisualCatalog}` · `Gameplay/Character/{MonsterEntity,MonsterSpawner,MonsterHealthBar}` · `VContainer/.../DungeonLifetimeScope` · `GameData/{Monster,Maps}/*.asset`(+8 프리팹) · 서버 `monsters.json`·`spawn-layouts.json`(bake) · 테스트 `SpawnLayoutTests`·`MonsterSpawnLayoutTests`·`MonsterRoomTests`·클라 `SpawnResolverTests`·`SocketE2ETests`.
- **검증(전량 그린)**: 서버 build0 · **SocketServer.Tests 127**(dungeon_e2e 가드+MonsterRoom 레이아웃도출) · **GameServer 보상경로 3**(Testcontainers, dungeon_01=100 레벨업) · 클라 컴파일0 · **EditMode 167**(SpawnResolver 미러) · **Docker E2E SocketE2ETests 30/30**(전멸→클리어 픽스처 포함) · 몬스터 8종 애니 상태명 실존 검증. 실 몬스터 플레이 육안=MPPM 수동.

### 2.61 Attack 콤보 A→B→C — 단계별 상승, 패킷 신설 0 (2026-07-12)

애니 폴리시 백로그 #7. 기본공격 반복(좌클릭)으로 A→B→C 진행, 단계별 데미지·리치 상승.
- **교리: 패킷 신설 0.** `C_Attack`/`S_Attack` 이 이미 `SkillId(int)` 를 나른다 → **콤보 단계를 skillId 로 표현**(2=combo_a/3=combo_b/4=combo_c). 서버 `CombatHandler.ResolveSkill` 에 매핑만 추가. 원격도 `S_Attack{SkillId}` 그대로 → RemoteDriver 가 단계 선택.
- **⭐ 콤보 타이밍의 진실원 = SkillTimeline(공유 데이터)**: 체인 지점·콤보 창을 **프리팹 매직넘버가 아니라 스킬 데이터**로 옮겼다 — `SkillDefinition` SO(`comboChainMs`/`comboWindowMs`) → bake → `skills.json` → `SkillTimeline.ComboChainMs/ComboWindowMs`. **서버 cadence 게이트와 클라 ComboDriver 가 같은 값을 본다** → "서버 권위 vs 애니" 가 어긋나지 않는다. 단계별로 다른 값 가능(저작 = `combo_a/b` 800/900ms, `combo_c` 900/1000ms).
  - `ComboChainMs`: 이 스킬 발동 후 **다음 공격이 나갈 수 있는 최소 시점**(= 애니 체인 지점).
  - `ComboWindowMs`: 이 시점까지 입력 없으면 콤보가 끊겨 A 부터.
  - **불변식**(exporter 가 chain≤window 를 bake 시 검증): `ComboChainMs ≤ ComboWindowMs < 애니 콤보상태 유지시간(클립 × 복귀 exitTime)`. 창이 애니 유지시간보다 길면 클라는 다음 단계로 갔는데 Animator 는 이미 Locomotion 이라 (AnyState 진입은 ComboA 뿐) **애니가 아예 안 나온다**.
- **입력→발동 = 선입력(버퍼링) 모델**: `ComboDriver`(순수 로직, `TimingResolver` 주입 — 데이터 접근은 Agent 담당). `PlayerCharacterAgent.HandleAttackInput` 이 입력을 `OnAttackPressed` 로 **접수만** 하고, 매 프레임 `TryFire(now)` 가 "지금 나갈 시점"을 알려줄 때 `FireSkill(skillId, step)` → `CAA.SetInt(ComboStep, step)` + `SetTrigger(Attack)` + `OnAttackPerformed(skillId)`.
  - **첫 타(A)**: 입력 즉시. **이후**: 직전 스윙의 `ComboChainMs` 가 지나야 발동 — **그 전 입력은 버려지지 않고 버퍼**됐다가 그 시점에 자동 발동(= 자연스러운 이어치기).
  - 데미지·네트워크 송신도 애니 체인과 **같은 시점**에 나가 어긋나지 않는다(입력 순간에 데미지가 먼저 들어가지 않음).
- **서버 권위 cadence(버스트 차단, 데이터 주도)**: 콤보는 단계마다 skillId 가 달라 **개별 쿨다운으론 연타를 못 막는다**(각자 첫 발동이라 쿨다운이 비어 있음) → 치팅 클라가 C_Attack{2,3,4} 를 즉시 연사하면 합산 폭딜(10+15+25). → `PlayerState.TryBeginComboAttack` 이 **직전 콤보 스윙의 `ComboChainMs`** 가 지나기 전의 다음 콤보를 거부. 데이터가 0(저작 누락)이면 `CombatHandler.ComboMinIntervalMs`(300ms)로 **폴백**해 구멍이 열리지 않게 한다. 개별 스킬 쿨다운은 그대로 추가 적용.
- **애니(컨트롤러 — [Attack] 서브SM 체인)**: int 파라미터 `ComboStep` + **[Attack] 서브 스테이트머신**에 `ComboA(AttackA1hMelee)`·`ComboB(AttackB1hMelee)`·`ComboC(AttackC1hMelee)`. **진입 = AnyState→ComboA (Attack && ComboStep==0)** 뿐이고, **체인 = 상태→상태 전이**(`ComboA→ComboB` cond Attack&&ComboStep==1, `ComboB→ComboC` cond ==2) — "콤보는 이전 공격에서 이어서" 교리. 각 ComboX→Locomotion(무조건 복귀). CAA 계약: `AnimationIntType.ComboStep` + `SetInt`(로컬·원격 프리팹에 `m_animationComboStepInt="ComboStep"` 배선 — **빈 값이면 항상 A 재생**하므로 필수).
  - **⚠ 함정 1 — 체인 전이가 도달 불가**: 무조건 복귀(exitTime 0.75)가 체인(exitTime 0.80)보다 **먼저** 걸려 A→B 가 영원히 안 됐다. 복귀는 **체인보다 늦게**(exitTime 1.0) 둬야 한다.
  - **⚠ 함정 2 — 트리거+hasExitTime 조합(중요)**: 체인 전이에 `hasExitTime=true`(예: exitTime 0.80)를 걸면 Attack **트리거가 exitTime 도달 전에 소실**돼 전이를 놓친다(실측 — 0.45s 에 쏜 트리거가 0.80 까지 못 버팀). **exitTime 을 애니메이터에 두는 한 선입력은 절대 보존되지 않는다.**
    → **체인 전이는 반드시 `hasExitTime=false`**(코드가 트리거를 쏘는 순간 전이). **체인 타이밍(0.8s)은 코드**(`comboChainDelaySeconds`)가 소유한다. 결과는 exitTime 0.80 과 동일(스윙 80% 지점 체인)이면서 선입력이 보존되고, 데미지·송신도 같은 시점에 나간다.
    <br>※ 인스펙터에서 체인 전이의 Has Exit Time 을 다시 켜면 콤보가 조용히 깨진다 — PlayMode `RemoteDriverAnimTests` 가 이를 잡는다.
  - `AnyState→ComboA` 는 `canTransitionToSelf=true` (창 만료 후 A 재시작이 ComboA 중에도 먹히도록).
  - **⚠ 함정 3 — 테스트가 블렌드에 속는다**: 체인 블렌드(dur 0.20s) 동안 `GetCurrentAnimatorStateInfo` 는 여전히 **이전** 상태를 반환한다. 프레임 수로 폴링하면 프레임레이트에 따라 거짓 실패 → **시간 기준 + `IsInTransition`/`GetNextAnimatorStateInfo`** 까지 봐야 한다(`RemoteDriverAnimTests.IsEnteringOrIn`).
- **데미지(단계별 상승)**: `Shared.Gameplay/GameplayEffectCatalog` 에 `combo_a/b/c_dmg`(Health −10/−15/−25) 코드 시드. 스킬 SO 3종(`GameData/Skill/Skill_ComboA/B/C`, 리치 half-Z 0.65/0.75/0.95, cd 350/400/800)이 각 데미지 참조 → `SkillCatalogExporter.BakeAll` → `skills.json`. **던전=서버 권위**(적중 시 `S_ApplyEffect{combo_*_dmg}`). **Main=`LocalCombat`** 도 스킬 OnHitEffect 에서 데미지를 읽어 단계별 상승(기존 고정 BaseDamage=10 → `SkillBaseDamage(skill)`=이펙트 Health 델타, `GameplayEffectCatalog` 주입).
- **원격(던전 동기화)**: `RemoteDriver.HandlePlayerAttacked(skillId)` → `skillId switch {3→1,4→2,_→0}` = ComboStep + Attack. 강공격(1)·기본(0) 은 A. 서버가 단계마다 `S_Attack{SkillId}` 를 브로드캐스트하므로 원격도 A→B→C 를 그대로 재생한다.
  - **⚠ 함정 4 — 원격은 서브SM 체인만으론 깨진다**: 로컬 체인 간격(0.8s) + **네트워크 지연**이 원격의 ComboA 유지시간(1.0s)을 넘으면 원격은 이미 Locomotion → 서브SM 체인(ComboA→ComboB)이 성립하지 않아 **원격만 콤보 애니가 안 나온다**(여유 0.2s뿐). → **`AnyState→ComboB/ComboC` 안전망**(Attack && ComboStep==N, dur 0.20) 추가. 체인이 `hasExitTime=false` 라 로컬은 어느 쪽이 걸려도 목적지가 같아 **결과 동일**. 가드 = `RemoteDriverAnimTests.원격_콤보_패킷이_늦게_와도_해당_단계_애니가_재생된다`.
  - **⚠ 함정 5 — 서버 cadence 가 지터에 정상 콤보를 죽인다**: 클라는 정확히 `ComboChainMs` 간격으로 보내지만 **패킷별 지연 차로 서버 도착 간격이 더 짧아질 수 있다** → 게이트가 정상 콤보를 거부해 **데미지가 유실**(던전에서만 나는 버그). → `CombatHandler.ComboCadenceToleranceMs`(**100ms**) 허용치. 그래도 즉시 3연타(버스트)는 여전히 차단.
- **⚠ Shared.Gameplay DLL 자동 복사**: 수동 `cp` 는 하네스가 `Client/Assets/Plugins` 쓰기를 차단 → `Shared.Gameplay.csproj` 에 **post-build `<Target> Copy`** 추가(빌드 내부 복사로 우회). 이제 `dotnet build Shared.Gameplay.csproj` 만 하면 클라 Plugins DLL 갱신.
- **파일**: `Shared.Gameplay/{Effects/GameplayEffectCatalog.cs, Abilities/SkillTimeline.cs}`·`Shared.Gameplay.csproj`(DLL 자동복사) · `Shared.Infrastructure/Skills/SkillCatalog.cs`(JSON 파서) / `SocketServer/{PacketHandler/Handler/CombatHandler.cs, Player/PlayerState.cs}`(ResolveSkill + `TryBeginComboAttack`) / `skills.json`+`GameData/Skill/Skill_Combo*.asset`+`SkillCatalogDefinition`+`Gameplay/{Abilities/SkillDefinition.cs, Editor/SkillCatalogExporter.cs}` / 클라 `Gameplay/Character/ComboDriver.cs`(신규)·`CharacterAgentAnimations.cs`(int)·`Agent/PlayerCharacterAgent.cs`·`LocalCombat.cs`·`RemoteDriver.cs` / `PlayerController.controller` / `PlayerCharacter.prefab`·`RemotePlayerCharacter.prefab`(ComboStep).
- **검증**: 서버 build0 · SocketServer.Tests **126**(`ComboCadenceTests` — 데이터 저작 확인·직전 단계 chain 게이트·**지터 허용**·**허용치 초과 연타 거부**·0 폴백) · Shared.Gameplay.Tests 39 · 클라 컴파일0 · EditMode **167**(`ComboDriverTests` 7 — A즉시·**선입력 버퍼링**·체인지점 이후 즉시·**단계별 다른 타이밍**·순환·창만료·Reset) · PlayMode **8**(`RemoteDriverAnimTests` — 서브SM 체인 A→B→C + **늦게 온 패킷도 재생**(던전 동기화) + hasExitTime 회귀 고정) · Docker E2E **SocketE2ETests 30/30**. 실 콤보 손맛 육안=MPPM 수동.

### 2.60 원격 회피 애니 — S_Dodge 브로드캐스트 (2026-07-12)

애니 폴리시 백로그 #5. 다른 플레이어의 회피(구르기)가 안 보이던 것 해소.
- **문제**: 던전 회피는 `DodgeSyncSender→C_Dodge`로 서버가 무적 창만 부여하고 **다른 클라에 브로드캐스트를 안 해** 원격이 회피 애니를 못 봤다. (로컬 회피 애니는 `DodgeDriver.Begin→SetTrigger(Dodge)`로 Main·Dungeon 둘 다 이미 재생.)
- **수정(S_Attack 패턴 그대로)**: `S_Dodge{UserId}` 신설(Union **1603**). 서버 `DodgeHandler`가 `TryBeginDodge` **성공분만** `room.Broadcast(S_Dodge)` → 클라 `DodgePacketHandler` → `ISocketPacketState.OnPlayerDodged` → `RemoteDriver.HandlePlayerDodged`가 `SetTrigger(Dodge)`. 무적 창/피해 무시는 서버 권위 그대로 — 이 패킷은 **연출 전용**.
- **Main**: 솔로(원격 없음, `RemoteDriver`는 던전 전용) → 브로드캐스트 대상 0. 로컬 회피 애니만 확인(이미 동작, 사용자 확인 범위). 원격 회피는 던전 전용 기능.
- **계약**: 패킷 `S_Dodge` 1개 추가(Union 1603, MemoryPack). gRPC 아님 → Generated 불요. 서버 리빌드+Docker 재배포. GameServer 는 Shared.Packet 코드 미참조(gRPC) → 무영향.
- **⚠ 프리팹 배선 함정(실플레이 후 발견)**: E2E 통과(패킷 수신)했는데도 원격 회피 애니가 **안 보였다**. 원인 = `RemotePlayerCharacter.prefab` 의 `CharacterAgentAnimations.m_animationDodgeTrigger` 가 **빈 문자열**(원격 CAA 는 Speed/Grounded/Attack/Dead/Revive 만 채워졌고 Dodge 누락) → `SetTrigger(Dodge)` 가 빈 파라미터명 가드로 조용히 스킵. 수정 = 프리팹에 `"Dodge"` 채움(컨트롤러엔 Dodge 상태·파라미터 이미 존재). **교훈: E2E 는 패킷 수신까지만 봐 애니 재생을 못 잡는다** → 실제 프리팹을 로드해 Animator 전이를 확인하는 PlayMode 가드(`RemoteDriverAnimTests`) 추가.
- **파일**: `Shared.Packet/Domains/DodgePacket.cs`(S_Dodge)·`Packet.cs`(Union 1603) / `SocketServer/PacketHandler/Handler/DodgeHandler.cs`(브로드캐스트) / 클라 `Network/Socket/Packets/DodgePacket.cs`·`Packet.cs`·`Handler/Contents/DodgePacketHandler.cs`(신규)·`SocketApiClient.cs`(OnPlayerDodged/NotifyPlayerDodged/등록)·`Gameplay/Character/RemoteDriver.cs`(HandlePlayerDodged)·**`Prefabs/Character/RemotePlayerCharacter.prefab`(Dodge 트리거명)**.
- **검증**: 서버 build0 · 클라 컴파일0 · EditMode **160**(신규 `S_Dodge_Dispatch...OnPlayerDodged`) · PlayMode **`RemoteDriverAnimTests`**(실 프리팹 S_Dodge→Animator Dodge 전이, 수정 전엔 실패할 가드) · Docker E2E **`SocketE2ETests` 29/29**(신규 회피 브로드캐스트 + 기존 28 회귀, 실서버). 실 2인 던전 육안=MPPM 수동.

### 2.59 부활 기상(GetUp) 애니 + 아이템 줍기 = 애니 없이 토스트 (2026-07-12)

애니 폴리시 백로그 #3(GetUp)·#4(Interact 클립). PROTOFACTOR 컬렉션 실사 후 결정.
- **부활 기상(#3, 컨트롤러만·코드 무변경)**: 기존 부활은 `Dead ──Revive──▶ Idle Walk Run` 직결(즉시 스냅, §2.56). 1hMelee 셋에 **전용 기상 클립 `Humanoid@GetBackUpFront`(2.67s, DeathFront 사망포즈와 짝)** 발견 → `GetUp` 상태 신설해 `Dead ──Revive──▶ GetUp(GetBackUpFront) ──▶ Idle Walk Run`으로 재배선. GetUp→Loco 전이 2개: (a) `hasExitTime 0.75`(자연 완료) (b) `Speed>0.1`(이동 입력 시 즉시 인터럽트=반응성). **`PlayerCharacter`·`RemotePlayerCharacter`가 같은 `PlayerController` 공유 → 로컬·원격 동시 적용.** 코드(`PlayerCharacterAgent.Revive/ReviveInPlace`·`RemoteDriver`)는 이미 `Revive` 트리거를 쏘므로 무변경.
- **아이템 줍기 = 애니 없이 토스트(#4)**: PROTOFACTOR 에 전용 "줍기" 클립이 없음(DrawWeapon 플레이스홀더뿐) → 사용자 결정 = **줍기 애니 제거 + 획득 토스트로 대체**. ① `PlayerCharacterAgent.HandleInteractInput` 에서 `SetTrigger(Interact)` 제거(이동잠금 `ApplyRoot` 는 이전 요청대로 유지 — §2.53). ② **`ShopToastMessage` 패턴 채택**(사용자 지시): 모델 계층 타입 메시지 `ItemToastMessage`(struct) 신설 → `InGameModel.OnItemPickup`(`Observable<ItemToastMessage>`, 이름=`ItemDisplayCatalog` 선택 주입, 없으면 itemId) → `GameHud` 가 표시. GameHud 는 **serialized `itemToastText`(프리팹 배치 가능) + 미할당 시 코드로 하단 중앙 TMP 생성**(Shop 은 미할당 시 로그 폴백이지만 획득은 놓치면 안 돼 코드 폴백으로 항상 표시). 색/타이머/`HideAfterDelay(CTS)` 동형. `GroundItemSpawner.HandlePickedUp` 은 진단 로그로 격하.
- **던전·Main 양쪽 병합(사용자 지적: Main 에서 안 뜸)**: 줍기 소스가 둘 — **던전=소켓 `S_ItemPickedUp`**(`ISocketPacketState.OnItemPickedUp`), **Main=로컬 `LocalGroundItem→ClaimKill`(gRPC, 비네트워크)**. Main 을 소켓 상태로 위장하지 않으려고 소스 무관 허브 **`ItemPickupNotifier`**(`Game.System.Player`, `PartyAscRegistry` 와 동일 위치·패턴) 신설: `LocalGroundItem` 이 `ClaimKill` 성공 시 `granted` 별로 `Notify` → `InGameModel` 이 소켓·허브 **양쪽을 같은 핸들러로 구독**해 동일 토스트로 병합. DI: `ItemPickupNotifier`(Scoped) Main·Dungeon 둘 다 등록(InGameModel 주입 충족).
- **한글 폰트 폴백(사용자 지적: 토스트 한글 안 나옴)**: **근본 원인 = TMP 기본 폰트 `LiberationSans SDF`(라틴, Static)에 한글 글리프·폴백 0** → 코드로 만든 TMP(토스트·`PartyHpView` 등, `TMP_Settings.defaultFontAsset` 사용)가 한글을 못 그림(프리팹 TMP 는 Chiron 직접 참조라 정상이었음). 수정 = `Assets/Art/Fonts/ChironSungHK-SemiBold SDF`(Dynamic, 한글 보유)를 **`TMP Settings.asset` 전역 폴백**(`fallbackFontAssets`)에 등록 → 모든 코드 생성 TMP 가 한글을 폴백 렌더(라틴은 LiberationSans 유지). 검증: 기본폰트 TMP 에 "아이템 획득" → 한글 5자 전부 `ChironSungHK-SemiBold SDF`·vis=True 확인. **파티 HP 한글 닉네임(§2.58)도 동시 해소.**
- **파일**: `GameResources/Animations/Player/PlayerController.controller`(GetUp) / `Gameplay/Character/Agent/PlayerCharacterAgent.cs`(Interact 제거) / `Presentation/InGame/ItemToastMessage.cs`·`InGameModel.cs` / `GUI/Hud/GameHud.cs` / `System/Player/ItemPickupNotifier.cs`(신규) · `Gameplay/Character/LocalGroundItem.cs`(Notify) · `VContainer/.../{Main,Dungeon}LifetimeScope.cs`(등록) / `Gameplay/Character/GroundItemSpawner.cs`(주석).
- **검증**: 클라 컴파일0 · EditMode **159**(신규 `아이템_획득시_OnItemPickup_토스트_메시지가_발행된다`[던전] + `Main_로컬줍기_통지시_OnItemPickup_토스트가_발행된다`[Main]) · PlayMode `ActionRootTests` **5/5**(강화: 부활 후 `GetUp` 진입→기상 재생→로코모션 복귀; 상호작용 Rooted 유지). 순수 클라 → 서버/E2E 불요. 실 육안(기상·던전/Main 획득 토스트)=MPPM 수동.

### 2.58 던전 파티 HP HUD — 원격 ASC 레지스트리 재사용(신규 패킷 0) (2026-07-12)

애니 폴리시 백로그 ★ 항목. 던전 좌상단에 파티원(로컬+원격) HP 바를 표시.
- **교리: 신규 패킷 0.** HP 진실원은 이미 서버 권위 GAS(`S_ApplyEffect`가 **방 전체 브로드캐스트**)다. 원격 플레이어의 피해/회복도 이미 클라에 도착하고 있으나, 여태 `EffectReceiver`가 로컬(TargetId==내 UserId)만 라우팅하고 버려서 원격 HP를 몰랐을 뿐. → 원격 캐릭터에 ASC를 얹고 TargetId로 라우팅하면 별도 동기화 패킷 없이 파티 HP가 따라온다.
- **데이터 경로**:
  `S_ApplyEffect(방 브로드캐스트)` → `SocketPacketState.OnEffectApplied`
  → `EffectReceiver.ResolveTarget(TargetId)` → **`PartyAscRegistry.TryGet`** (로컬+원격 모두)
  → 해당 ASC `ApplyEffectAuthoritative` → `GameplayAttribute.OnChanged`
  → `PartyModel`(각 ASC.OnAttributeChanged 구독) `Changed` → `PartyHpView` 재렌더.
- **컴포넌트**:
  - `Client/Assets/Script/System/Player/PartyAscRegistry.cs` (신규) — UserId→ASC 딕셔너리(`LocalPlayerContext`의 파티 확장). 생산=CharacterSpawner, 소비=EffectReceiver(라우팅)·PartyModel(집계). Gameplay↔Presentation 형제라 공통 하위 `Game.System.Player`에 둠.
  - `CharacterSpawner` — 로컬 스폰 시 `Register(authUserId, asc)`, 원격 스폰 시 `Register(snapshot.UserId, go.GetComponent<ASC>())`, 디스폰 `Unregister`, Dispose `Clear`.
  - `RemotePlayerCharacter.prefab` — **ASC(Health 100/100) 추가**(기존엔 없었음). 스폰 시 서버 권위 기준선으로 덮어씀(아래 HP 기준선 동기화).
  - `EffectReceiver.ResolveTarget` — 레지스트리 우선 → 미등록 폴백(로컬 LocalPlayerContext). `OnEffectRemoved`는 여전히 로컬만(S_RemoveEffect에 TargetId 없음 → 버프 제거는 대상 식별 불가; 파티 HP는 즉발 델타라 무관).
  - `Presentation/InGame/PartyModel.cs` (신규) — 레지스트리+로스터(닉네임)+ASC Health 집계 → `IReadOnlyList<PartyMemberInfo>`. 구성/HP 변경 시 Changed.
  - `GUI/Hud/PartyHpView.cs` (신규) — MVI View(오직 PartyModel 주입). **코드로 자체 Overlay Canvas+행 풀 생성**(프리팹 수술 0). 좌상단, 로컬=하늘색+`<b>`, 아군=연두, 사망=회색. GameHud와 분리한 이유: GameHud는 InGameModel 전용(뷰=모델 1개 규칙).
  - DI: `DungeonLifetimeScope`(PartyAscRegistry+PartyModel+PartyHpView), `MainLifetimeScope`(PartyAscRegistry만 — CharacterSpawner가 로컬 등록하므로 필요, 파티 HUD는 미표시).

#### 2.58b 원격 HP 기준선 동기화 — S_PlayerJoined 에 Hp/MaxHp (2026-07-12, 실플레이 후속 수정)
- **증상(실 2인 플레이)**: 파티창 아군이 `70/100`인데 그 아군 본인 화면은 `110/140` — MaxHp·현재HP 둘 다 어긋남. 로컬 본인은 정확.
- **원인**: 로컬 HP 기준선은 **owner-only**(`PlayerProgressionHolder.GetProgression`→`PlayerStatApplier`, §2.41)라 남의 MaxHealth를 클라가 모른다. 원격 ASC가 prefab 100/100 에서 출발 → 서버 델타(정확히 수신, −30)는 얹히나 **기준선이 100이라** 결과가 40씩 어긋남(현재 70/최대 100). 몬스터는 `S_SpawnMonster`에 Hp/MaxHp가 있어 정확했지만 `S_PlayerJoined`엔 없었다.
- **수정(몬스터와 동일 패턴)**: `S_PlayerJoined` += `Hp,MaxHp`(서버 `PlayerState`가 이미 보유, `InitPlayerState(maxHealth)`). `ToJoinedPacket` 한 곳만 채우면 **3 전송지점(본인응답·타인브로드캐스트·늦은입장 로스터)** 모두 반영. 클라 `SocketPlayerSnapshot`+`UpsertPlayer`(선택적 hp/maxHp)+핸들러 전달 → `CharacterSpawner.SpawnRemote`가 원격 ASC `Health.SetMax/SetCurrent`로 기준선 교정 후 등록. 이후 델타가 **정확한 기준선** 위에 얹혀 파티 HP 일치. (로컬은 PlayerStatApplier 그대로.)
- **계약**: `S_PlayerJoined` 직렬화 필드 2개 append(MemoryPack 순서 보존, 서버+클라 미러 동시). gRPC 아님 → Generated 재생성 불요. 서버 리빌드+Docker 재배포 필요.
- **파일**: `Shared.Packet/RoomPackets.cs`·`SocketServer/PacketHandler/Handler/RoomJoinLeaveHandler.cs`(ToJoinedPacket) / 클라 `Network/Socket/Packets/RoomPackets.cs`·`SocketApiClient.cs`(snapshot+Upsert)·`Handler/Contents/PlayerJoinedPacketHandler.cs`·`Gameplay/Character/CharacterSpawner.cs`.

#### 2.59 Actor 통합 전투 인프라 착수 — ActorIds + AbilityActivationMath (2026-07-16, 증분1/7)
- **계기**: 몬스터 공격 모션 부재 진단 → 근본은 "몬스터가 공격했다" 신호 경로 자체가 없음(플레이어는 `S_Attack`→`RemoteDriver`(§2.55 R2) 있으나 몬스터는 통째 부재). 전체 설계·전 축 통합지도 = [actor-combat-architecture.md](actor-combat-architecture.md).
- **교리**: 전투 상호작용(발동·적중·연출·조회)은 **ActorId 하나로 통합**(플레이어=UserId 양수 / 몬스터=−InstanceId 음수), 생명주기(스폰·상태·사망)는 종족별 유지. 완전 Actor 통합 안 함(서버 ASC 불가 = gas ②⑥ / §9 서버분리 가능성 보존).
- **무엇(증분1 = Shared 순수만, 배선 없음)**:
  - `ServerAll/Shared/Shared.Gameplay/Actors/ActorIds.cs` — ActorId 부호 규약 **단일 정의**(`FromMonster`=−id / `IsPlayer/IsMonster` / `ToMonsterInstanceId`). 클라·서버 손계산 금지.
  - `ServerAll/Shared/Shared.Gameplay/Abilities/AbilityActivationMath.cs` — 발동 게이트 순수함수(`Evaluate`→Ok/Blocked/OnCooldown/NotEnoughMana, 우선순위 Blocked→Cooldown→Mana). 쿨다운=기존 `SkillTimelineMath.CooldownElapsed` 재사용.
- **왜 원시 파라미터 게이트**: 몬스터 공격수치는 이미 `Shared.Infrastructure.Monsters.MonsterCatalog`(monsters.json) 단일소스 → 별도 SkillTimeline 시드는 중복. 게이트가 `cooldownMs/manaCost`를 값으로 받아 플레이어(SkillTimeline)·몬스터(MonsterDef.AttackCooldownMs) **공용**(계획의 "몬스터 스킬 시드" 철회).
- **검증**: `Shared.Gameplay.Tests` 50/50(신규 `ActorIdsTests` 4·`AbilityActivationMathTests` 6) + `ServerAll.sln` 빌드 0오류.
- **증분2 완료(2026-07-16)**: 패킷 `S_AbilityActivated`(Union **1604**){`long ActorId`, `int SkillId`} = Actor 통합 발동 연출 신호(S_ 브로드캐스트, 서버 핸들러 없음). 위치 `Shared.Packet/Packets/Domains/AttackPacket.cs`+`Packet.cs`. **클라 미러는 ClientCodegen 재생성**(`dotnet run --project ServerAll/Tools/ClientCodegen -- <repoRoot>` — 수기수정 금지, `// <auto-generated>`). 검증 `AbilityActivatedPacketSerializationTests` 3/3 + SocketServer.Tests 130/130 + Unity 컴파일 0오류. ※`dotnet build Client/Game.Main.csproj`의 Game.Input CS2001은 기존 stale csproj(무관).
- **증분3 완료(2026-07-16)**: 서버 `Room.TickMonsters`(`SocketServer/Room/Room.cs`) 몬스터 공격을 ① ad-hoc 쿨다운 → `AbilityActivationMath.CanActivate`(MonsterDef.AttackCooldownMs 먹임, 플레이어와 동일 Shared 규칙) ② 발동 시 `S_AbilityActivated{ActorId=ActorIds.FromMonster(instanceId), SkillId=0}` broadcast(**i-frame continue 앞** = 헛스윙도 스윙신호 나감) ③ `S_ApplyEffect.SourceId` 0→`-instanceId` 승격(데미지·CC 둘 다). 클라 미소비(핸들러=증분4, 디스패처가 미등록 무시라 무해). 검증 `MonsterAttackTests` +2(발동신호·헛스윙) → SocketServer.Tests 132/132 + ServerAll.sln 0오류. ※SourceId 승격은 클라·서버 어디도 `==0` 분기 없음 확인 후 적용.
- **증분4 완료(2026-07-16) — 몬스터 공격 모션 실해소**: 클라 라우팅 신설.
  - `Gameplay/Character/ActorRegistry.cs`(ActorId→IActorView Dictionary, 방스코프) · `IActorView.cs`(`PlayAbilityCue(int)`) · `AbilityCueRouter.cs`(IInitializable/IDisposable — `OnAbilityActivated` 단일 구독→registry 조회→Cue).
  - `Network/Socket/Handler/Contents/AbilityActivatedPacketHandler.cs` + `ISocketPacketState.OnAbilityActivated`/`NotifyAbilityActivated`(SocketApiClient) + 핸들러 DI 등록.
  - `MonsterEntity`: `IActorView` 구현 — `attackState` 필드 + `PlayAbilityCue`가 CrossFade + `_attackLockUntil`(attackLockSec 동안 locomotion PlayState 억제 = 공격 애니 보존).
  - `MonsterSpawner`: `ActorRegistry` 주입 → 스폰 시 `Register(FromMonster(id), entity)` / 디스폰·Dispose 시 Unregister.
  - `DungeonLifetimeScope`: `ActorRegistry`(Scoped)+`AbilityCueRouter`(EntryPoint) 등록.
  - **프리팹**: 던전 몬스터 8종 `MonsterEntity.attackState="Attack"` 설정(execute_code). ※Main `CreepyDemonLocal`은 증분6.
  - **검증**: Unity 0오류 · EditMode 172/172(`ActorCombatRoutingTests` 6) · PlayMode `MonsterEntityAnimTests`(신호→Animator "Attack") · **Docker E2E SocketE2ETests 31/31**(신규 `S_AbilityActivated_발동신호_수신`, 서버 리빌드 후). ※신규 클라 .cs 6개·수정 프리팹 8개 커밋 시 `.meta` `git add -f` 필수([[unity-meta-gitignored]]).
- **증분5 완료(2026-07-16) — 플레이어 흡수**: 플레이어 공격도 몬스터와 같은 파이프.
  - 서버 `CombatHandler.HandleAttack`(0d): `S_Attack` broadcast → `S_AbilityActivated{ActorId=ActorIds.FromPlayer(UserId), SkillId}`. ※발동 게이트(마나·콤보 cadence·쿨다운)는 `PlayerState.TryBeginSkill/TryBeginComboAttack`에 이미 존재 = **그대로 유지**(stateless AbilityActivationMath로 강제전환 안 함 — 콤보 로직 손실 방지, YAGNI).
  - 클라 `RemoteDriver`: `IActorView` 구현 — `HandlePlayerAttacked`(OnPlayerAttacked 구독) 제거 → `PlayAbilityCue(int)`(콤보 매핑+SetTrigger). `CharacterSpawner`가 `ActorRegistry.Register(FromPlayer(UserId), driver)`(스폰)/Unregister(디스폰·Dispose). `MainLifetimeScope`에 ActorRegistry 등록(생성자 충족, Main 솔로라 빈 레지스트리).
  - **⚠️ orphaned(후속 정리 대상)**: `S_Attack`(Union 1601) 타입·`AttackPacketHandler`·`OnPlayerAttacked`/`NotifyPlayerAttacked`는 **이제 아무도 안 보냄**(dead). 타입 삭제=공개계약 변경이라 **명시 승인 시** 별도 정리. 지금은 보존(무해).
  - 검증: 서버 0오류 · Unity 0오류 · EditMode 172/172 · PlayMode 콤보 4/4(`RemoteDriverAnimTests` PlayAbilityCue 경로) · **Docker E2E SocketE2ETests 31/31**(콤보/basic S_AbilityActivated로 갱신, socketserver 리빌드 후).
- **증분6 완료(2026-07-16) — Main 몬스터 공격 모션 해소**: `Gameplay/Character/LocalMonster.cs`(Main 솔로·클라 권위)를 던전 MonsterEntity 와 대칭화.
  - `IActorView` 구현 + `attackState`/`attackLockSec`/`_attackLockUntil` 필드 + `PlayAbilityCue(int)`(CrossFade+lock, MonsterEntity 와 동일 재생). Update 의 locomotion PlayState 를 attack lock 동안 억제.
  - `TryAttack`: 인라인 `_nextAttackTime` 쿨다운 → `AbilityActivationMath.CanActivate(nowMs=(long)(Time.time*1000), _lastAttackMs, (int)(attackCooldownSec*1000), 0,0,false)`(던전·플레이어와 동일 Shared 규칙). 발동 시 `PlayAbilityCue(0)` 를 **i-frame continue 앞**에서 호출(헛스윙 포함). 데미지 적용(`ApplyEffect`)은 기존 유지.
  - 프리팹 `CreepyDemonLocal.prefab`(유일 LocalMonster) `attackState="Attack"`.
  - ※Main 은 네트워크·ActorRegistry·라우터 없음 — 로컬 AI 가 `PlayAbilityCue` 직접 호출(던전은 서버 신호→라우터 경유, 재생 로직은 공통).
  - 검증: Unity 0오류 · PlayMode `LocalMonsterAnimTests`(발동→Animator "Attack") 포함 애니 5/5 · EditMode 172/172. (Main 솔로라 Docker E2E 무관.)
- **증분7 완료(2026-07-16) — 스케일**: 서버 dirty-flag만. `Monster/MonsterState.cs` 에 `_sent*` + `StateDirty()`/`MarkStateSent()` 추가 → `Room.TickMonsters` 가 위치·회전·HP·페이즈 무변화 시 `S_MonsterState` 송신 생략(**Idle 경비 몬스터 트래픽 0**). `CombatHandler` 데미지 송신 직후에도 `MarkStateSent()`(틱 중복 재송신 방지). 신규 입장자는 `S_SpawnMonster` 로스터로 최신 상태 수신 → 유실 없음.
  - **클라 이동 라우팅 Registry화는 보류(YAGNI, 설계 §4.4 재판정)**: `OnMonsterMoved` fan-out 비용 = 몬스터당 int 비교+early-return(마이크로초)이고, 대량 몬스터의 지배 비용은 **엔티티마다 매 프레임 `MonsterEntity.Update()` 보간·렌더**(라우팅으로 안 줄어듦). 병목이 아니라 판정 → 확장점만 남김(필요 시 `MonsterSpawner._monsters` 단일 구독자 dispatch 로 전환, IActorView 무변경).
  - 검증: `MonsterTickDirtyStateTests` 2(idle 생략·chase 매틱) → SocketServer.Tests 134/134 · ServerAll.sln 0오류 · **Docker E2E 31/31**(리빌드 후 회귀).
- **✅ AC 트랙 완료(증분1~7)** — 원 문제 "몬스터 공격 모션 안 나온다" = 던전(증분4)·Main(증분6) 양쪽 해소. 전투 발동·연출·조회가 ActorId 단일 파이프로 통합(플레이어=양수/몬스터=음수).

### 2.57 NPC 애니 + 락온 strafe(8방향) + 락온 UI 마커 (2026-07-12)

애니 폴리시 백로그(animation-combat-polish-backlog.md) #1·#6.
- **NPC 애니**: `NPCController` 가 파라미터만 있고 states=0(빈 컨트롤러)이었다. Locomotion 블렌드(IdleUnarmed/WalkForwardUnarmed/RunForwardUnarmed, Speed 0/2/6) 생성+기본상태. Main 씬 NPC(씬 배치, 프리팹 아님)에 `SK_Protof-Actor`+Animator 부착·캡슐 렌더 숨김. `NpcCharacterAgent`가 플레이어처럼 FSM+CharacterAgentAnimations 를 써서 모델+Animator만 붙으면 자동 애니. ⚠ NPC 는 씬에서 active=False.
- **락온 strafe(8방향)**: 락온 중(`CharacterMotor.FacingOverride`) 몸은 타겟 향하고 이동은 카메라기준 스트레이프인데 1D Speed(전진)만 있어 옆/뒤가 어색했다. → 계약 `CharacterAgentAnimations`에 `MoveX`/`MoveY`(float)·`Strafe`(bool) + SetFloat/SetBool 빈값 가드(원격/NPC 미배선 경고 방지). `GroundState.DriveStrafeAnimation`이 락온 시 이동방향을 facing 프레임으로 분해(MoveX=우dot, MoveY=전dot, ×속도비율)해 공급. `PlayerController` Locomotion 서브SM에 `Strafe` 2D FreeformDirectional 블렌드(9클립=idle+8방향 Run 1hMelee) + `Idle Walk Run Blend↔Strafe`(Strafe bool) 전이. 검증: 런타임 Animator Strafe=true→Strafe 상태·MoveX=-1→RunLeft·MoveY=1→RunForward.
- **락온 UI 마커**: `LockOnMarker`(월드공간 Canvas 빌보드, MonsterHealthBar 패턴). `LockOnDriver`가 락온 시 `Show(target)`·해제 시 `Hide()`, 지연 생성·재사용. 아이콘 = **절차적 링 스프라이트 런타임 생성**(빌트인 `UI/Skin/Knob.psd`는 플레이어 빌드에 없어 로드 실패 → Texture2D→Sprite 1회 캐시).
- **플레이 버그 2건 수정**: ① 락온 중(=Strafe 상태) 공격 무동작 — Attack 전이가 `Idle Walk Run Blend` 상태 전용이라 Strafe 엔 없었다 → `Strafe→RightHand1Combat`(Attack)·`Strafe→Interact`(Interact) 직접 전이 추가(Jump/Fall 은 Locomotion AnyState 라 무관). ② 마커 스프라이트 런타임 오류(위).
- **NPC 2종**: `NPC`(NpcCharacterAgent, 씬 비활성이라 활성화) + `NPC_Elder`(NPCDialogueInteractable, agent 없는 대화 캡슐 — 모델+Animator 부착, 루트 y=1 캡슐중심이라 모델 −1 오프셋). `NPC (1~7)`/`window_npc` 는 화면 UI(대화 초상화)라 무관.
- 검증: EditMode 155/155 + 런타임 Animator(strafe·Strafe중 Attack). **미검증(플레이)**: strafe 손맛·마커 위치는 사용자 확인("잘된다").

### 2.56 Rooted 공중 상태 확장 + 부활 복귀 애니 (2026-07-11)

이전 세션 잔여 버그 2건 정리(plan A "벌인 것 닫기").
- **Fall/Jump 이동잠금 누락**: `Rooted` 체크가 `GroundState` 에만 있어 공중 공격/줍기 시 에어컨트롤이 안 잠겼다. → `FallState`·`JumpState` 에 `AbilitySystemComponent`(선택 인자) 주입 + `HasTag(Rooted)` 시 수평 0·중력 유지(GroundState 동일 규약). `StateFactory` 가 `context.AbilitySystem` 전달. `LandState` 는 no-op 이라 제외. 테스트 `Rooted_태그가_있으면_공중_FallState도_수평이동을_막는다`.
- **부활 복귀 애니**: Dead 상태(AnyState→Dead, 홀드)에 나가는 전이가 없어 `ResetTrigger(Dead)` 만으론 부활해도 사망 포즈에 갇혔다. → 양성 `Revive` 트리거 신설(`AnimationTriggerType.Revive` + `m_animationReviveTrigger`), 컨트롤러 `Dead→Idle Walk Run Blend`(Revive) 전이. `PlayerCharacterAgent.Revive/ReviveInPlace`·`RemoteDriver.HandlePlayerRevived` 가 발화, 사망 시 `ResetTrigger(Revive)` 위생. 프리팹(Player/Remote) 문자열 "Revive". 검증: 런타임 Animator 구동 Dead→Revive→로코모션 복귀 + EditMode 153/153.

### 2.55 HitboxMath yaw 부호 버그 수정 + 몬스터 체력바 + 공격 진단로그 (2026-07-11)

- **HitboxMath 방향 버그(중대·서버권위 공유)** — `ServerAll/Shared/Shared.Gameplay/Combat/HitboxMath.cs` 의 월드→로컬 회전이 `yawRad = -yaw` 로 X/Z 교차항 부호가 뒤집혀 있었다. **yaw 0/180(정북·정남, sin0)에서만 맞고 90/270/대각에선 히트박스가 반대쪽**으로 갔다("전방 안 맞음"). 수정 = 부호 제거(`yawRad = yaw`). Unity 좌표 forward=(sinθ,0,cosθ)의 올바른 역회전.
  - **파급**: `HitboxMath` 는 서버 권위 전 판정 공유(플레이어→몬스터, 몬스터→플레이어, PvP). 던전의 비정면 공격/피격이 전부 어긋났었다.
  - **사각지대 원인**: 기존 `HitboxMathTests`·`CombatHandlerTests` 가 yaw 0/180 만 검증. → TDD 회귀 `임의_yaw에서_정면_타겟은_적중하고_후방은_빗나간다`(45/90/135/270/315) 추가(수정 전 red 5개 확인).
  - **반영 경로**: 소스는 `ServerAll` 이지만 **클라는 `Client/Assets/Plugins/Shared.Gameplay/Shared.Gameplay.dll`(netstandard2.1, tracked) 로 참조** → 수정 후 **DLL 재빌드+복사 필수**(클라 예측·Main LocalCombat 폴백). 서버 권위는 Docker socketserver 리빌드.
  - 검증: Shared.Gameplay 39/39 · SocketServer 전투 28/28 · 클라 DLL 반영 후 8 yaw 전부 정면HIT/후방MISS · EditMode 153/153.
- **몬스터 체력바** — `Monster.prefab` 머리 위 월드공간 Canvas(BG+Fill Filled Horizontal), `MonsterHealthBar`(신규)가 부모 `MonsterEntity.HpChanged` 구독→fillAmount, 카메라 빌보드. `MonsterEntity` 에 `Hp/MaxHp` + `HpChanged` 추가(스폰 seed + S_MonsterState→OnMonsterMoved 갱신). HP 진실원=서버, 표시 전용.
- **⚠ 임시 진단로그(원인 확정 후 제거)** — 비방장 공격 미동작 추적용 `[DIAG-Attack]`(클라 `CombatSyncSender`, 서버 `CombatHandler` docker stdout) + 기존 `[DIAG-Interact]`(줍기 이동잠금). `TODO(diag)` 표시.

### 2.54 원격 플레이어 애니(RemotePlayerCharacter) — 서로 보이기 + 공격 연출 (2026-07-11)

로컬 플레이어(§2.52)에 이어 **원격 플레이어도 실제 캐릭터로 보이게**. 두 파트:

- **R1 (네트워크 변경 0)** — `RemotePlayerCharacter.prefab` 에 `SK_Protof-Actor` 모델+Animator(PlayerController, avatar=null, rootMotion=false)+무기(**메시 전용**)+캡슐숨김. `RemoteDriver` 가 애니 구동: 수신 스냅샷의 **보간 수평변위→Speed**(블렌드 0/2/6), `OnPlayerDead→Dead`, `OnPlayerRevived→Dead 해제`, Grounded 상시 true(점프/낙하 미동기화=지상 가정). `CharacterAgentAnimations` 은 Speed/Grounded/Attack/Dead 만 배선(나머지 빈값=미구동).
- **R2 (서버 포함)** — 서버 `CombatHandler.HandleAttack` 가 **마나·쿨다운 게이트 통과 시에만** `S_Attack{AttackerId,SkillId}` 를 방에 Broadcast(연사 치팅이 원격 애니로 안 샘). 클라 `AttackPacketHandler`(신규)→`ISocketPacketState.OnPlayerAttacked`→`RemoteDriver` 스윙 애니. **S_Attack=연출 전용**, 적중=서버 권위(S_ApplyEffect/S_MonsterState) 유지. `S_Dodge` 패킷 부재 → 원격 회피 애니는 범위 밖.

**서버 권위 불변식(중대)**: 원격 프리팹엔 `WeaponHitbox`/`Rigidbody`/`Collider` **절대 금지**(넣으면 원격이 로컬에서 몬스터 타격 → 권위 붕괴). 검증에서 colliders=0·rigidbodies=0 확인.
**검증**: 서버 118/118 · 클라 EditMode 153/153(`S_Attack→OnPlayerAttacked` 신규) · Docker SocketE2E 27/27 + `RawSocket_공격하면_S_Attack_연출_브로드캐스트를_수신한다` 신규. 손댄 파일: `RemoteDriver.cs`·`AttackPacketHandler.cs`(신규)·`SocketApiClient.cs`·`CombatHandler.cs`·`RemotePlayerCharacter.prefab`.

### 2.53 Action 이동잠금(Rooted) — 공격·상호작용 중 이동 금지 (2026-07-09)

**교리(CA-1): Action 이동제약은 FSM 전이가 아니라 태그로.** `PlayerCharacterAgent.HandleInteractInput` 주석이 예고한 대로 구현.
- **태그**: `Gameplay/Character/ActionTags.Rooted`("State.Rooted") — **클라 전용**(서버 미사용, `Shared.Gameplay/GameplayTags` 와 분리). 이동=클라권위(C_Move)라 잠긴 동안 C_Move 미송신 → 원격도 정지로 봄.
- **부여**: `PlayerCharacterAgent.ApplyRoot(sec)` — 공격 `FireSkill`(skill startup+active+recovery=basic 450ms) / 상호작용 `HandleInteractInput`(고정 `InteractRootSeconds`=0.6s). `Update` 최상단이 `_rootedUntil` 경과 시 자동 `RemoveTag`.
- **소비**: `GroundState.StateUpdate` 가 `HasTag(Rooted)` 폴링 → 수평이동·Speed 0, `Move((0,verticalVel,0),0)`(중력·회전·락온 facing 유지). 기존 Slow 태그 게이트와 동일 패턴.
- **순서 규칙**: `HandleInteractInput` 은 `ApplyRoot` 를 `target.Interact()` **앞에서** 호출한다 — 대상의 `Interact()` 가 조기반환(세션 없음·인벤 미주입)하거나 예외를 던져도 이동잠금은 걸려야 하기 때문. (뒤에 두면 조용히 스킵됨.)
- **검증**: PlayMode `ActionRootTests` 3종 — ①공격→부여→만료해제 ②실제 `InteractionDetector` 감지→`Interact()`→부여 ③Rooted 시 GroundState 수평변위≈0(없으면 이동). EditMode 152/152. 신규 `.cs` = `ActionTags.cs`·`ActionRootTests.cs`(`.meta` `git add -f`).
- **미해결**: 실게임 아이템 줍기에서 "안 멈춤" 보고 — 위 3종이 사슬 전체를 증명하므로 줍기가 `HandleInteractInput` 을 안 타는 경로 의심(2순위 소비자 `ReviveInteractor.Update` 가 `ConsumeInteractPressed()` 폴링). 원인 확정 후 합의된 `IInteractable.RootSeconds`(대상별 지속시간) 도입 예정.

### 2.52 플레이어 애니메이션 배선 + 무기 프롭·무기콜라이더 판정 (2026-07-09)

정식 문서 [player-animation-setup.md](player-animation-setup.md). MotionMatching 외부화 후 **플레이어 프리팹이 회색 캡슐(모델·Animator 없음)** + 컨트롤러 클립참조 깨짐이라 애니가 전혀 안 돌던 것을 배선.

- **기반**: `SK_Protof-Actor.fbx`(PROTOFACTOR, Generic 스켈레톤·**아바타 0개=리타겟 불필요, avatar=null 경로매칭**) + Animator를 `PlayerCharacter.prefab` 자식으로. `PlayerController.controller` 6상태 모션을 1Handed Melee **non-`_RM`(in-place)** 클립으로 재지정(이동=코드 `CharacterMotor`, 루트모션 미사용) + 고아 `MM_*` 삭제. Dead(AnyState 홀드)·Dodge(AnyState→복귀) 상태/파라미터 추가 + 프리팹 트리거 문자열.
- **LoopTime**: 순환 클립(Idle/Walk/Run/Falling)만 ON, 원샷은 OFF(Death=마지막프레임 홀드).
- **무기 프롭**: `SM_BludgeonProp` → `humanoid_ R Hand/WeaponProp`(로컬 identity=손 본 로컬공간 정합).
- **무기 콜라이더 판정(Main 클라권위 전용)**: `Gameplay/Character/{WeaponHitbox,WeaponAnimationEventRelay}` 신규. `AttackA1hMelee` 클립 Animation Event(0.35s ON/0.62s OFF)→Relay(Animator GO)→`WeaponHitbox`(WeaponProp: CapsuleCollider trigger+Kinematic RB) 활성구간→`OnTriggerEnter(LocalMonster)`→`OnHit`→`LocalCombat.ApplyWeaponHit`. `LocalCombat`=무기 있으면 콜라이더 판정, 없으면 기존 OverlapSphere 폴백. **던전은 무관**(서버가 클라 콜라이더 모름 → `C_Attack`→서버 HitboxMath 유지).
- **히트박스 무기리치 정합(서버·클라 공유)**: 무기 스윙이 몸통~머리(측정 y0.4~1.9)인데 박스가 발높이라 어긋남 → `Skill_BasicSwing/HeavySwing.asset` offsetY/halfY 수직확장(XZ 리치는 유지=테스트 안전) → bake `skills.json`. ⚠️ bake는 `SkillCatalogExporter.BakeAll()` 직접호출(메뉴 `Export()`는 `DisplayDialog` 모달→MCP 프리즈).
- **검증**: 서버 전투단위 28/28 · Docker SocketE2E 27/27 · 무기콜라이더 체인 플레이모드 마커 · EditMode 152/152.
- **잔여**: 부활 복귀 애니 갭(`Revive`가 `ResetTrigger(Dead)`만 → Dead 홀드 탈출 신호 없음, §2.5.1 death-respawn). 무기 스윕 서버권위화·콤보 A→B→C·RemotePlayer/NPC 애니는 미착수.

### 2.51 타겟팅/락온 — 락온 시 카메라/공격 방향 고정 (2.6.3, 2026-06-30)

**교리: 락온은 순수 클라 조준 보조 — 패킷·서버·Shared 변경 0.** 공격 적중은 이미 "플레이어 facing/위치" 기반이고, facing 은 `MoveSyncSender`(던전)가 rotY 를 **정지 중에도** 송신한다. 따라서 락온이 플레이어를 타겟으로 **회전시키기만 하면** 던전 서버 hitbox(`CombatHandler`)가 자동 정렬되고, Main 은 `LocalCombat`가 같은 facing 으로 판정한다. → 새 패킷/서버 게이트 불필요.

- **결정(사용자)**: 키=**Tab**(`.inputactions` 신규 액션 — 처음 Q였으나 E는 Interact 충돌이라 Tab으로 확정) · 타겟선정=**화면중앙 최근접**(뷰포트 중심 거리) · 범위=**Main+던전 동시**.
- **마커(레지스트리)**: `Gameplay/Character/LockOnTarget.cs` — `DownedAllyMarker` 패턴(정적 `_active` 리스트, OnEnable/OnDisable 등록). `FindBest(camera, playerPos, maxRange)` = 화면 안 + 카메라 앞 + 평면거리≤range 중 **뷰포트 중심(0.5,0.5) 최근접**(`WorldToViewportPoint`). 던전 `MonsterEntity`·Main `LocalMonster` 는 다른 클래스지만 이 마커 하나로 통일(물리 레이어 무의존). **프리팹 부착**: `Monster.prefab`(던전)·`LocalMonster.prefab`(Main).
- **드라이버**: `Gameplay/Character/LockOnDriver.cs`(순수 C#, `DodgeDriver`/`KnockbackDriver` 형제, `PlayerCharacterAgent` 소유). `Toggle()`=락 중이면 해제·아니면 `FindBest` 획득. `Tick()`=매 프레임 유효성(파괴/비활성/사거리+3m 히스테리시스 이탈→자동 해제) 후 `Motor.FacingOverride`=타겟방향 + `CameraFollow.LockTarget`=타겟. `ForceUnlock()`=사망/씬종료 시 원복. **버그 수정**: `Tick` 첫 가드를 `_target==null`(Unity fake-null)로 두면 **파괴된 락 대상도 조기 반환** → 죽은 몬스터에 카메라가 영구 잠긴다. `ReferenceEquals(_target,null)`로 "미락(C# null)"과 "락 중 대상 파괴(fake-null)"를 구분 → 후자는 유효성검사로 내려가 정리(LockOnDriverTests 가 포착).
- **연결점(2개, 최소 침습)**: ① `CharacterMotor.FacingOverride`(nullable Vector3) — 값 있으면 회전전략(카메라기준) 대신 그 방향을 바라봄. **이동(DesiredMoveDirection)은 무영향** = 락온 중에도 이동은 카메라기준 스트레이프. ② `CharacterCameraFollow.LockTarget`(Transform) — 값 있으면 마우스 Look 대신 피벗을 타겟 방향 yaw/pitch 로 수렴(`lockOnTurnSpeed`). 락 중에도 yaw/pitch 캐시 갱신 → 해제 시 마우스가 현재 각도에서 자연 연속.
- **입력**: `LockOn` one-shot 신설 — `.inputactions`(Tab, `<Keyboard>/tab`) + 래퍼 재생성(`manage_asset import`) + 계약5: `CharacterInputFrame.LockOnPressed`·`ICharacterInputWriter.PressLockOn`·`ICharacterInputSource.ConsumeLockOnPressed`·`CharacterInputBuffer`·`PlayerInputComponent.OnLockOn`. 테스트 Fake 3곳도 `ConsumeLockOnPressed` 추가.
- **에이전트 배선**: `PlayerCharacterAgent` — `lockOnRange`(SerializeField 15m) + Awake 에서 `GetAroundComponent<CharacterCameraFollow>()`로 `LockOnDriver` 생성. Update 정상 흐름: dodge 게이트 옆 `HandleLockOnInput()`(토글 폴링) → `_lockOn.Tick()`(facing 적용을 `base.Update()`→`Motor.Move` 전에) → `base.Update()`. 사망 게이트 진입 시 `_lockOn.ForceUnlock()`.
- **검증**: PlayMode `LockOnDriverTests` 4/4(화면중앙 선정·토글 잠금/해제·사거리밖 거부·소실 자동언락) + **PlayMode 전체 160/160**(E2E Docker 포함, 입력계약 확장 무회귀) + **EditMode 152/152** + Unity 컴파일0. 네트워크/소켓 소스 무변경 → Docker E2E 불요(연결-커버리지 훅 무관). 신규 `.cs.meta` 3개 `git add -f`([[unity-meta-gitignored]]). **잔여(선택)**: 락온 표시 UI(타겟 위 마커)·타겟 전환(다음 적) 입력·Animator strafe 블렌드.

### 2.49 스킬 데이터 자산화 — SO 저작→bake→서버 (2.2, 2026-06-26)

**교리: 스킬을 클라 SO 로 저작 → Export bake → 서버가 임베디드 JSON 으로 검증** (gas-architecture §2.5, DropTable/Monster/Consumable 과 동일 패턴). 기존 하드코딩 `Shared.Gameplay.SkillCatalog`(코드 시드) → 데이터 주도.

- **서버(권위)**: `Shared.Infrastructure/Skills/skills.json`(임베디드) + `Skills/SkillCatalog.cs`(로드→`SkillTimeline`, Shared.Gameplay 순수 타입) + .csproj `EmbeddedResource`. `CombatHandler.ResolveSkill(int skillId)` 가 이 카탈로그 사용 + 패킷 int→문자열 매핑(0=basic_swing, 1=heavy_swing — C_Attack 계약 보존).
- **클라(저작·런타임)**: `Gameplay/Abilities/SkillDefinition`(스킬당 SO, CreateAssetMenu — id·타임라인·hitbox·onHitEffectIds, `ToTimeline()`) · `SkillCatalogDefinition`(SkillDefinition 참조 목록 = "각 스킬별 Asset") · `SkillCatalogProvider`(런타임 id→SkillTimeline, DI) · `Editor/SkillCatalogExporter`(Tools/Skill/Export, 카탈로그→skills.json, hitboxShape=enum 이름 문자열). `LocalCombat`(Main) 가 코드시드 대신 Provider 로 hitbox 조회. `MainLifetimeScope` 에 Provider 등록(`AddressKeys.Data.SkillCatalog`).
- **자산**: `Assets/GameData/Skill/{Skill_BasicSwing, Skill_HeavySwing, SkillCatalogDefinition}.asset`(카탈로그 Addressable, address=path). heavy_swing=넓은 hitbox·느린 타이밍(멀티스킬 입증).
- **CC 융합**: `onHitEffectIds[]` 가 이미 효과 id 를 담으므로 스킬에 stun/slow CC 부착은 **데이터 경로 준비됨**. 단 player→monster CC 는 몬스터가 스턴 태그를 honoring 안 함(별도 사안). 몬스터→플레이어 CC 는 `monsters.json onHitEffectId` 로 이미 작동(§2.48).
- **멀티스킬 입력(2026-06-26)**: `HeavyAttack`(우클릭) 입력 신설로 heavy_swing 인게임 트리거. `.inputactions` 액션 추가 + **입력래퍼 `.meta` 생성경로 정정**(stale `Assets/Script/Input/`·Game.Input → 실사용 `Assets/Script/Gameplay/Input/`·Game.Gameplay.Input — `PlayerInputComponent` 이 쓰는 래퍼가 재생성되게). 입력 계약 확장: `CharacterInputFrame.HeavyAttackPressed` + `ICharacterInputWriter.PressHeavyAttack` + `ICharacterInputSource.ConsumeHeavyAttackPressed` + `CharacterInputBuffer` + `PlayerInputComponent.OnHeavyAttack`. `PlayerCharacterAgent.OnAttackPerformed` → `Action<int skillId>`(0=좌/basic·1=우/heavy) → 던전 `CombatSyncSender` C_Attack{SkillId} 가변 / Main `LocalCombat.PerformHit(skillId)` 가 스킬 hitbox 해석(`SkillName(int)`=ResolveSkill 동일 규약). **발동권위는 무변경 확보**(서버 SkillTimeline.CooldownMs 게이트가 heavy 1200ms 강제).
- **클라 쿨다운 예측(2026-06-26)**: `PlayerCharacterAgent`(`SkillCatalogProvider` 주입, Main/Dungeon) `_lastCastTime[skillId]` → `FireSkill`/`SkillCooldownReady`(skill.CooldownMs) 클라 게이트. dodge=`DodgeDriver.CanBegin` 기존.
- **마나 데이터(2026-06-26)**: `SkillTimeline.ManaCost` + skills.json `manaCost`(basic0/heavy20, 자산↔JSON↔서버 정렬) + Infra 파서 + 클라 `SkillDefinition.manaCost`/Exporter + `DodgeConfig.ManaCost=15` + 프리팹 ASC `Mana(100,100)`.
- **마나 게이트/검증/리젠/동기화 완료(2026-06-26)**: 권위=서버, 클라 예측. 리젠 단일소스 `Shared.Gameplay/Combat/ManaConfig.RegenPerSecond=10` + 패킷 `S_PlayerMana`(Union 1642, owner-only 정정).
  - **서버**: `PlayerState.{Mana,MaxMana,TrySpendMana,RegenMana}` · `TryBeginDodge(nowMs,manaCost)` 원자 게이트(쿨다운+마나) · `CombatHandler.HandleAttack`(마나게이트→쿨다운→차감→owner 정정, 무료 basic 생략) · `DodgeHandler`(ManaCost 15 게이트+정정) · `RoomTickService`→`Room.RegenAllPlayerMana`(매 틱, **동기화 패킷 없음**) · `MaxMana` 전파 `PlayerStats.MaxMana`→`PlayerInfo.MaxMana`→`Room.InitPlayerState` 시드(Lv1=50, 폴백 `DefaultMaxMana=100`) · `RoomJoinLeaveHandler` 입장 시 초기 `S_PlayerMana`.
  - **클라**: `PlayerCharacterAgent.{HasMana,SpendMana,RegenMana}`(`FireSkill` 마나→쿨다운 순·`HandleDodgeInput` 게이트, 매 프레임 리젠) · `ManaPacketHandler`→`ISocketPacketState.OnManaUpdated`→`EffectReceiver` 로컬 ASC `SetMax`+`SetCurrent` 정정.
  - **동기화 정책** = 차감/거부/입장 시점만(리젠은 클라 동일 rate 예측 수렴, per-tick 스팸 회피 — 사용자 결정).
- **공격=이름있는 GameplayAbility 식별 + 발동 로그(2026-06-27, 경량)**: 정식 GAS Ability 실행엔진(CA-3)은 보류 유지 — **새 클래스/패킷 0, 로직 무변경**, AbilityId 식별·로그만. 플레이어 공격=스킬 id(`SkillTimeline.Id`, basic_swing/heavy_swing)가 곧 AbilityId → `PlayerCharacterAgent.FireSkill` 가 발동 시 `[GameplayAbility] 발동: '{id}' (mana,cd)` 로그. 몬스터 공격=`'{monsterId}_attack'` 규약 AbilityId → Main `LocalMonster`(`attackAbilityId` SerializeField, 기본 slime_attack) `TryAttack` 로그 + 던전 서버 `Room.TickMonsters` 발동 로그. 검증: 서버빌드0 + Unity컴파일0(로그/식별만). 사용자 플레이 시 콘솔에서 발동 어빌리티 확인용.
- **검증**: SocketServer.Tests 114/114(`SkillCatalogTests` 4 + `Combat/PlayerStateManaTests` 4 신규) + EditMode 152/152(`SkillCatalogProviderTests`·멀티입력·쿨다운 무회귀) + 서버솔루션빌드0 + Unity컴파일0 + ClientCodegen 미러 재생성 + Shared.Gameplay.dll 재배치 + **던전 마나 E2E 2/2**(`SocketE2ETests.RawSocket_입장하면_초기_마나_S_PlayerMana_수신`·`RawSocket_회피하면_서버가_마나를_차감해_S_PlayerMana_정정한다`, 신선 Docker). **HUD 마나바는 이미 완비**(HP/MP/버프 동일 MVI 경로 §2.25b: `InGameModel.OnAttributeChanged(Mana)`→`MpChanged`→`InGameReducer.WithMp`→`GameHud.mpSlider`(MP_Ball)). 마나 차감/리젠 도입 전까진 값 불변이라 공이 고정돼 보였을 뿐 — 차감/리젠으로 실제 변동. **잔여**: 코드시드 `Shared.Gameplay.SkillCatalog` 폐기.

### 2.50 Co-op 부활 — 다운 아군 살리기 (2.5.2, 2026-06-29)
서버 권위. 흐름: 아군 다운(State.Dead/HP0) → 시전자 근접+Interact 홀드(채널) → 서버 검증(거리·다운상태·미실패) → HP 부분복구+다운해제 → S_PlayerRevived 브로드캐스트(원격 가시성).
- **Shared**: `Shared.Gameplay/Combat/ReviveConfig`(Range 2.5m·Hold 3s·Restore 50%, DodgeConfig 형제) + 패킷 `C_Revive`(Union 1824, TargetUserId)·`S_PlayerRevived`(1825, UserId/Hp).
- **서버**: `Room.TryRevive(reviverId,targetId)` — 검증 ①자기제외 ②`_outcome≠Failed` ③시전자 생존·입장·미끊김 ④대상 다운(`_downed`) ⑤평면거리≤Range. 통과 시 `_downed.Remove`(멱등)+`Hp=MaxHp×RestorePercent/100`. `PacketHandler/Handler/ReviveHandler`(C_Revive)→`TryRevive`→`S_PlayerRevived` 브로드캐스트.
- **클라(던전 전용)**: `Gameplay/Character/DownedAllyMarker`(정적 레지스트리 `FindNearest`, 원격 다운 캐릭터에 부착=부활 대상) · `ReviveInteractor`(**사거리 안 다운아군+E 즉시 부활송신** — 홀드 채널 제거(2026-06-29, 사용자 요청). **입력=`GetComponent<ICharacterInputSource>`**(DI 아님 — `CharacterInputBuffer` 컴포넌트, CharacterAgent 동일. ⚠️ 이전 `_container.TryResolve`(DI)는 항상 null→Update 무동작·로그0 버그였음). 세션만 `Configure` 주입. `[DefaultExecutionOrder(-10)]` Interact 우선소비. 활성화·범위 진입/이탈 로그) · `PlayerCharacterAgent.ReviveInPlace(hp)`(State.Dead 해제+서버 HP, **텔레포트 X**) · `RevivePacketHandler`→`ISocketPacketState.OnPlayerRevived`→`CharacterSpawner.HandlePlayerRevived`(로컬=ReviveInPlace/원격=마커 제거).
- **핵심 변경**: `CharacterSpawner.HandlePlayerDead` 원격 분기 = **Destroy→다운 보존**(2.5.1 ⓔ-2 교체). 부활하려면 다운 아군이 상호작용 대상으로 살아 있어야 함. 홀드시간=클라 UX, 서버는 게임의미 불변식(거리/상태)만 검증(사용자 결정 — 부활은 저위험).
- **DI 주의(2026-06-29 회귀수정)**: `ReviveInteractor` 입력은 `[Inject]` 금지 — 입력 미등록 스코프(스폰 전용 PlayMode 테스트)에서 `_container.Inject` 가 `ICharacterInputSource` 미해결로 throw(`CharacterSpawnMultiClientTests` 2건 깨짐). → `Configure(session, input)` 수동주입 + `CharacterSpawner` 가 `TryResolve`(미해결 시 null → Update no-op). **전체 PlayMode 실행으로 포착**(단위/컴파일만으론 못 잡음 — [[verify-before-user-playtest]]).
- **검증**: SocketServer.Tests 118/118(`Combat/ReviveTests` 4 신규=50%복구/거리거부/멱등/자기제외) + 서버솔루션빌드0 + Unity컴파일0 + **던전 부활 E2E 1/1**(`SocketE2ETests.RawSocket_다운된_아군을_부활하면_양쪽_S_PlayerRevived_수신`, 신선 Docker) + **PlayMode 156/156·EditMode 152/152** + **MPPM 2창 플레이 검증 완료(2026-06-29: 다운→E 부활→복귀 전 경로 로그 확인)**. **잔여**: 다운/부활 Animator 포즈(미배선=로그).

### 2.48 상태이상/CC — 스턴·슬로우 (2.6.2, 2026-06-26)

**핵심: 기존 GAS 머신러리 완전 재사용 — 새 패킷 0.** CC = "GrantedTags 를 부여하는 Duration 효과" + "그 태그를 폴링하는 게이트". `GameplayEffectDefinition.GrantedTags`·`ASC.HasTag`(활성효과 동적 합산)·`ASC.Tick`(자동 만료)가 이미 존재 → 정의+게이트만 추가.

- **Shared(DLL 1회 편집으로 클라·서버)**: `GameplayTags.Stun/Slow` + `GameplayEffectCatalog.SeedDefaults` 에 `stun_1_5s`(Dur1500,Granted=[Stun])·`slow_3s`(Dur3000,Granted=[Slow], modifier 없음=순수 상태태그) + `Shared.Gameplay/Combat/CcConfig.cs`(SlowMultiplier=0.5). 서버 `CombatEffectCatalog` 가 같은 catalog 위임 → 자동 합류.
- **클라 게이트**: `PlayerCharacterAgent.Update` Stun 게이트(입력/이동 정지 + 진행중 `_dodge.Cancel`, 사망과 달리 ASC.Tick 으로 자동 만료→해제) · `GroundState.StateUpdate` Slow(`HasTag(Slow)`→targetSpeed×SlowMultiplier, `StateFactory` 가 `context.AbilitySystem` 주입).
- **소스(부여)**: 던전(서버권위)=`monsters.json onHitEffectId`(slime=slow_3s) → `MonsterDef`/`MonsterStats` 운반 → `Room.TickMonsters` 가 데미지 `S_ApplyEffect` 와 함께 `S_ApplyEffect{EffectId=cc, Amount=0}` 브로드캐스트 → **기존 `EffectReceiver`→`ApplyEffectAuthoritative` 경로 그대로**. Main(클라권위)=`LocalMonster.onHitCcId`(slow_3s) + 주입된 `GameplayEffectCatalog` 로 로컬 `ApplyEffect`. bake 파이프라인 전체 반영(`MonsterCatalogDefinition` SO 필드 + `MonsterCatalogExporter` + monsters.json).
- **권위 주의**: 스턴/슬로우 게이트는 본질적으로 클라(자기 입력 제어). 서버는 효과 브로드캐스트만, 클라가 준수. 자기 CC 무시 치팅은 PvE 저위험 → dodge 처럼 서버 입력 거부 하드닝은 후속.
- **넉백(2026-06-26)**: CC 와 별 메커니즘(태그 아닌 위치 임펄스). `Gameplay/Character/KnockbackDriver.cs`(순수C#, DodgeDriver 형제) — `CharacterMotor.Dash(worldDir,speed,faceDirection:false)`(회전 없이 밀림) 으로 distance/duration 강제 변위. `PlayerCharacterAgent.ApplyKnockback(sourcePos,distance,duration)` **public API**(방향=공격자→피격자, 추후 GameplayEffect/Ability 가 이 진입점으로 융합) + Update 게이트(`_knockback.IsActive` → 스턴보다 우선, 밀려나는 중 입력 차단). 테스트 배선=`LocalMonster.knockbackOnHit`(토글+거리/시간). **스턴 이동정지 견고화**: 게이트가 `base.Update()` 전 return → Motor 이동은 이미 정지(플레이어 이동 경로=Motor.Move 단일). 추가로 애니 `Speed=0`(걷기 클립/루트모션 정지, 애니 캐릭터 대비). 검증: PlayMode `DodgeDriverTests` 4(넉백 2 신규). Main 슬라임 데모=stun_1_5s+knockback / 던전=slow_3s.
- **검증**: SocketServer.Tests 106/106(CC 브로드캐스트 1 신규 + slime 이 2효과화돼 깨진 데미지패킷 단정 4곳을 `EffectId=="monster_attack_dmg"` 특정으로 정정) + PlayMode `CcGateTests` 1(스턴 억제·만료 재개) + `DodgeDriverTests` 4(넉백 2) + **던전 CC E2E `SocketE2ETests.RawSocket_몬스터_공격은_슬로우_CC도_함께_브로드캐스트한다` 1/1**(신선 Docker — 슬라임 공격→서버 `slow_3s` S_ApplyEffect) + 양 빌드0 + Unity 컴파일0. 스턴(Main)·넉백(Main)·슬로우(던전) 플레이 검증 완료(사용자). **잔여**: 넉백의 던전(서버권위) 경로 = **Ability 융합 단계로 연기**(현재 Main 클라 전용) · CC 입력거부 서버 하드닝.

### 2.47 회피(Dodge) + 3인칭 카메라 Follow 수정 (2.6.1, 2026-06-26)

**① 회피(Dodge)** — Action 축 임펄스(FSM 상태 아님 — CA-1). 입력(LeftCtrl)은 기존 배선, **발동 소비자만 추가**.
- 코어: `Gameplay/Character/DodgeDriver.cs`(순수 C#) — 고정 월드방향 대시(`CharacterMotor.Dash` 신규)+`State.Invulnerable` i-frame 태그+애니 트리거(`AnimationTriggerType.Dodge`). 소유=`PlayerCharacterAgent`(폴링 `HandleDodgeInput`, 활성 동안 FSM·다른 Action 게이트). 방향=입력시 카메라기준(`Motor.ResolveWorldMoveDirection`)/무입력시 정면.
- **무적 권위 정합**(authority-model): Main(클라권위)=`LocalMonster.TryAttack`가 `target.HasTag(Invulnerable)` 게이트 / 던전(서버권위)=`OnDodgePerformed`→`DodgeSyncSender`→`C_Dodge`(Union 1602)→`SocketServer/.../Handler/DodgeHandler`→`PlayerState.TryBeginDodge`(서버 쿨다운 검증=**C_Dodge 연사 영구무적 치팅 차단**)→`Room.TickMonsters`가 `IsInvulnerableAt` 동안 피해 무시(헛스윙).
- **수치 단일소스** = `Shared.Gameplay/Combat/DodgeConfig.cs`(IframeMs 500·CooldownMs 1000, 클라·서버 공유). 대시 속도/지속(연출)만 클라 `LocomotionSettings`. 태그=`GameplayTags.Invulnerable`("State.Invulnerable").
- 검증: SocketServer.Tests `Combat/DodgeIframeTests` 2 + PlayMode `DodgeDriverTests` 2. **잔여**: Animator dodge 클립/파라미터 배선(미배선=조용히 스킵)·플레이검증·던전 dodge E2E.

**② 3인칭 카메라** — 원인=씬 와이어링(vcam `Follow`=NULL → 런타임 스폰 플레이어 미추적). 코드는 이미 3인칭(`PlayerRotationStrategy` 카메라기준 이동·`CharacterCameraFollow` Look 회전).
- `Gameplay/Camera/GameplayCameraRig.cs`(신규): `LocalPlayerContext.OnSet` 구독→vcam.Follow=플레이어 `CameraFollowTarget`. Main/Dungeon LifetimeScope에 `RegisterComponentInHierarchy`. CharacterSpawner 무변경(랑데부 재사용, `DialogueCameraController` 패턴).
- 씬: Main=`Third Person Aim Camera`에 rig 부착·`CinemachineThirdPersonAim` 제거·`PlayerCharacter` 프리팹 `CameraFollowTarget` Y 0→1.5. Dungeon=Cinemachine 부재라 리그 신설(Brain+CinemachineCamera+ThirdPersonFollow+rig).

**부수**: slime AttackDamage 5→15 밸런스(커밋 `899aa114`)에 안 따라온 스테일 테스트 3개(`MonsterCatalogTests`·`MonsterAttackTests` Defense 2) 기대값 정정.

### 2.46 상점 구매 실패 = AlertPopup 가시화 (2026-06-25)
- **무엇**: 구매(Buy) 실패가 `Shop.cs ShowToast`의 인-윈도우 토스트로만 처리됐는데, **`toastText` 미할당이면 `Debug.Log` 폴백**이라 인게임에 안 보임(콘솔에서만). → 실패는 **`AlertPopup`**(prefab 자체 Addressable 로드라 필드배선 불요, 명시적)로 띄움. 성공은 인-윈도우 토스트 유지.
- **메시지 정정**: `ShopModel.BuyAsync` 실패 메시지가 항상 `"골드가 부족합니다"`로 하드코딩 → 실제 사유는 서버 권위(code=1005 InvalidRequest 등 골드와 무관할 수 있음). 단정 제거 → `"구매에 실패했습니다. 골드 또는 구매 조건을 확인하세요."`.
- **패턴**: `QuestNotificationPresenter`·`LobbyViewController` 와 동일(`AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, destroyCancellationToken)` → `AlertPopup.Setup(title, msg, glow:Warning)` + `SetAddressableOwner`). View(GUI)가 띄움 = MVI 준수(Presentation은 메시지만 생성).
- **위치**: `GUI/Shop/Shop.cs`(ShowToast 실패→`ShowFailPopupAsync`, 미사용 ToastFail 제거) · `Presentation/Shop/ShopModel.cs`(메시지).
- **후속 보완(2026-06-25)**: ① **구매 실패 사유 클라 계산** — 서버 1005는 골드 부족이 흔한 원인. `ShopModel.BuyAsync`가 **보유 골드(state.Gold) vs 총가격(BuyPrice×Qty)** 비교로 사유 추론 → `"골드가 부족합니다.\n보유 X / 필요 Y"`(N0 포맷) 또는 일반 사유. 서버는 권위 게이트, 클라는 설명. ② **인벤토리 판매/소비/장착 실패도 팝업화** — `InventoryModel.OnToast`를 `string`→**`InventoryToast{Message,Success}`**(Shop의 `ShopToastMessage` 동형)로 refactor, 7개 OnNext에 성공/실패 플래그. `Inventory.cs`가 실패=AlertPopup·성공=로그(`ShowToast`/`ShowFailPopupAsync`). 검증: CS0 + **PlayMode `InventoryModelTests`+`ShopModelTests` 15/15**(테스트 `OnToast` 구독을 `.Message`로 갱신).

### 2.45 게임 데이터 에셋 Resources 폐기 → Addressables/GameData (2026-06-25)
- **무엇**: 모든 게임 데이터 SO를 **`Resources/` 밖**으로. `Resources` 폴더는 빌드에 무조건 전부 포함(온디맨드 아님)이라 폐기. 전부 `Assets/GameData/<컨텐츠>/`로 이동 + 런타임 로드는 **Addressables**(address=에셋 경로). (`VContainerSettings.asset`만 VContainer 요구로 `Assets/Resources/`에 잔존.)
- **규약(최종)**:
  - **위치**: `Assets/GameData/<컨텐츠>/` — `Maps/`·`Effects/`·`Item/`·`Consumable/`·`Dialogue/`·`Monster/`·`Loot/`·`Progression/`·`DungeonLobby/`.
  - **런타임 로드 SO(Addressables)**: MapDefinition(맵) + 카탈로그 5종(EffectIcon·ItemDisplay·GradeSprite·Consumable·Dialogue). address = 에셋 경로(`Assets/GameData/...asset`), "Default Local Group" 등록. 주소상수 = `Game.GUI.AddressKeys.Data.*`(카탈로그) / MapLoader는 로컬(`$"Assets/GameData/Maps/{mapId}.asset"`, Game.Gameplay→Game.GUI 역참조 회피).
  - **저작 전용 SO(런타임 미로드)**: DropTable·LevelTable·Monster — 클라·서버 모두 bake된 JSON을 읽고 SO는 Export 소스일 뿐 → Addressables 불요, GameData에 두기만. (코드 변경 0, 익스포터 `AssetDir`만 GameData로.)
  - **GUID 참조 에셋**: `DungeonLobby/DungeonCatalog.asset`(팝업 프리팹 SerializeField)·`Dialogue/Dialogue_Elder.asset`(DialogueCatalog 참조) — Addressable 카탈로그 로드 시 의존성으로 자동 번들.
- **로딩 코드**: `MapLoader`=async `Addressables.LoadAssetAsync<MapDefinition>(addr)`(await+Release). `Dungeon/MainLifetimeScope`=동기 `Configure`라 `LoadData<T>(addr)`=`LoadAssetAsync<T>(addr).WaitForCompletion()`(로컬 번들 동기 로드, 씬 수명 카탈로그라 핸들 보존). 미등록 주소면 null→빈 SO 폴백.
- **asmdef**: `Game.Gameplay`·`Game.VContainer`·`Game.Tests.EditMode`에 `Unity.Addressables`+`Unity.ResourceManager` 참조 추가.
- **검증**: CS0 + Addressables 7주소 해석 OK·구 Resources 키 NULL + `EffectIconCatalogAssetTests`(Addressables)·`SpawnResolverTests` 5/5. 신규 폴더/이동 `.meta` 커밋 시 `git add -f`([[unity-meta-gitignored]]). ※중간에 `Assets/Resources/<컨텐츠>` 통합을 거쳤으나 "Resources=빌드 항상포함" 때문에 본 Addressables 안으로 최종 대체.

### 2.44 캐릭터 정보/스탯창 (7.3) (2026-06-25)
- **무엇**: 중앙 모달 패널 — 레벨/경험치 + 스탯7(체력·마나·공격력·방어력·힘·민첩·지능) = **9개 라인**(라벨:값). 열기 시 서버 권위 GetProgression pull.
- **열기 경로(Quest 창 100% 동형)**: GameHud `btn_Ability`·**G키** → `InGameModel.Accept(ToggleAbility)` → `OnToggleAbility` → `StatViewController`(POCO, IInitializable)가 최초 1회 `StatWindow.prefab` Addressable 로드·Inject → 이후 SetActive 토글.
- **View(`StatWindow`)**: `ProgressionModel` 주입 → Start/OnEnable 시 `Refresh()` + `State` 구독 → `State.Lines` 를 rowTemplate(라벨 TMP + 값 TMP) 복제로 렌더. 닫기=자기 SetActive(false). 창 활성 중 `UiInputCaptureBehaviour`(ProgressionModel.Begin/EndUiCapture) 로 이동 차단.
- **레이어 변환**: GUI 는 System(`ProgressionData`/`ProgressionStats`) 비참조 → Presentation `ProgressionViewState.Loaded` 가 **`StatLine`(string 라벨/값) 목록**으로 변환해 노출(QuestTracker 가 bool 헬퍼 쓴 것과 같은 이유). 색상은 프리팹 TMP 에(라벨=흐림/값=금색).
- **DI**: `ProgressionModel`(Scoped) + `StatViewController`(EntryPoint) = **MainLifetimeScope 등록**(IProgressionService 는 ProjectScope Singleton). `ProgressionModel` 에 `IInputContext` 옵셔널 추가(입력 점유).
- **위치**: `GUI/Stat/{StatWindow,StatViewController}.cs` · `Presentation/Progression/{ProgressionModel,ProgressionViewState,StatLine}.cs` · 프리팹 `Assets/Prefabs/GUI/Stat/StatWindow.prefab`(Addressable) · `GUI/AddressKeys.cs`(UI.StatWindow) · `InGameModel`/`InGameIntent`(ToggleAbility).
- **한계(후속)**: Main 전용(던전 미등록 — 던전 스탯창 원하면 DungeonLifetimeScope 에 ProgressionModel+StatViewController 등록). 프리팹 색/여백 다듬기는 Unity.
- **검증**: 컴파일0 + PlayMode `ProgressionViewStateTests` 3 + `GameHud(Buff)IntegrationTests` 2 + EditMode `InGameModelTests` 4 그린.

### 2.43 퀘스트 추적 HUD (7.4) (2026-06-25)
- **무엇**: GameHud 우상단 `QuestTracker` 패널이 진행 중 퀘스트(이름 + 조건 "slime 처치 2/3")를 표시. 0개면 패널 숨김.
- **인증 레이스 수정(2026-06-25 후속)**: 에디터 자동 로그인이 async라 토큰 채워지기 전 `GetQuests`(저널 열기·`OnProgressionChanged`·대화)가 먼저 발사되면 **401 "Authorization header is missing"**. → `QuestService`(System)에 `AuthSession` optional 주입 + 4개 메서드(GetQuests/Accept/Claim/ReportTalk) 시작에 `await AuthenticatedAsync().AttachExternalCancellation(ct)` 게이트(+`OperationCanceledException` 정상종료 catch). `PlayerProgressionHolder`·`LobbyModel` 동일 패턴. System 레이어에 둬 QuestModel·DialogueModel 양 호출자 일괄 보호. 런타임 검증: 재생 중 `QuestService._authSession=INJECTED·IsAuthenticated=True`. 테스트는 `FakeQuestService` 사용이라 실 생성자 무영향.
- **흐름**: `QuestTrackerView`(GUI, GameHud 루트에 부착) → Start 시 QuestModel 구독 + `Accept(Refresh)` → `State.Quests` 중 진행중만 추려 행(TMP) 동적 풀 렌더.
- **수락/보상 즉시 반영**: NPC 대화 수락/완료/보상은 `DialogueModel` 이 `IQuestService` 를 **직접** 호출 → QuestModel.State 미갱신(트래커가 안 뜸). 그래서 두 경로의 **단일 알림 소스 `QuestNotifier.OnNotice`** 를 트래커가 추가 구독해 알림마다 `QuestModel.Accept(Refresh)` → 즉시 갱신(수락=등장, 보상=사라짐). QuestNotifier 도 Main 전용이라 동일 TryResolve.
- **킬 진행도 즉시 반영**: 진행(ReportKill)은 서버 내부라 클라 RPC·신호 없음. 단, 킬 직후 `MainMonsterSpawner` 가 exp/스탯을 `PlayerProgressionHolder.RefreshAsync` → `OnChanged` 발화. 이를 **QuestModel 이 옵셔널 주입받아 구독 → self `RefreshAsync`** → 진행 카운트(2/3→3/3)·완료 전이가 트래커/퀘스트창에 즉시 반영. 레이어: PlayerProgressionHolder=System → **QuestModel(Presentation)이 구독**(GUI 트래커는 System 비참조라 불가). holder 는 Main·던전 모두 Scoped 등록이라 Main QuestModel 이 해소.
- **진행중 판정**: GUI 는 System 타입(`QuestProgressState`) 비참조 → QuestEntryModel **bool 헬퍼**로 판정 = `!CanAccept && !IsClaimed`(미수주·수령완료 제외 = Accepted/Completed). 순수함수 `QuestTrackerView.InProgress` 로 분리(테스트).
- **DI 핵심**: `QuestModel` 은 **MainLifetimeScope 전용** 등록(ctor 가 Main 전용 `QuestNotifier` 요구 → 던전 등록 시 cascade). GameHud 는 Main·던전 공용이라 하드 [Inject] 시 **던전 GameHud 가 깨진다** → `[Inject] IObjectResolver` + `TryResolve(typeof(QuestModel))` 로 **선택 주입**: Main=구독 / 던전=root.SetActive(false) 숨김. (PlayerStatApplier §2.40 과 동일한 "GameHud 공용 + Main 전용 의존" 회피 패턴.)
- **위치**: `Client/Assets/Script/GUI/Hud/QuestTrackerView.cs` · 프리팹 `Assets/Prefabs/GUI/HUD/GameHud.prefab`(QuestTracker 패널 + Row 템플릿, QuestTrackerView 는 GameHud 루트).
- **한계(후속)**: exp 쿨다운 중 킬은 `OnChanged` 미발화(ExpGained=0 가드) → 그 킬의 진행 카운트는 다음 갱신 때 반영(드문 엣지). 던전 내 추적은 QuestModel·QuestNotifier 던전 스코프 등록 필요(현재 Main 전용).
- **검증**: 컴파일0 + PlayMode `QuestTrackerViewTests` 2 + `GameHud(Buff)IntegrationTests` 2(프리팹 변경 회귀 없음) 그린.

### 2.42 상점 판매(Sell) UI — 인벤토리 판매 (7.6) (2026-06-23)
- **무엇**: 인벤토리에서 비장착 아이템 판매. 서버 Sell(인벤 차감→골드 적립)은 기존 완비(proto `Sell`·`ShopService.SellAsync`·`ShopSellResult`) — 이번엔 클라 배선만.
- **흐름**: 인벤 아이템 클릭 → `ItemActionPanel`(use/equip/**sell**) → Sell 버튼 → `Inventory.ShowSellConfirmAsync`(가격 = `InventoryModel.GetSellPriceAsync` = 서버 GetShop `sell_price` 1회 캐시) → `ConfirmPopup`("…{price}골드에 판매?", 확인/취소) → 확인 시 `Accept(InventoryIntent.SellItem)` → `InventoryModel.SellItemAsync` → `IShopService.SellAsync(itemId,1)`(서버 권위) → 성공 시 `RefreshAsync`(아이템+골드 갱신)+토스트. 취소=팝업만 닫힘.
- **장착품 제외**: `InventoryModel.RefreshAsync` 가 착용 itemId 를 표시에서 이미 제외(§2.27 ⑨) → 판매 버튼이 애초에 안 뜸(추가 작업 0).
- **결정**: 가격=서버 `sell_price`(표시용, SellResponse gold 가 최종 권위) · **1개/확인**(스택 수량 선택은 후속) · `ItemActionPanel.Bind` 에 onSell/canSell 추가 → 인벤 canSell=true / 장비창 canSell=false. 가격 룩업은 `InventoryModel` 의 public 쿼리(View 가 팝업에 표시) — 실제 차감은 Intent 경유(MVI 유지).
- **DI**: `IShopService` 는 ProjectLifetimeScope Singleton(ShopInstaller) → InventoryModel(Main·던전)이 모두 해소. InventoryModel ctor 에 `IShopService shop=null` 옵셔널 추가(테스트 하네스 무영향).
- **위치**: `System/Shop/{IShopService,ShopService}.cs`(SellAsync) · `Presentation/Inventory/{InventoryModel(GetSellPriceAsync/SellItemAsync),InventoryIntent(SellItem)}.cs` · `GUI/Inventory/{ItemActionPanel(Bind+sellButton),Inventory(ShowSellConfirmAsync)}.cs` · `GUI/Equipment/Equipment.cs`(Bind canSell=false).
- **검증**: 클라 컴파일0 + PlayMode `InventoryModelTests` 8(판매가 룩업·SellItem 위임 2 신규) + `ShopModelTests` 7 그린. 잔여: 인게임 플레이 검증(프리팹 sellButton 사용자 할당).

### 2.41 클라 ASC HP 기준선 = 서버 레벨 스탯 동기화 (2026-06-23)
- **무엇**: 로컬 플레이어 ASC 의 Health(Max/Current)를 서버 권위 레벨 MaxHealth 로 정렬. Main·던전 공통.
- **증상**: 레벨업한 캐릭터가 던전에서 **다운된 뒤에도 몬스터가 한참 더 공격**. `[로컬 다운]`(클라 HP≤0 즉발 입력게이트)과 `[S_PlayerDead 수신]`(서버 HP≤0) 사이 큰 간격.
- **원인**: 클라 ASC HP = **prefab 고정 100**, 서버 던전 HP = **레벨 maxHealth**(level-table: Lv1 100/Lv2 120/Lv3 140…, `RoomManager.CreateRoom`→`InitPlayerState(playerInfo.MaxHealth)`). 둘 다 같은 `S_ApplyEffect(-1)` 적용하지만 **기준선이 달라** 클라(100)가 먼저 0 → 로컬 다운, 서버(140)는 잔여 HP만큼 계속 공격 → 그 차이×쿨다운이 그 "Delay". `S_PlayerJoined` 엔 HP 필드 없음.
- **왜 누락됐나**: 전투 HP=서버권위라 클라는 HP를 *계산 안 하고* `S_ApplyEffect` 델타로 *렌더만* → ASC HP 기준선을 prefab 100 그대로 방치. "즉발 손맛"용 로컬 사망 *예측*만 클라 ASC HP 를 직접 읽어 desync 가 표면화. **스탯 동기화 자체는 이미 존재**(서버 `GetProgression`→`StatBlock`, 클라 `PlayerProgressionHolder.Stats`(MaxHealth/Def/Atk), Main `LocalCombat`이 Def/Atk 사용) — **마지막 한 칸(동기화된 MaxHealth→ASC 적용)만** 빠졌던 것.
- **수정(클라 전용, 패킷 변경 0)**: `GameplayAttribute.SetMax(int)` 추가 + `PlayerStatApplier`(MonoBehaviour) — 로컬 스폰 시 `CharacterSpawner.SpawnLocalAsync` 에서 부착. 홀더는 **하드 `[Inject]` 아닌 `Bind(holder)`** + `CharacterSpawner` 가 `_container.TryResolve(PlayerProgressionHolder)` 로 **있으면 연결/없으면 생략**(미등록 테스트 하네스도 스폰 정상 — "주입 의존 추가→DI호스트 파손" 재발 방지). 스폰 시 `holder.Stats.MaxHealth` 로 Max 정렬+풀피, `OnChanged`(레벨업) 시 재적용(MaxHealth 실제 변화 시만 — 킬마다 풀힐 방지). 서버 던전 HP 도 같은 level-table maxHealth 라 클라==서버 → 로컬다운==S_PlayerDead(간격 0). (앞서 검토한 S_PlayerJoined 에 HP 추가 안은 철회 — 데이터는 이미 클라에 있음.)
- **위치**: `System/GameplayAbilitySystem/Attribute/GameplayAttribute.cs`(SetMax) · `Gameplay/Character/PlayerStatApplier.cs`(신규) · `Gameplay/Character/CharacterSpawner.cs`(부착) · 테스트 `Tests/PlayMode/Character/PlayerStatApplierTests.cs`.
- **검증**: 클라 컴파일0 + PlayMode `PlayerStatApplierTests` 3/3(정렬+풀피 / 무변화 재적용 풀힐금지 / 0 미갱신 prefab유지). 죽음→DungeonFailed 흐름 자체는 기존 `SocketE2ETests` E2E 가 커버. (레벨업+던전 사망 타이밍 풀 E2E 는 150초+ 라 비추가 — 단위테스트로 기준선 가드.)
- **밸런스(적용 2026-06-23)**: 슬라임 `attackDamage` 5→**15**(`monsters.json`) — vs Def5 = 10뎀/타 → HP100을 ~10타(쿨다운 1.5s = 약 15초)에 사망. 기존 5는 max(1,5−5)=1뎀 → 100타(150초)로 패배가 비현실적으로 느렸던 것 보정(사용자 ~10타 선택). socketserver 리빌드·재배포 반영. 위 HP-sync 와 결합해 로컬다운==S_PlayerDead==패배 화면이 ~10타에 일관 발생.

### 2.40 클라 하트비트 + 연결처리 E2E 커버리지 + 재발방지 가드 (2026-06-23)
- **무엇**: ① 클라 keep-alive 하트비트(근본 원인 수정) ② 누락됐던 연결 생존성 E2E 추가 ③ 연결 소스 변경 시 테스트 누락을 경고하는 Stop 훅 + 커버리지 정책.
- **근본 원인**: 서버 `HeartBeatService`는 방 플레이어가 60s 무패킷이면 퇴장(`LastRecvAt` 기준, 수신 패킷마다 갱신). 클라는 **이동 시에만 C_Move** 송신 → 제자리 전투/대화/AFK 60s = 무패킷 → 서버가 끊음. 프로토콜·서버·DummyClient엔 핑 루프가 있었으나 **Unity 클라만 C_Ping 송신부 미구현**(S_Pong 핸들러도 없음 — dispatcher가 미등록 패킷 무시라 무해).
- **수정**: `SocketSession`에 `HeartbeatInterval`(기본 15s, **프로퍼티** — ctor 주입 시 VContainer가 TimeSpan 미해소로 DI 깨짐) + `RunHeartbeatLoopAsync`(Connected~Joined 동안 `C_Ping{IsHealthy=true}` 주기 송신, 세션 토큰 취소 시 종료). 서버는 어떤 수신이든 `LastRecvAt` 갱신 → 유휴여도 생존.
- **테스트(누락 메움)**: ⓐ 빠른 단위 `Tests/PlayMode/Network/Socket/SocketSessionHeartbeatTests`(Fake 커넥터, 짧은 인터벌 — Joined 동안 C_Ping 송신·끊김 후 중단, Docker/60s 불필요) 2/2. ⓑ E2E `SocketE2ETests`: `세션배정_없는_UserId로_입장하면_거부된다`(C_PlayerJoin Redis 검증=소켓 인증, C_Auth 패킷 없음) + `유휴시_핑있으면_연결유지_핑없으면_서버가_끊고_OnDisconnected가_발화한다`(~80s, `[Timeout]`+`ignoreTimeScale`, 세션 수명 토큰=`CancellationToken.None`. 핑 호스트 생존 / 핑off 게스트 서버 퇴장+OnDisconnected). ⓒ EditMode `InGameModelTests` 끊김 단위(직전 §2.39).
- **재발 방지**: Stop 훅 `.claude/hooks/check-network-e2e-coverage.ps1` — 연결 소스(`Network/Socket`, `SocketServer`, 패킷) 변경 시 소켓 E2E/`SocketServer.Tests` 동반 변경 없으면 경고(settings.json Stop 등록). + `.claude/rules/testing.md` "연결 처리(소켓) E2E 커버리지 정책"(체크리스트 + 느린/시간기반 테스트 작성법). **교훈**: E2E가 해피패스만 덮고 liveness/실패모드 카테고리가 비어 하트비트 누락이 새어나감.
- **위치**: `Network/Socket/Session/SocketSession.cs`(HeartbeatInterval+RunHeartbeatLoopAsync) · `Tests/PlayMode/Network/Socket/SocketSessionHeartbeatTests.cs`(신규) · `Tests/PlayMode/E2E/Network/Socket/SocketE2ETests.cs`(+2 E2E, ConnectJoinedSessionAsync 에 heartbeatInterval 옵셔널) · `.claude/hooks/check-network-e2e-coverage.ps1` · `.claude/settings.json` · `.claude/rules/testing.md`.
- **검증**: 클라 컴파일0 + 단위 2/2 + 신규 E2E 통과(거부 + 유휴 생존/끊김 81.8s) + 기존 SocketE2E 회귀 확인.

### 2.39 던전 중 비정상 끊김 처리 (6.4) (2026-06-23)

### 2.39 던전 중 비정상 끊김 처리 (6.4) (2026-06-23)
- **무엇**: 던전 플레이 중 소켓이 비정상으로 끊기면(서버 다운/네트워크 절단) 입력 정지 + "연결 끊김" 팝업 → 확인 시 Main 복귀. 그 전엔 끊겨도 클라가 던전에 갇혀 `MoveSyncSender`가 "not joined" 예외를 매 프레임 던졌음(실사례: DB 초기화로 서버 재기동 → 진행 중 연결 끊김).
- **이벤트(의도/비의도 구분)**: `ISocketSession.OnDisconnected`(event Action) 신설. `SocketSession`은 `_intentionalDisconnect` 플래그로 구분 — `DisconnectAsync`(정상 퇴장)에서 true 세팅 → 수신 루프 종료 시 발화 안 함. 서버/네트워크 절단으로 수신 루프가 EOF/예외로 끝나면 false → `RunReceiveLoopAsync` finally에서 **`UniTask.SwitchToMainThread()` 후** OnDisconnected 발화(구독자 Unity 작업 안전). `ConnectAsync`가 새 세션마다 false로 리셋.
- **MVI 배선**: GUI는 Network 직접참조 불가 → `InGameModel`(Presentation→Network ✓)이 OnDisconnected 구독, 핸들러(1회 가드)에서 ⓐ `IInputContext.EnterUi()` 입력 정지 ⓑ R3 `OnConnectionLost` side-channel 발행(QuestModel.OnToast 동형). `GameHud`(GUI)가 OnConnectionLost 구독 → `AlertPopup`(Danger, onOk=ReturnToLobby) 표시. **EnterUi 균형**: IInputContext는 루트 Singleton(씬 넘어 유지)이라 누수 시 Main 입력이 막힘 → `InGameModel.Dispose`(던전 스코프 해제=Main 로드 시)에서 `_uiCaptured`면 ExitUi.
- **부수**: `MoveSyncSender.FixedUpdate`가 `State != Joined`면 송신 skip(예외 스팸 제거, 끊김/복귀 전이 구간 방어).
- **위치**: `Network/Socket/Session/{ISocketSession,SocketSession}.cs`(OnDisconnected) · `Gameplay/Character/MoveSyncSender.cs`(가드) · `Presentation/InGame/InGameModel.cs`(구독+EnterUi+OnConnectionLost) · `GUI/Hud/GameHud.cs`(AlertPopup) · 테스트 Fake 9곳 `event global::System.Action OnDisconnected`(System 섀도잉 회피).
- **검증**: 클라 컴파일0 + EditMode `InGameModelTests` 4/4(신규 "비정상끊김시_입력정지하고_OnConnectionLost가_발행된다": 끊김→EnterUi+신호·중복끊김 무시·Dispose ExitUi 균형) + 기존 InGame relay 15/15 유지. **잔여**: 방 자체 유예·자동 재접속 시도(현재 로비 복귀).

### 2.38 퀘스트 보상=NPC 대화 일원화 · 팝업 알림(QuestNotifier) (2026-06-23)
- **무엇**: ① Quest 창의 **수락·보상 버튼 폐지** → 창은 저널(목록+진행) 전용. 수주/보상 수령은 NPC 대화(`DialogueModel`)로만. ② 수락/완료/보상을 **AlertPopup**으로 피드백(기존 `Quest.ShowToast`는 `Debug.Log`뿐이라 화면 피드백 0 = "완료됐는지 확인 불가" 문제 해소).
- **왜 QuestNotifier(단일 소스)**: 수락·보상은 대화에서, 완료 전이는 GetQuests 시점(저널 열기·대화 시작)에서 발생 — 두 모델(QuestModel·DialogueModel)이 같은 알림을 내야 함. 각자 감지하면 완료 알림 **이중 발화** 위험 → 공유 `QuestNotifier`(Presentation)에 last-seen 상태 캐시를 두고 **Completed 로 전이할 때만 1회** 발화(스팸 방지). 응집↑(원칙4), 과추상화 아님(2개 소비자 공유).
- **레이어**: `QuestNotifier`(Presentation)가 `Observable<QuestNotice>` 발행 → `QuestNotificationPresenter`(GUI, 엔트리)가 구독해 `AlertPopup`(GUI/Common, Addressable) 표시. 알림종류(Accepted/Completed/Claimed)→PopupGlowType(Info/Warning/Success) 매핑은 GUI에서. **GUI→Presentation 단방향 유지, 신규 교차참조 0.** (LobbyViewController 의 AlertPopup 로드 패턴 재사용.)
- **경로**: 대화 수락/수령 = `DialogueModel.QuestActionAsync`(성공 시 `NotifyAccepted/NotifyClaimed`) · 완료 감지 = `QuestModel.RefreshAsync` + `DialogueModel.RefreshQuestStatesAsync` 의 `QuestNotifier.Sync(quests)`. 이름·보상요약은 Sync 캐시(`QuestData`)에서 조회.
- **위치**: `Presentation/Quest/QuestNotifier.cs`(신규, +QuestNotice/Kind) · `GUI/Quest/QuestNotificationPresenter.cs`(신규) · `Presentation/Quest/QuestModel.cs`(notifier 주입+Sync) · `Presentation/Dialogue/DialogueModel.cs`(notifier 주입+Notify/Sync) · `GUI/Quest/Quest.cs`(수락·보상 버튼 영구 숨김) · `VContainer/.../MainLifetimeScope.cs`(QuestNotifier Scoped + Presenter 엔트리).
- **검증**: 클라 컴파일0(UnityMCP) + **PlayMode `QuestNotifierTests` 3/3**(완료 전이 1회·보상요약 포함·수락 이름) + 기존 `DialogueModelTests` 12 + `QuestModelTests` 4 = **16/16 유지**(notifier 옵셔널 주입이라 단위테스트 무영향). **잔여(Unity)**: Title→Main 풀플로우 플레이로 팝업 눈 확인.

### 2.37 대화 저작 실배선 — 선택지 UI · 대화중 오브젝트 숨김 · npc_elder 샘플 (2026-06-23)
- **무엇**: ① `Dialogue.prefab` 에 선택지 UI 실제 구성(분기 노드 클릭이 안 되던 버그) ② 그래프툴에서 **대화 중 숨길 GameObject 지정**(HUD 등) ③ `npc_elder` 샘플 대화 + Main 씬 NPC 배치.
- **선택지 버그 원인**: 프리팹에 본문 `Text` 하나뿐 → `choiceContainer`/`choiceTemplate` 미할당 → 선택지 버튼 0개. `DialogueView`는 설계상 선택지 2개↑ 노드는 빈 클릭 무시(버튼 전용)라 시작 노드(다중 선택지)에서 영구 정지. **수정**: 프리팹에 `ChoiceContainer`(VerticalLayoutGroup+ContentSizeFitter) + `ChoiceTemplate`(Button+Label TMP) 추가 후 `DialogueView` 필드 배선.
- **대화중 숨김(그래프툴 지정)**: SO는 씬 참조 불가 → `DialogueDefinition.hideObjects`=**이름/경로 문자열 리스트**(per-대화). 그래프툴 툴바 `TextField "숨길 오브젝트"`(쉼표구분)에서 편집·Save. 런타임: `DialogueModel.RenderNode` 가 `DialogueState.HideObjects` 로 실어 발행 → `DialogueViewController` 가 **열림 엣지 1회** `GameObject.Find`→SetActive(false)+참조보관, **닫힘 엣지** 복원(Dispose 도 복원). 적용=GUI 레이어(씬 접근 허용), 데이터=Presentation, 저작=Editor → **신규 교차참조 0**. 한계: 대화 시작 시 활성·고유 이름 전제(YAGNI).
- **위치**: `Presentation/Dialogue/{DialogueDefinition(hideObjects),DialogueState(HideObjects),DialogueModel(RenderNode)}.cs` · `GUI/Dialogue/DialogueViewController.cs`(Apply/RestoreHide) · `Gameplay/Editor/Dialogue/DialogueGraphWindow.cs`(툴바 TextField) · 에셋 `Prefabs/GUI/Dialogue/Dialogue.prefab`(선택지 UI+Addressable 등록) · `GameData/Dialogue/Dialogue_Elder.asset`(샘플 4노드, 非Resources) · `Assets/GameData/Dialogue/DialogueCatalog.asset`(npc_elder 매핑, Addressable address=경로, §2.45) · `Scenes/Main.unity`(NPC_Elder, Interactive 레이어).
- **검증**: 클라 컴파일0(Editor.log 강제 동기 재컴파일 후 CS 에러 0) + 직전 세션 런타임 대화창 표시(본문 렌더) 확인. **잔여(Unity)**: Title→로그인→Main 풀플로우 플레이로 분기 클릭·HUD 숨김 눈으로 확인(시작씬 Title이라 Main 단독 Play는 스코프 미생성).

### 2.36 TalkToNpc 진행 — 4.5 Phase C (2026-06-22)
- **무엇**: "특정 NPC와 대화" 목표(TalkToNpc) 퀘스트를 서버 권위로 진행. 대화 시작=대화 보고 → 매칭 퀘스트 +1.
- **NpcCatalog 생략(YAGNI)**: 별도 NPC 도메인/화이트리스트 안 만듦 — `ReportTalk`은 수주한 TalkToNpc 퀘스트의 `TargetId==npcId` 로만 매칭하므로 **잘못된 npcId=매칭 0=무진행**(자동 안전). 멱등=`RequiredCount=1` + 진행 상한(`AddProgress` 가 Progress≥Required 면 무효) → 반복 대화 파밍 불가, 별도 멱등 저장 불필요. (킬 훅과 동일 구조로 `QuestService.AdvanceMatchingAsync(objective, targetId)` 로 ReportKill/ReportTalk 공통화.)
- **경로**: 클라 `DialogueModel.StartAsync` → `IQuestService.ReportTalkAsync(npcId)`(대화 시작 시, RefreshQuestStates 전에 호출 → 진행 후 조건부 선택지 즉시 반영) → System `QuestService` → gRPC `ReportTalk` → `QuestGrpcService.ReportTalk`(인증+위임) → `IQuestService.ReportTalkAsync`(Application).
- **⚠️ 신뢰 한계(설계 명시)**: Main NPC=클라 로컬이라 서버가 "진짜 대화했나" 위치 검증 불가 → 클라가 ReportTalk 위조 가능하나 **멱등(퀘스트당 1회)으로 영향 상한**(킬 클레임 쿨다운 사상). 진짜 서버 권위는 NPC 서버 실존하는 4.6 World 세션 합류 시 승격.
- **시드**: `quest_greet_elder`(TalkToNpc, target `npc_elder`, ×1 → exp30+gold50). npcId=DialogueCatalog 키와 동일 컨벤션.
- **위치**: App `Domains/Quest/{IQuestService,QuestService(AdvanceMatchingAsync)}.cs` · Domain `Entities/Quest/QuestCatalog.cs`(시드) · proto `quest.proto`(ReportTalk) · `API/Services/QuestGrpcService.cs` · 클라 `System/Quest/{IQuestService,QuestService}.cs`(ReportTalkAsync) · `Presentation/Dialogue/DialogueModel.cs`(StartAsync→ReportTalk).
- **검증**: **GameServer 375/375**(단위+Testcontainers통합+E2E — QuestService ReportTalk 2 신규) + 클라 컴파일0 + **PlayMode `QuestE2ETests` TalkToNpc 풀루프**(수주→ReportTalk(타대상 무진행/대상 완료)→보상 gold+50) + DialogueModelTests 7/7. **→ 4.5 코드 완결**(A1·A2·A3·B·C). 잔여=Unity 저작/플레이.

### 2.35 대화↔퀘스트 연동 — 4.5 Phase B (2026-06-22)
- **무엇**: 대화 선택지로 퀘스트 수주/보상수령(서버 권위 재사용) + 퀘스트 상태 기반 선택지 조건부 노출.
- **DialogueModel + IQuestService(재사용, 신규 서버 0)**: 선택지 action `AcceptQuest`/`ClaimQuest` → `IQuestService.AcceptAsync`/`ClaimRewardAsync`(4.4 그대로). `showIf`(DialogueShowCondition: QuestNotAccepted/InProgress/Completed/Claimed) → 퀘스트 상태로 선택지 노출 필터(예: 미수주면 "수락"만, 완료면 "보상받기"만).
- **상태 캐시 = 동기 렌더 + 비동기 갱신**: 노드 렌더(showIf 평가)는 매번 gRPC round-trip 하면 비싸므로 `_questStates`(questId→QuestProgressState) 캐시를 **대화 진입(Start)·수락/수령 직후에만** `GetQuests`로 채우고, 노드 렌더는 캐시로 동기 평가. 액션 후 targetNodeId 있으면 이동·없으면 현재 노드 재렌더(조건부 선택지 즉시 갱신). conditionQuestId 미지정이면 가드(null-key Dictionary 크래시 방지 → 표시).
- **레이어**: DialogueModel(Presentation)→IQuestService(Game.System.Quest) ✓(Presentation→System). DI 무변경(IQuestService 는 QuestInstaller 가 루트 등록). 비동기화로 Start/액션이 UniTaskVoid(+CTS 취소).
- **위치**: `Presentation/Dialogue/DialogueModel.cs`(IQuestService·_questStates·QuestActionAsync·IsChoiceVisible·RefreshQuestStatesAsync). 데이터(action/showIf/questId/conditionQuestId)는 A1 모델에 이미 있던 필드 — Phase B 는 평가·실행만 추가(스키마 무변경).
- **검증**: 컴파일0 + **PlayMode `DialogueModelTests` 7/7**(A1 4 + 미수주→수락만·완료→보상만·수락→QuestService 위임 3). **잔여**: 실제 대화 SO 에 AcceptQuest/showIf 저작(A2 툴) + 플레이.

### 2.34 대화 그래프 저작툴 — 4.5 Phase A2 + A1 View 배선 (2026-06-22)
- **무엇**: ① DialogueDefinition 을 노드 그래프로 편집하는 GraphView 에디터(A2) ② A1 `DialogueView`↔`Dialogue.prefab`(본문 바) 배선 + 선형 진행.
- **그래프 툴(Editor 전용, `Gameplay/Editor/Dialogue/`)**: `DialogueGraphWindow`(EditorWindow — 메뉴 `Tools▸Dialogue▸Graph Editor`, DialogueDefinition 더블클릭 `[OnOpenAsset]` 으로 열림, 툴바 ObjectField+Save/Reload) → `DialogueGraphView`(GraphView — 우클릭 Add Node·드래그로 선택지 출력포트↔노드 입력포트 연결·삭제·노드별 "Set as Start"; `PopulateFrom(def)`/`SaveTo(def)`) → `DialogueNodeView`(Node — 입력포트 Multi + speaker/body 필드 + 선택지 행마다 label·action(enum)·questId·showIf(enum)·conditionQuestId·출력포트 Single). **엣지→targetNodeId 변환**(저장 시 output.connections→대상 NodeView.NodeId), `editorPosition` 저장으로 레이아웃 보존. 신규 노드 id=GUID. `def.EditorNodes`(#if UNITY_EDITOR 접근자)로 직렬화. GraphView=`UnityEditor.Experimental.GraphView`(Unity6 가용 확인).
- **A1 View 배선**: `DialogueView` 를 `Dialogue.prefab` 루트에 추가(YAML), `bodyText`→자식 Text(TMP) 연결(speaker/choice/close=미사용=null). **선형 진행 추가**: 본문 전용 바(선택지 버튼 없음)라 `Update` 에서 클릭/Enter/Space 폴링 → 노출 선택지 1개면 그 선택지 진행·0개면 닫기·2개+면 무시(분기는 choiceContainer/template 있는 프리팹에서 버튼으로). GameHud 키폴링과 동일 관용.
- **위치**: `Gameplay/Editor/Dialogue/{DialogueGraphWindow,DialogueGraphView,DialogueNodeView}.cs` · `GUI/Dialogue/DialogueView.cs`(advance) · `Prefabs/GUI/Dialogue/Dialogue.prefab`(DialogueView 컴포넌트).
- **검증**: 컴파일0(UnityMCP) + **PlayMode `DialogueModelTests` 12/12**(A1 4 + Phase B 3 + 추가 5: TalkToNpc 보고·노드별 카메라 SetShot·종료 카메라Exit+입력해제·끊긴 GoTo 닫힘·시작노드 폴백 — FakeInputContext/FakeDialogueCamera/FakeQuestService 사용). **잔여(Unity)**: `Dialogue.prefab` Addressable 주소=AddressKeys.UI.Dialogue 마킹 + DialogueDefinition 에셋을 그래프툴로 저작 + DialogueCatalog 에 npcId 매핑 + 씬 NPC 배치 + 플레이. 분기 선택지 UI(choiceContainer+template)는 프리팹 확장 시.
- **카메라(A3) ✅ 2026-06-22**: Cinemachine 3.1.6 → **전용 dialogue vcam Priority 승격**(Brain 블렌딩, 게임 vcam 무수정). 구도는 노드별 `DialogueShot`(System.Dialogue: Closeup/OverShoulder/TwoShot) 그래프툴 EnumField 선택. `DialogueNode.shot` + `IDialogueCamera`(System: Enter/SetShot/Exit) → `DialogueCameraController`(Gameplay/Camera, Cinemachine: Closeup=LookAt/Follow NPC·OverShoulder=Follow Player·TwoShot=CinemachineTargetGroup, Priority 100↔-10). 배선: NPC.Interact→Enter(lookTarget,player) / DialogueModel.GoToNode→SetShot(node.shot)·Close→Exit. **IDialogueCamera optional**(미배치 시 대화 정상·카메라 무동작) — 단 DialogueModel/NPCBinder 가 ctor 주입이라 Main 스코프는 RegisterComponentInHierarchy 로 항상 등록(씬에 컨트롤러 존재 필요). **asmdef**: Game.Gameplay += Unity.Cinemachine(GUID 4307f53...), Game.Gameplay.Editor += Game.System. **씬(Main.unity) 생성**: `Dialogue Vcam`(CinemachineCamera+RotationComposer) + `Dialogue Camera Controller`(dialogueCam 배선). **잔여**: vcam 바디/오프셋 튜닝 + TwoShot 용 TargetGroup 할당(선택) + 플레이. 위치: `System/Dialogue/{DialogueShot,IDialogueLauncher(+IDialogueCamera)}.cs` · `Gameplay/Camera/DialogueCameraController.cs` · `Presentation/Dialogue/DialogueDefinition.cs`(shot)·`DialogueModel.cs`(SetShot/Exit) · `Gameplay/Character/{Interactions/NPCDialogueInteractable,NPCDialogueBinder}.cs` · `Gameplay/Editor/Dialogue/*`(shot 필드).

### 2.33 대화(Dialogue) 코어 — 4.5 Phase A1 (2026-06-22)
- **무엇**: NPC 상호작용→대화 트리 런타임 엔진(서버 0). 노드 그래프 순회(GoTo)·종료(EndDialogue)·동적 선택지. 4.5 4-페이즈 중 A1(A2 그래프툴·B 퀘스트연동·C 서버NPC/TalkToNpc 후속).
- **데이터 모델(graph-friendly, A2 툴 고려)**: `DialogueDefinition`(SO) = startNodeId + nodes[]. `DialogueNode`{id=안정 GUID(엣지 참조), editorPosition(그래프 레이아웃 보존·런타임 무시), speaker, bodyText, choices[]}. `DialogueChoice`{label, action(GoTo/EndDialogue/AcceptQuest/ClaimQuest), targetNodeId, questId, showIf(Always/QuestStatus*)}. A1 은 GoTo/EndDialogue + showIf=Always 만 평가(AcceptQuest/ClaimQuest·조건부=Phase B). `DialogueCatalog`(SO) npcId→DialogueDefinition.
- **레이어 다리(핵심 결정 — 처음 설계 틀려서 수정)**: NPC=Gameplay, 대화창=GUI. **GUI는 Game.System 직접참조 금지**(asmdef 규칙) → 처음에 `IDialogueLauncher`(System)를 GUI 컨트롤러가 구현하려다 컴파일 실패. 수정: **`IDialogueLauncher`(System) 구현을 `DialogueModel`(Presentation)에 둠**(Presentation→System ✓). NPC(Gameplay→System ✓)가 `IDialogueLauncher.Open(npcId)`(문자열만) 호출→DialogueModel.Start. GUI `DialogueViewController`는 System 안 보고 **State.IsOpen 만 구독**해 창을 Addressable 로드/표시/숨김(GUI→Presentation ✓). 즉 "다리는 양쪽이 다 보는 가장 낮은 레이어(여기선 Presentation이 System 인터페이스를 구현)에 둔다".
- **씬 NPC 주입 = 일괄 바인더**: 씬 배치 NPC N개를 per-object InjectGameObject 하지 않고 `NPCDialogueBinder`(IInitializable)가 `FindObjectsByType<NPCDialogueInteractable>` → `Bind(launcher)`. (런타임 스폰 NPC 는 스폰 시 별도 Bind — A1=씬배치 대상.) NPC 는 `IInteractable`(E 상호작용, `InteractionDetector` 재사용, `LocalGroundItem` 동형) + npcId 만.
- **DI(MainLifetimeScope, Main 전용)**: `DialogueCatalog`(Resources 폴백) + `DialogueModel`.As<IDialogueLauncher>().AsSelf()(**단일 인스턴스가 launcher=model 양쪽**) + `DialogueViewController`(엔트리) + `NPCDialogueBinder`(엔트리). DialogueModel 은 IInputContext 로 대화 중 이동 차단(Start=EnterUi/Close=ExitUi).
- **위치**: Presentation `Presentation/Dialogue/{DialogueDefinition(+Node/Choice/enum),DialogueCatalog,DialogueModel,DialogueState,DialogueIntent}.cs` · System `System/Dialogue/IDialogueLauncher.cs` · GUI `GUI/Dialogue/{DialogueView,DialogueViewController}.cs` · Gameplay `Gameplay/Character/Interactions/NPCDialogueInteractable.cs`·`Gameplay/Character/NPCDialogueBinder.cs` · `GUI/AddressKeys.cs`(UI.Dialogue) · `VContainer/.../MainLifetimeScope.cs`.
- **검증**: 클라 컴파일0(UnityMCP) + **PlayMode `DialogueModelTests` 4/4**(Start→시작노드·GoTo 이동·EndDialogue 종료·콘텐츠없음 미오픈). **잔여(Unity)**: `Dialogue.prefab`(DialogueView, Addressable 주소=AddressKeys.UI.Dialogue) + `DialogueCatalog.asset`(빈 생성됨, npcId→def 등록) + Main 씬 NPC 배치 + 플레이. DialogueDefinition 저작은 A2 그래프툴.

### 2.32 퀘스트(Quest) 풀스택 — 수주/진행/보상 (4.4, 2026-06-22)
- **무엇**: 수주(Accept)→진행(서버 권위 ReportKill)→완료→보상수령(Claim) 풀스택. MVP=KillMonster(slime). 도메인 패턴=Codex/Wallet 동형.
- **진행 = 서버 권위(클라 보고 아님)**: 유일한 진행 훅 = `MainSpawnClaimService.ClaimExpAsync`(Main 킬 클레임, exp 쿨다운 통과=진짜 킬 1회) → `IQuestService.ReportKillAsync(userId, slot.MonsterId)` → 매칭 Accepted·미완료 `KillMonster` 퀘스트 progress++(상한 clamp). 클라가 "킬했다" 못 보냄=위조 불가. **던전 맵클리어 킬은 MVP 범위 밖**(ClaimExpAsync는 Main 경로).
- **영속 = DB-only(Redis 캐시 X)**: `UserQuest`(`user_quests` (UserId,QuestId) 복합키, Status{Accepted/Claimed}+Progress). read-rare/write-heavy(킬마다 진행)라 캐시 부적합(plan §4.4). 읽기 AsNoTracking, 쓰기 upsert(키로 tracked 조회→SetValues / 없으면 Add). EF `AddUserQuests`(raw SQL 멱등).
- **완료=파생, 보상=조합·중복차단**: "완료"는 상태 아님 = Accepted && Progress≥RequiredCount 파생(QuestProgressStatus 4-상태: NotAccepted/Accepted/Completed/Claimed). 보상(exp/gold/item)=Progression+Wallet+Inventory 조합(Shop 동형). ClaimReward = **Claimed 선마킹·영속 후 지급**(지급 실패해도 재수령 불가, 중복 보상 차단).
- **GetQuests = 전체 카탈로그 × 상태 병합**(사용자 결정): 서버가 `QuestCatalog.All` × `UserQuest` 병합해 미수주 포함 전체 반환. 퀘스트 def(이름/설명/보상)를 proto가 실어 보냄 → **클라 퀘스트 카탈로그 미러 불필요**(퀘스트 수 적음, 아이템과 달리).
- **시드 3종**: `quest_slime_hunt`(slime×3 → exp50+gold100) · `quest_slime_slayer`(slime×5 → exp80+potion×2) · `quest_potion_collect`(potion×3 → exp30+gold50, **CollectItem 구조 시연 — 진행 훅 보류**). **CollectItem 훅 보류 이유**: 수집 진행은 `InventoryService.GrantItemAsync`에 합류해야 하는데, 거기에 IQuestService 의존 추가 시 그 서비스를 수동 조립하는 DI호스트 6+곳(LootGrant/DungeonResult 통합·E2E 등)이 재파손(codex 때 겪음) → MVP에선 KillMonster만 훅(MainSpawnClaimService 1곳만 생성자 변경=테스트 1곳 영향).
- **클라**: System `Game.System.Quest`(IQuestService/QuestService, proto enum→도메인 enum 은닉) → Presentation `QuestModel`(MVI: GetQuests→QuestEntryModel, 수주/수령 Side Effect 토스트) → **기존 View 스캐폴드 배선**(`Quest` 마스터-디테일: 목록 QuestSlot 고정풀 선택→정보/조건/보상 + 수락/완료 버튼. `QuestSlot`/`QuestConditionSlot`/`QuestRewardSlot`에 Bind 추가. 거절(abandon) 서버 미지원=btn_Decline 숨김) → `QuestViewController`(InGameModel.OnToggleQuest, **HUD `btn_Quest` 기존 버튼 활용**=도감과 달리 버튼 존재). **MVI 준수**: QuestEntryModel은 string/bool/int만 노출(System enum/타입 비노출 → GUI→System 위반 회피, RewardLines=문자열 리스트).
- **위치**: 서버 Domain `Entities/Quest/{QuestObjectiveType,QuestStatus,QuestDef(+QuestReward),QuestCatalog,UserQuest}.cs` · App `Domains/Quest/{IQuestService,QuestService,QuestStateView(+결과타입),Interfaces/IQuestRepository}.cs` · Infra `Domains/Quest/QuestRepository.cs`·`Persistence/Configurations/Quest/UserQuestConfiguration.cs`·`Migrations/*_AddUserQuests` · 훅 `Infrastructure/Domains/Inventory/MainSpawnClaimService.cs`(+IQuestService) · DI `InventoryInstaller` · proto `quest.proto` · `API/Services/QuestGrpcService.cs`·`MiddlewareInstaller`. 클라 `System/Quest/*`·`Presentation/Quest/*`·`GUI/Quest/*`(스캐폴드 배선)·`GUI/OutGame/QuestViewController.cs`·`Presentation/InGame/{InGameIntent,InGameModel}.cs`(ToggleQuest)·`GUI/Hud/GameHud.cs`(btn_Quest bind)·DI(GameApiClient·QuestInstaller·MainLifetimeScope) · Generated `Network/Https/{Generated/Quest*,Interfaces/IQuestGrpcService,Services/QuestGrpcService}.cs`.
- **검증**: 서버 빌드0 + **GameServer 373/373**(단위+Testcontainers통합+E2E 전부 — Quest 신규 19: Service 9·Grpc 4·Repo통합 4·MainSpawnClaim 킬훅 2; Docker gameserver 리빌드로 AddUserQuests 적용). 클라 컴파일0(UnityMCP) + **PlayMode QuestModelTests 그린 + 클라 E2E `QuestE2ETests` 4/4 그린(Docker)**: ① 풀루프(수주→`ClaimMonsterExp` 슬롯1/2/3 킬로 서버 진행→Completed→`ClaimQuestReward` 골드+100 지갑 반영→Claimed) ② 미완료 수령 거부 ③ 중복 수주 거부 ④ 미인증=RpcException(Unauthenticated, AuthInterceptor가 서비스 진입 전 거부 — Result 아님). E2E는 `E2ETestBase`에 `QuestService` 추가. **전체 PlayMode 117/117 그린 + 플레이 검증 통과(사용자 2026-06-22: 인게임 수주→처치→보상 수령 정상)** → 4.4 ✅. **후속(코어 무관)**: CollectItem 진행 훅(InventoryService 합류 시).

### 2.31 등급 슬롯 배경 + 공통 슬롯 통합 + 도감 보류 (3.7 방향전환, 2026-06-21)
- **무엇**: §2.30 직후 방향전환(사용자). ① 등급을 **Color 틴트 → 실제 슬롯 배경 스프라이트**로 ② 인벤/상점 슬롯을 **공통 컴포넌트 1개**로 통합 ③ **장비 슬롯도 등급 처리** ④ **도감 클라 전체 제거(보류)**, 서버는 휴면.
- **등급 배경 = `GradeSpriteCatalog`(SO, Presentation.Inventory)**: `ItemGrade → Sprite`. fantasy_gui_4 `fg4_slot{색}`(Common=GreyMedium·Rare=Blue·Epic=Violet·Legendary=Orange). 에셋 `Assets/GameData/Item/GradeSpriteCatalog.asset`(4 스프라이트 할당 완료, Addressable address=경로, §2.45). **GradeColors(틴트)는 잔존하나 슬롯엔 미사용**(텍스트색 등 향후용).
- **공통 슬롯 = `ItemContentsSlot`(Game.GUI.Common)로 통일**: `ShopItemSlotView` 삭제. `ItemContentsSlot.Bind(itemId, icon, count, onClick, gradeBackgroundSprite, displayName)` — count≤1 숨김·displayName null 숨김·gradeBackground null 끔(프리팹마다 쓰는 필드만 할당: 그리드=icon+count / 리스트=icon+name). **슬롯은 도메인/enum 모름 — Sprite·문자열만 받음**(기존 decoupling 계승, 단 Color→Sprite). 프리팹: `Shop_Item`=`ShopItemSlotView`(missing)→`ItemContentsSlot` 컴포넌트 교체+필드 wiring(itemIcon/itemName/itemButton/gradeBackground=슬롯 루트 Image), `ItemContentsSlot.prefab`=`gradeFrame`→`gradeBackground` 필드명 갱신(루트 배경 Image).
- **등급 해석 위치 = Model(View 아님)**: Icon 흐름과 동일하게 **Model 이 GradeSpriteCatalog.Get(grade)→Sprite 를 SlotModel 에 적재**(InventoryItemModel/ShopItemModel/EquipmentSlotModel 에 `GradeBackground`), View 는 그 Sprite 를 슬롯/배경에 세팅만. MVI 준수(View 는 자기 Model 만). 3개 Model 생성자에 `GradeSpriteCatalog gradeCatalog=null` 추가 → **Main·Dungeon 스코프 양쪽에 RegisterInstance**(VContainer 는 C# 기본값 무시 = 미등록 시 해소 실패 [[always-run-full-e2e-suite]] 교훈, 그래서 양 스코프 등록 필수).
- **장비 등급**: `Equipment` View 가 착용 시 슬롯 배경(`Slot` Image)을 `equipped.GradeBackground` 로, 미착용 시 Start 에서 캐시한 기본 스프라이트로 복원(`_defaultSlotSprites`). 별도 grade Image 안 만듦(기존 Slot Image 재사용).
- **상점 Selected Item 패널 등급(2026-06-21 추가)**: `Shop.RenderSelected` 가 선택 아이템 슬롯 프레임(`item_slot` 의 Image, `SelectedShopItemGradeBackground`)을 `sel.GradeBackground` 로. 리스트 슬롯과 동일 등급 스프라이트. 프리팹 wiring 완료(기존 item_slot Image 재사용, 신규 GameObject 없음).
- **아이템 설명(2026-06-21 추가)**: `ItemDisplayCatalog.Entry.description`(미러, [TextArea]) → `ShopItemModel.Description` → `Shop.RenderSelected` 선택 패널 desc(이전 빈 문자열). 11종 전부 저작. 스탯은 `Stats` 슬롯이 별도 표시라 설명은 플레이버/용도만(중복 회피). 인벤토리도 동일 카탈로그라 향후 재사용 가능.
- **도감 보류(클라 제거 / 서버 휴면)**: 클라 `System/Codex`·`Presentation/Codex`·`GUI/Codex`·`CodexViewController`·`CodexInstaller`·`GameApiClient`/`ProjectLifetimeScope`/`MainLifetimeScope` 도감 배선·`InGameModel/Intent.ToggleCodex`·`GameHud btn_Codex/SideButtonType.Codex`·`AddressKeys.Codex/CodexItem` **삭제**. **서버 도감(§2.30: 도메인·proto·grpc·`AddUserCodex` 마이그레이션·테스트)은 보존**(마이그레이션 롤백=파괴적이라 회피, 재개 시 클라만 재작성). 클라 생성 stub(`Generated/Codex*`·`ICodexGrpcService`·`Services/CodexGrpcService`)은 무해 잔존(proto에서 재생성됨).
- **검증**: 클라 컴파일 0(UnityMCP) + **PlayMode 109/109 그린**(통합/E2E 포함 — 슬롯/등급/모델 ctor 변경·도감 제거 무회귀. CodexModelTests 4 삭제로 113→109). 서버 무변경(이번 라운드)이라 GameServer 354/354 유지.
- **잔여(Unity 시각)**: `ItemDisplayCatalog.asset` 의 아이템별 `grade` 값 저작 + 슬롯 배경 Image 위치/스케일 육안 점검 + 신규 .cs `.meta` `git add -f`.

### 2.30 아이템 등급/레어도 + 도감 (3.7, 2026-06-21)
- **무엇**: ① 아이템 등급(레어도)을 클라에 노출·색 표시 ② 도감(컬렉션) — "한 번이라도 획득한 아이템"을 서버 권위로 기록·조회·UI.
- **등급 = 클라 미러(사용자 결정, proto 무변경)**: 서버 `ItemDef.Grade`(`ItemGrade` enum 기존, 게임플레이 무효과=표시 전용)는 그대로 두고, 클라 표시 데이터는 `ItemDisplayCatalog.Entry.grade`(미러, name/icon 과 동일 컨벤션)로 흘린다. `GradeColors`(Presentation, 단일 색 매핑: Common 회색·Rare 파랑·Epic 보라·Legendary 주황)를 인벤/상점/도감 슬롯이 공유. **왜 미러**: GetInventory/GetShop 3곳 proto 변경 회피 + 기존 정의 미러 패턴 일관. 드리프트 위험은 name 과 동일 수준(수용). 미래 드랍 가중치는 서버 `ItemDef.Grade`(권위)가 담당 — 클라 미러와 무충돌.
- **도감 발견 = 서버 권위 단일 funnel**: 발견 기록은 오직 `InventoryService.GrantItemAsync`(루트·상점·ClaimKill 모든 획득이 수렴)에서 `ICodexService.MarkDiscoveredAsync` 호출 → 클라가 "발견했다" 보고 불가(치팅 차단, Quest 진행과 동일 원칙). 멱등 — `INSERT … ON CONFLICT (UserId,ItemId) DO NOTHING`(동시 첫 획득 경합 안전).
- **영속 = DB-only(Redis 캐시 없음)**: `UserCodexEntry`(`user_codex` (UserId,ItemId) 복합키, append-only). write-once·read-rare(도감 열 때만)라 캐시 이득 낮음(계획된 Quest 4.4 와 동형). 읽기 `AsNoTracking`.
- **조회 proto = 발견 itemId 집합만**: `GetCodex → repeated discovered_item_ids`. 전체 목록·등급·아이콘·완성도 분모는 **클라 카탈로그(ItemDisplayCatalog.All)** 가 소유 → 서버는 발견 사실만. 클라가 전체×발견 병합.
- **클라 흐름**: System `Game.System.Codex`(ICodexService/CodexService, proto 은닉) → Presentation `CodexModel`(MVI: ItemDisplayCatalog.All 순회 × 발견 HashSet → `CodexEntryModel{discovered,grade,…}` + 완성도 = 발견/전체) → View `Codex`(그리드, Addressable `Codex_Item` 동적 슬롯, ShopModel/Shop 동형) + `CodexItemSlotView`(미발견=실루엣 틴트+"???", 등급색 프레임) → `CodexViewController`(InGameModel.OnToggleCodex 구독, HUD `btn_Codex` → `InGameIntent.ToggleCodex`). 등록: GameApiClient(ICodexGrpcService) + `CodexInstaller`(루트) + MainLifetimeScope(CodexModel+ViewController).
- **위치**: 서버 Domain `Entities/Codex/UserCodexEntry.cs` · App `Domains/Codex/{ICodexService,CodexService,Interfaces/ICodexRepository}.cs` · Infra `Domains/Codex/CodexRepository.cs`·`Persistence/Configurations/Codex/UserCodexEntryConfiguration.cs`·`Migrations/*_AddUserCodex` · DI `Installers/Domain/InventoryInstaller.cs`(합류) · proto `Shared.Contracts/Protos/codex.proto` · `API/Services/CodexGrpcService.cs`·`MiddlewareInstaller`. 클라 `System/Codex/*` · `Presentation/Codex/*` · `Presentation/Inventory/{ItemGrade,GradeColors}.cs`+`ItemDisplayCatalog`(grade·All)+`InventoryItemModel`(Grade) · `GUI/Codex/{Codex,CodexItemSlotView}.cs`·`GUI/OutGame/CodexViewController.cs`·`GUI/Common/Slots/Contents/ItemContentsSlot.cs`(gradeColor)·`GUI/Hud/GameHud.cs`(btn_Codex)·`GUI/AddressKeys.cs`·`Presentation/InGame/{InGameIntent,InGameModel}.cs`(ToggleCodex) · Generated `Network/Https/{Generated/Codex*,Interfaces/ICodexGrpcService,Services/CodexGrpcService}.cs`(ClientCodegen).
- **검증(Docker 리빌드 후 full suite, 2026-06-21)**: 서버 빌드 0 + **GameServer 354/354**(단위+Testcontainers 통합+E2E 전부 그린) — `CodexServiceTests` 5·`CodexGrpcServiceTests` 3·InventoryService 지급→발견 2·`CodexRepositoryIntegrationTests` 4 신규 + 클라 단위(`InventoryService` 생성자 `ICodexService` 추가) 5사이트 보정. **PlayMode 111/113**(클라). `CodexModelTests` 4/4 + loot/inventory/reward/socket E2E 그린(실서버 GrantItemAsync 의 codex 훅 무회귀 확인).
  - **⚠️ DI호스트 회귀 6개 발생→수정([[always-run-full-e2e-suite]] 재확인)**: `InventoryService` 생성자에 `ICodexService` 추가 → **수동으로 InventoryService 를 등록하는 4개 통합/E2E harness**(LootGrantConsumer통합·DungeonResultConsumer통합·LootGrantReward E2E·DungeonResultReward E2E)가 `ICodexService`/`ICodexRepository` 미등록으로 consumer DI 해소 실패 → 보상 파이프라인 6테스트 TaskCanceled(타임아웃). 각 harness 에 두 등록 추가로 그린. **교훈: 생성자에 의존성 1개 추가 = 그 타입을 수동 조립하는 모든 테스트 호스트가 깨진다 → full suite 필수.**
  - **무관 기존 실패 2개(코덱스 아님)**: `GameHudIntegrationTests`/`GameHudBuffIntegrationTests` 가 `InGameModel` 해소 시 `PlayerProgressionHolder`(§2.25b, 06-14 InGameModel 생성자에 추가된 optional 파라미터) 미등록으로 VContainer 실패. 해당 테스트(06-05)는 그 후 full PlayMode 미실행이라 06-14부터 잠복. 별도 수정 대상(InGameModel 생성자·codex 무관, 내 diff=+10줄 Subject 추가뿐).
- **잔여(Unity 저작)**: `Codex.prefab`/`Codex_Item.prefab`(Addressable 주소=AddressKeys.UI.Codex/CodexItem) + GameHud `btn_Codex` + `ItemDisplayCatalog.asset` grade 값·전체 아이템 엔트리(완성도 분모 정확성) + 슬롯 `gradeFrame` Image 할당. 신규 .cs `.meta` `git add -f`.

### 2.24 몬스터 카탈로그(SO) + Main 킬 Exp 보상 (2026-06-14)
- **무엇**: ① 몬스터 정의를 **SO 저작 카탈로그**로 승격(기존 C# 하드코딩 `Server.Monster.MonsterCatalog` 대체) ② Main 몬스터 처치 시 **ClaimKill 경로로 Exp 보상**.
- **왜 카탈로그 분리**: exp/스탯은 "몬스터가 무엇인가"라 **스폰 데이터(MonsterSpawnDef=위치/슬롯)에 넣으면 비대+중복**(같은 slime 3슬롯이면 exp 3× 저작). 몬스터 타입당 1정의 = DRY. 구조 = **단일 SO + List**(DropTable/LevelTable 과 동일, 사용자 결정 B안).
- **데이터 흐름**: 클라 `MonsterCatalogDefinition` SO(List<MonsterDefinition>: maxHp·이동·공격·**expReward**) → `MonsterCatalogExporter` bake → `Shared.Infrastructure/Monsters/monsters.json`(임베디드) → `Shared.Infrastructure.Monsters.MonsterCatalog.Get(id)→MonsterDef`. **양 서버 공유**: SocketServer `Server.Monster.MonsterCatalog`=시뮬 스탯만 매핑하는 **얇은 어댑터**(exp 제외, 하드코딩 제거); GameServer `MainSpawnClaimService`=expReward 조회.
- **Main 킬 Exp = 킬 즉시 + 별도 청구(`ClaimMonsterExp`)**, 아이템은 줍기(`ClaimKill`) 유지(사용자 결정 2026-06-14): exp와 아이템은 **독립 청구·독립 쿨다운**(`mainexp:*` vs `mainclaim:*`). 둘 다 슬롯검증+per-user 쿨다운으로 파밍 상한(authority-model §4b). **왜 분리**: ClaimKill 1건이 exp+아이템을 같은 쿨다운으로 묶으면 "exp 즉시 + 아이템 줍기"를 동시에 못 함(쿨다운 1회) → exp 전용 RPC 신설. 클라가 exp 못 정함(서버 `MonsterCatalog.ExpReward`). 던전 Exp(맵클리어)와 **동일 `AddExpAsync` 단일 적립 권위**로 수렴.
  - 클라 흐름: `MainMonsterSpawner.HandleDied`(킬) → `ClaimMonsterExp`(exp 즉시 적립 + `[MainMonsterSpawner] 경험치 +N` 로그) / `LocalGroundItem.Interact`(줍기 E) → `ClaimKill`(아이템). 서버 `MainSpawnClaimService.ClaimExpAsync`/`ClaimKillAsync`(슬롯검증·쿨다운 공통 헬퍼 `ValidateSlot`/`TryClaimCooldownAsync`).
- **proto**: `ClaimKill`=아이템 전용(exp_gained 제거) + 신규 `ClaimMonsterExp(map_id,slot_id)→{result, exp_gained}`. 클라 Generated 재생성. `MainExpClaimResult`(Application).
- **위치**: Shared `Monsters/{MonsterCatalog.cs,monsters.json}` · SocketServer `Monster/MonsterCatalog.cs`(어댑터) · 클라 `Gameplay/Monster/MonsterCatalogDefinition.cs`·`Gameplay/Editor/MonsterCatalogExporter.cs` · GameServer `Infrastructure/Domains/Inventory/MainSpawnClaimService.cs`(IProgressionService 주입)·`Application/.../MainClaimResult.cs`·`API/Services/InventoryGrpcService.cs` · proto `inventory.proto`.
- **검증(완료)**: Shared.Gameplay 34 · SocketServer 101(MonsterCatalog 5·마이그레이션 회귀) · **GameServer 전체 278(Testcontainers 통합+E2E 포함)** — MainSpawnClaim 10(ClaimExp 적립·쿨다운=0exp·exp↔item 쿨다운 독립·위조거부)·InventoryGrpc 10(ClaimMonsterExp 매핑·미인증) · **PlayMode `MainLootE2ETests` 통과 + 플레이 확인(사용자 2026-06-14: 킬 즉시 `[MainMonsterSpawner] 경험치 +20 획득` 로그)**.
- **클라 로그**: 킬 즉시 `MainMonsterSpawner.HandleDied` → `[MainMonsterSpawner] 경험치 +N 획득` 로그(`ClaimMonsterExp` 성공·exp>0). 줍기(`LocalGroundItem`)는 아이템 로그만(exp 분리). 정식 토스트는 7.x.

### 2.25 Main 클라 스탯 반영 + 레벨/exp 로그 (2026-06-14)
- **무엇**: Main(클라 권위 로컬 전투)이 현재 레벨 스탯을 반영. `LocalCombat` 데미지 고정 10 → AttackPower 기반(레벨업하면 다음 스윙부터 강해짐). 2.4(던전=서버권위 스탯전투)의 **Main 대응판**.
- **클라 스탯 홀더** = `PlayerProgressionHolder`(`System/Progression/PlayerProgressionHolder.cs`, **Game.System**). `IProgressionService.GetProgression`(서버권위) 결과를 `Current`(ProgressionData: Level/Exp/ExpToNext/Stats)로 캐시. `IAsyncStartable.StartAsync`로 **Main 진입(로그인 직후) 1회** + 킬마다 `RefreshAsync`. 편의 접근자 `AttackPower`/`Defense`/`Level`. 미갱신 시 default(0).
  - **왜 System(ProgressionModel 과 별개)**: `ProgressionModel`(Presentation, 스탯창 MVI reactive)은 GUI 전용. Gameplay(LocalCombat/MainMonsterSpawner)는 **Presentation 미참조**(레이어 방향) → System 에 별도 홀더. 진실원=서버, 홀더는 마지막 pull 캐시만.
- **데미지** = `LocalCombat.PerformHit`에서 `StatCombatMath.MeleeDamage(BaseDamage 10, _progression?.AttackPower ?? 0, 0)`(Shared 결정론, 던전과 동일 산식). 홀더 `[Inject] Construct` 메서드 주입 — `CharacterSpawner.AttachLocalCombat`서 `AddComponent` 후 `_container.Inject(combat)`(동적 부착이라 수동 주입).
- **킬 후 로그** = `MainMonsterSpawner.ClaimExpAsync` 성공 → `holder.RefreshAsync` → `LogProgression()`: `[Progression] 현재 Lv N · Exp X/Y (다음까지 Z)`(만렙=`(만렙)`).
- **DLL 재배치 필수였음**: `StatCombatMath`(2.4 추가)가 클라 `Plugins/Shared.Gameplay.dll`(stale)에 미포함 → 컴파일 실패. `Shared.Gameplay` Release 재빌드 → `Client/Assets/Plugins/Shared.Gameplay/`에 재복사(공개 API 변경이라 필수, §공유코어 단일소스 패턴).
- **위치**: `System/Progression/PlayerProgressionHolder.cs`(신규) · `Gameplay/Character/{LocalCombat,MainMonsterSpawner,CharacterSpawner}.cs` · `VContainer/.../MainLifetimeScope.cs`(`RegisterEntryPoint<PlayerProgressionHolder>().AsSelf()`).
- **검증**: 변경 어셈블리 dotnet build 0오류(Game.System·Game.Gameplay·Game.VContainer). ⚠️ Unity 재임포트(새 dll meta)+플레이 확인은 사용자.

#### 2.25b HUD exp 게이지 중계 (2026-06-14)
- **무엇**: GameHud 에 사용자가 추가한 `expSlider`(`Slider`)+`expValue`(TMP) 를 레벨/Exp 에 연결. exp 데이터는 `PlayerProgressionHolder`(2.25) → `InGameModel` → `InGameState` → `GameHud` 로 흐른다.
- **왜 InGameModel 경유(ProgressionModel 직접주입 X)**: MVI 규칙 "View 는 자신의 Model 하나만 주입"(unity-client.md). GameHud 의 Model 은 `InGameModel` → exp 도 InGameState 로 합류시킨다(HP/MP/버프와 동일 경로). `ProgressionModel`(스탯창 7.3 전용)은 별개 유지.
- **흐름**: `holder.OnChanged`(RefreshAsync 성공 시 발행, 로그인·킬) → `InGameModel.PushProgression`(`IInitializable.Initialize`서 구독+즉시 1회, holder `IAsyncStartable.StartAsync` 보다 먼저 실행돼 첫 pull 미유실) → `Dispatch(InGameResult.ExpChanged)` → `InGameReducer` → `InGameState.WithExp(Level/Exp/ExpToNext)` → `GameHud.RenderExp`(slider.value=Exp/ExpToNext, text=`Exp/ExpToNext` 또는 만렙 `MAX`).
- **던전도 표시**: `PlayerProgressionHolder` 를 **DungeonLifetimeScope 에도 등록**(표시 전용 — 던전 전투는 서버 권위라 데미지엔 미사용). 안 그러면 던전 HUD exp 게이지가 빈 채로 보임. `InGameModel` 의 holder 주입은 optional(null-safe) — 미등록 스코프면 exp 게이지 미갱신.
- **위치**: `GUI/Hud/GameHud.cs`(`RenderExp`) · `Presentation/InGame/{InGameModel,InGameState,InGameResult,InGameReducer}.cs`(ExpChanged/WithExp) · `System/Progression/PlayerProgressionHolder.cs`(`OnChanged`) · `VContainer/.../DungeonLifetimeScope.cs`(holder 등록).
- **런타임 함정 2건(수정 완료 2026-06-14)**:
  - ① `Slider.value=` 가 프리팹에 잘못 연결된 `onValueChanged`(UI.Text 변환 실패) persistent 리스너를 발동 → ArgumentException. exp 게이지는 표시 전용이라 `expSlider.SetValueWithoutNotify(fill)` 로 콜백 차단(GameHud.RenderExp). 프리팹 리스너 정리는 사용자 권장.
  - ② 홀더 `StartAsync` 가 인증 전 eager pull → `GetProgression Unauthenticated` Error 로그 노이즈. `AuthSession.AuthenticatedAsync().AttachExternalCancellation` 로 **로그인 완료 후 pull**(`PlayerProgressionHolder` 에 `AuthSession` optional 주입, 같은 Game.System 어셈블리).
- **검증**: EditMode `InGameExpRelayTests` 2/2 그린(홀더 Refresh→State 반영 / 홀더 없으면 초기값). 변경 어셈블리 dotnet build 0오류(Game.System·Game.Presentation·Game.GUI·Game.VContainer). ⚠️ 게이지 실렌더 육안 확인은 사용자 플레이.

### 2.26 몬스터→플레이어 Defense 반영 (2.4 증분3, 2026-06-14)
- **무엇**: 몬스터가 플레이어를 때릴 때 **플레이어 Defense를 데미지에서 차감**(이전: 고정 `monster_attack_dmg` 카탈로그값, Defense 무시). 플레이어→몬스터(증분2)의 역방향 대칭.
- **산식**: `Room.TickMonsters`에서 `int dmg = StatCombatMath.MeleeDamage(MonsterDef.AttackDamage, 0, PlayerState.Defense)` = `max(1, AttackDamage − Defense)`(무피해 방지 최소1). 몬스터별 `AttackDamage`(카탈로그 기존 필드) 사용 — 고정 catalog 아님.
- **전달(패킷 계약 변경)**: `S_ApplyEffect`에 `int Amount` 필드 추가(서버 권위 Health 델타, 음수=데미지). 데미지가 player Defense 의존이라 클라가 자체계산 불가 → 서버가 계산해 전달. **Amount=0 = 카탈로그 고정값 사용(버프/디버프 하위호환)**. 클라 미러는 **ClientCodegen 재생성**(`Network/Socket/Packets/EffectPacket.cs`, Union 1640 불변).
- **서버 흐름**: `Room.TickMonsters` → `ApplyPlayerEffect(target, Health −dmg)`(서버 HP 권위, Defense 반영) + `S_ApplyEffect{ Amount = −dmg }` 브로드캐스트. `CombatEffectCatalog.Resolve("monster_attack_dmg")` 의존 제거(Room.cs).
- **클라 흐름**: `EffectApplyPacketHandler`(packet.Amount → `SocketEffectApply.Amount`) → `EffectReceiver` → `ASC.ApplyEffectAuthoritative(def, id, stacks, healthOverride: Amount)`. healthOverride≠0이면 Instant 효과의 Health 모디파이어 양을 그 값으로 덮어씀(카탈로그 무시).
- **클라 예측 미도입**: 몬스터→플레이어는 로컬 예측 대상 아님(서버 수치 그대로 렌더). 공유 시계·예측-정정은 EF-2d 후속(YAGNI).
- **위치**: 서버 `Shared.Packet/Packets/Domains/EffectPacket.cs`(+Amount) · `SocketServer/Room/Room.cs`(TickMonsters). 클라 `Network/Socket/Packets/EffectPacket.cs`(코드젠) · `Handler/Contents/EffectPacketHandler.cs` · `SocketApiClient.cs`(SocketEffectApply) · `Presentation/InGame/EffectReceiver.cs` · `System/GameplayAbilitySystem/AbilitySystemComponent.cs`(healthOverride).
- **검증**: SocketServer 103/103(신규 `MonsterAttackTests` Defense 2: AttackDamage−Defense·최소1) + 클라 EditMode `EffectSystemTests` 9/9(신규 HealthOverride 2) + 서버/클라 빌드·Unity 컴파일 0오류. **플레이 검증 통과(2026-06-15)**: 던전에서 Lv1(Def5) 플레이어가 slime(AD5)에게 맞을 때 `amount=-1`·HP 1씩 감소(이전 옛 서버는 5씩) — 서버 리빌드 후 확인. (옛 Docker 이미지면 `amount=0` 폴백=거짓검증 함정.)

#### 2.26b Main 대응판 — LocalMonster Defense 반영 (클라 전용, 2026-06-15)
- **무엇**: Main(클라 권위 로컬)의 `LocalMonster`도 플레이어 Defense를 차감(이전: 고정 `-attackDamage` Health effect). 던전(서버권위, §2.26)의 Main 대응. **서버/패킷 0 — 클라 1파일**(Main은 싱글, LocalMonster가 로컬 ASC에 직접 즉발피해).
- **산식/주입**: `LocalMonster.TryAttack` → `int dmg = StatCombatMath.MeleeDamage(attackDamage, 0, _progression?.Defense ?? 0)`(던전과 동일 Shared 함수, 최소1) → `BuildAttackEffect(dmg)` 즉발 적용. `[Inject] PlayerProgressionHolder _progression`(`MainMonsterSpawner.Spawn`의 `_container.InjectGameObject`로 충족, `_localPlayer`와 동일 경로). 미주입 시 Defense=0 폴백. 고정 prebuilt `_attackEffect` 제거→공격마다 빌드(쿨다운 1.5s, 빈도 낮음).
- **테스트**: 데미지 산식 = `StatCombatMath`(기존 단위테스트 `StatCombatMathTests` 4종, base/AP/Defense/최소1). LocalMonster는 그 함수에 holder.Defense를 배선만 — 신규 산식 없음. (MonoBehaviour Update/주입 통합은 PlayMode 영역, 플레이 검증으로 대체.)
- **위치**: `Gameplay/Character/LocalMonster.cs`(`_progression` 주입 + `TryAttack`/`BuildAttackEffect`). ⚠️ 플레이 육안(Main slime 피격 `[LocalMonster] 공격 dmg=N` 로그·Defense만큼 덜 깎임)은 사용자.
- slime expReward=20(SO 조정 가능). 신규 클라 .cs `.meta` `git add -f`([[unity-meta-gitignored]]).

#### 2.26c 미입장 플레이어 AI 타깃 제외 — 입장 전 사망 레이스 수정 (2026-06-17)
- **결함**: 몬스터는 `_playerStates`(GameStart 시 `InitPlayerState`로 초기화) 기준으로 공격하며 **소켓 join 여부를 안 봄**. 강한 몬스터가 (스폰 위치=플레이어 스폰과 겹치면) 플레이어가 입장하기 전 첫 틱에 죽여 `S_PlayerDead`가 빈 방에 발행→**유실**. 사망 E2E(`SocketE2ETests.RawSocket_몬스터에게_죽으면…`)가 test_brute 9999 즉사로 이 잠복 결함을 표면화(약한 데미지일 땐 입장 후 죽어 우연히 가려졌음).
- **수정**: `PlayerState.HasJoined`(기본 false) 추가 → `C_PlayerJoin` 성공 시 `Room.MarkJoined(userId)`(구 `MarkReconnected` 개명, `_playerStates` 락 내 `HasJoined=true` + `DisconnectedAtMs=null`)가 true. `Room.TickMonsters` 타깃 필터 = `HasJoined && DisconnectedAtMs is null && !downed`. 입장/재접속 활성화 단일 진입점 = `RoomJoinLeaveHandler.HandlePlayerJoin`.
- **위치**: `SocketServer/Player/PlayerState.cs`(HasJoined) · `Room/Room.cs`(MarkJoined·TickMonsters 필터) · `PacketHandler/Handler/RoomJoinLeaveHandler.cs`(MarkJoined 호출).
- **테스트 영향**: `InitPlayerState` 후 곧장 `TickMonsters`로 공격을 기대하던 단위(MonsterAttackTests 5·PlayerHpServerAuthorityTests 몬스터데미지 1)는 `room.MarkJoined(userId)` 보정. `ReconnectGraceTests`는 호출 개명만.
- **검증**: SocketServer 단위 **103/103** + 서버 빌드 0오류 + socketserver 리빌드·재배포 후 PlayMode **SocketE2ETests 21/21**(사망·재접속 포함). test_brute 9999 유지(입장 후 결정론 즉사 = 픽스처 의도).

### 2.23 스탯 산식 — 크로스서버 스탯 전파 + 스탯 기반 데미지 (2.4 증분1·2, 2026-06-14)
- **무엇**: 던전 전투 데미지를 고정값(`CombatEffectCatalog`) → **플레이어 AttackPower 스탯 기반**으로 승격(서버 권위 재계산). 스탯은 GameServer(progression), 전투는 SocketServer 라 **크로스서버 전파** 동반.
- **핵심 경계 결정(= authority-model §4c)**: SocketServer 는 **DB 직접 접근 안 함**(EF/Npgsql 참조 0). GameServer 가 progression+레벨테이블로 **합산 결과**를 계산해 게임시작 메시지로 스냅샷 전달 → SocketServer 는 그 결과만 적용. 근거 2개: ① 데이터 소유/스키마 결합 회피(SocketServer=인터넷 엣지, DB 자격증명 부여 회피) ② **최종 스탯=다단계 합산(레벨+장비+버프)이라 권위가 하나여야 함** → "입력 말고 답을 넘긴다". 함정: 지금은 레벨뿐이라 SocketServer 가 `LevelTable.StatsAt` 직접 호출 가능해 보이나 3.2 장비 들어오면 깨짐 → 처음부터 GameServer 합산.
- **단일 합산 권위 = `ProgressionService.GetStatsAsync`**(Application) → `PlayerStats`(Level+스탯). 오늘=LevelTable 룩업, 미래=+장비(3.2)+버프 합류점. gRPC `GetProgression`·게임시작 메시지 둘 다 여기로 수렴(LevelTable 단일 소스라 분기해도 불일치 불가).
- **전파 경로**: `IProgressionService.GetStatsAsync` → `DungeonLobbyService`(StartGame 시 참가자별 호출) → `GameStartRequestedMessage.PlayerInfo{+MaxHealth +AttackPower +Defense}`(additive, 기본0) → Redis stream → SocketServer `RoomManager.CreateRoom` → `Room.InitPlayerState(…, attackPower, defense, maxHealth)` → `PlayerState{+AttackPower +Defense}`. MaxHp = 메시지값(0이면 DefaultMaxHp 폴백). InitPlayerState 새 인자는 **optional(기본0)** 이라 기존 테스트/레거시 경로 무변경.
- **데미지 산식 = `StatCombatMath.MeleeDamage(baseDamage, attackPower, defense)` = max(1, base+AP−def)**(Shared.Gameplay 결정론, 클라 미러 가능). `CombatHandler.ScaleDamageByStats`(순수, 테스트가능)가 카탈로그 Health 감소량을 base 로 보고 재계산 — Health 데미지 모디파이어만 스케일, 회복/버프는 그대로. **AP=0 → base 동일 = 완전 하위호환**(기존 전투 테스트 무변경).
- **위치**: Application `Domains/Progression/{PlayerStats.cs, ProgressionService.GetStatsAsync, IProgressionService}` · `Domains/DungeonLobby/DungeonLobbyService.cs`(progressionService 주입) · Shared `Messages/GameStartRequestedMessage.PlayerInfo` · Shared.Gameplay `Combat/StatCombatMath.cs` · SocketServer `Player/PlayerState.cs`·`Room/Room.InitPlayerState`·`Room/RoomManager.cs`·`PacketHandler/Handler/CombatHandler.ScaleDamageByStats`.
- **테스트(단위+통합+E2E)**: GetStatsAsync 2 + `PlayerStatSeedTests` 4(InitPlayerState 세팅·미설정폴백·PlayerInfo 기본값·**CreateRoom→PlayerState 전파**) + `StatCombatMathTests` 4 + `CombatHandlerStatDamageTests` 3 + **`DungeonLobbyServiceTests` StartGame 메시지에 Lv2 스탯 적재**(교차도메인) + **보상→레벨업 E2E/통합**(`DungeonResultRewardE2ETests`·`DungeonResultConsumerIntegrationTests` — 던전 보상 100=Lv1임계→Lv2/Exp0 DB영속, 멱등). 전체 그린: **GameServer 272(E2E·통합 포함)·SocketServer 96·Shared.Gameplay 34**. 전체 솔루션 빌드 0오류.
- **⚠️ 회귀 교훈(2026-06-14)**: 단위만 돌려 ① AddExp 레벨업이 보상경로 테스트 3개(`Exp==100` 옛 단언) ② `DungeonLobbyService` 새 의존성 `IProgressionService` 미등록으로 E2E DI호스트 3개(`GameStartE2ETest`·`RoomLifecycleConsumerIntegrationTests`)를 조용히 깨뜨림 → 전부 수정. [[always-run-full-e2e-suite]]: 공유계약/생성자/보상로직 변경 후 **전체 E2E+통합(Docker) 필수**.
- **잔여**: ① **몬스터→플레이어 Defense 반영** — 보류. `S_ApplyEffect` 는 effectId 만 싣고 클라/서버가 같은 카탈로그값 적용(§4 HP 서버권위+클라예측). 서버만 defense 감산하면 클라 예측과 갈라짐 → **effect amount 전달 또는 클라 예측-정정** 필요(EF-2d "정밀화"와 합류). ② 장비/버프 modifier 합류 = 3.2. ③ 클라 데미지 표시 미러(선택).

### 2.22 레벨업/스탯 성장 — 레벨 테이블 룩업 + 서버 권위 레벨업 (2.3, 2026-06-14)
- **무엇**: Exp 적립 시 서버가 레벨업(remainder 이월·60만렙)하고 영속. 스탯은 레벨별 테이블에서 **룩업 파생**(저장 안 함). 클라는 gRPC pull로 레벨/Exp/스탯 조회(스탯창 7.3).
- **핵심 결정 — 스탯=레벨 룩업 파생, DB는 Level/Exp만**: 스탯 컬럼 추가/마이그레이션 **안 함**. `user_progressions`는 기존 Level/Exp 그대로. 스탯은 항상 `LevelTable.StatsAt(level)`로 파생 = 단일소스. 2.4 스탯합산이 이 base 위에 장비/버프를 얹는 구조. (사용자 결정: "스탯은 레벨별 테이블에 정의")
- **데이터 교리(DropTable·Consumable과 동일)**: SO 저작 → Editor bake → Shared 임베디드 JSON. 진실원 = 클라 `LevelTableDefinition` SO(1~60행: expToNext + 스탯). `LevelTableExporter`(`Tools/Progression/Generate Default Curve`=거듭제곱 `round(100·L^1.5)`+선형스탯 시드 / `Export`=bake / `Import`=부트스트랩) → `level-table.json`(임베디드) → 서버 `LevelTable` 리더.
- **레벨업 불변식 = 엔티티 소유**: `UserProgression.AddExp(amount, ILevelCurve)` — Exp 누적 후 `while(Level<MaxLevel && Exp>=ExpToNext(Level)){ Exp-=…; Level++ }`. 만렙은 `LevelTable.ExpToNext`가 `long.MaxValue` 반환→루프 통과 차단. `ILevelCurve`(**Domain** 추상, DIP) ↔ `LevelTableCurve`(Infra, LevelTable 위임, 무상태 싱글턴). 인터페이스 정당화 = 구현 2개(실데이터+테스트 스텁).
- **클라 노출 = pull(GetProgression)**: 레벨업은 GameServer(`DungeonResultConsumer`)에서 일어나 SocketServer 푸시는 크로스서버라 과함 → 스탯창 열 때 최신값 pull이 단순·정확. 서버가 `LevelTable` 룩업으로 스탯 합성(만렙 expToNext=0). userId=JWT(`context.GetUserId()`).
- **위치(서버)**: `Shared.Contracts/Protos/progression.proto`(GetProgression) · `GameServer.API/Services/ProgressionGrpcService.cs`(+`MiddlewareInstaller` 등록) · `Shared.Infrastructure/Progression/{LevelTable.cs,level-table.json}` · Domain `Entities/User/{UserProgression.AddExp,ILevelCurve}.cs` · Infra `Domains/Progression/LevelTableCurve.cs` · Application `Progression/{IProgressionService,ProgressionService}.GetProgressionAsync`.
- **위치(클라)**: 저작 `Gameplay/Progression/LevelTableDefinition.cs`+`Gameplay/Editor/LevelTableExporter.cs` · 생성 `Network/Https/{Generated/Progression*,Interfaces/IProgressionGrpcService,Services/ProgressionGrpcService}.cs`(ClientCodegen) · System `System/Progression/{IProgressionService,ProgressionService,ProgressionData}.cs`(proto 은닉) · Presentation `Presentation/Progression/{ProgressionModel,ProgressionViewState}.cs`(MVI pull) · DI `GameApiClient`(Network 래퍼)+`ProgressionInstaller`(ProjectLifetimeScope)+`Main/DungeonLifetimeScope`(Model).
- **테스트**: 서버 진행 28 그린 — `LevelTableTests` 7(룩업/단조/만렙/clamp/파싱) · `UserProgressionTests` 8(레벨업/다중점프/remainder/만렙고정) · `ProgressionServiceTests` 3 · `ProgressionGrpcServiceTests` 3(인증+스탯합성) · `ProgressionRepositoryIntegrationTests` 7(레벨업 DB영속 신규). 전체 솔루션 빌드 0오류.
- **검증 완료**: 서버 28 + **Unity 클라 컴파일 0오류** + **PlayMode E2E `ProgressionE2ETests` 2**(Docker 대상 — 신규유저 GetProgression→Lv1 기본스탯·미인증 거부, `E2ETestBase`에 `ProgressionService` 배선). 전체 PlayMode E2E 68/68. **잔여(사용자 Unity)**: ① 스탯창 prefab 비주얼(7.3) ② `LevelTable.asset` 실저작(`Tools/Progression/Generate Default Curve`→`Export`). **신규 클라 .cs는 `.meta` `git add -f` 필요([[unity-meta-gitignored]]).**

### 2.21 소모품/포션 사용 — MVI Side Effect (3.8 α 로직, 2026-06-10)
- **무엇**: 인벤토리 소모품을 "사용"하면 서버가 수량을 차감(권위)하고 클라가 회복을 GAS로 적용. **MVI Side Effect Cycle**로 구현 — State(수량) vs Side Effect(회복·토스트) 분리.
- **권위 비대칭(핵심)**: **인벤토리 수량 = 서버 권위**(`ConsumeItem` RPC = 보유/수량 검증·차감) / **플레이어 HP 회복 = 클라 권위**(GAS, 기존 비대칭 유지). 순서 = consume 성공 후에만 회복(없는 포션 사용 차단). → 회복 수치는 서버가 안 봐도 됨 → **효과 데이터는 클라 전용**(SO 서버 공유 불필요·불가).
- **Side Effect 패턴(`LobbyModel.NavigateToRoom`과 동형)**: `InventoryModel`이 `Subject<string>`→`Observable` 2채널 — `OnConsumableUsed`(회복 트리거)·`OnToast`(메시지). `UseItem` 의도 → `UseItemAsync`: consume(gRPC) → 실패 시 `OnToast`(실패)만 / 성공 시 `OnConsumableUsed`+`OnToast`+Refresh. **Model은 신호만, 적용은 구독자**.
- **구독자**: `ConsumableEffectHandler`(Presentation, IInitializable) — `OnConsumableUsed` 구독 → `ConsumableCatalog.Get(itemId)`(stat/amount/policy) → `GameplayEffectDefinition` 빌드 → `LocalPlayerContext.ASC.ApplyEffect`(GAS funnel → `OnAttributeChanged` → HUD 자동). 토스트는 View가 `OnToast` 구독(현재 로그, 정식 위젯 7.x).
- **왜 System 최소**: `ConsumableUseService`(System 오케스트레이션) **만들지 않음** — System `IInventoryService`엔 proto 은닉용 `ConsumeItemAsync` 한 메서드만(GetInventory와 동일 성격). 오케스트레이션은 Model(MVI), 효과는 구독자. (사용자 선호 "System 최소" 반영)
- **왜 이벤트 버스 아님**: VitalRouter/MessagePipe 미도입 — consume→heal은 결과 의존·순서·단일 소비자라 직접 await + Side Effect 채널이 적합(버스는 다대다 팬아웃 때 도입).
- **데이터**: `ConsumableCatalog`(SO, `Game.Presentation.Inventory`) itemId→`ConsumableEffectDef[]{stat,amount,policy,durationMs}`. heal/mana/buff 전부 스탯 modifier라 코드 없이 데이터만 추가.
- **위치**: proto `inventory.proto`(ConsumeItem) · `GameServer.API/Services/InventoryGrpcService.ConsumeItem` · 클라 `System/Inventory/{IInventoryService,InventoryService}.ConsumeItemAsync` · `Presentation/Inventory/{ConsumableCatalog,ConsumableEffectHandler}.cs`·`InventoryModel`(UseItem+채널)·`InventoryIntent.UseItem` · DI `Main/DungeonLifetimeScope`(ConsumableCatalog+Handler 등록).
- **테스트**: `InventoryGrpcServiceTests` **9/9**(ConsumeItem 보유/미보유/부족/미인증 4 신규) + `InventoryModelTests` **5/5**(UseItem 성공→OnConsumableUsed+OnToast / 실패→토스트만 2 신규). 서버빌드·Unity 컴파일 0오류.
- **β UI 코드 완료 2026-06-10**: `ItemContentsSlot`(IPointerClickHandler, itemId+클릭 콜백) → `Inventory.OpenActionPanel(itemId, slotRect)`: **`ItemActionPanel`(`Script.GUI.Inventory`)을 Canvas 직속 생성**(슬롯 자식 아님 — `GetWorldCorners`로 슬롯 오른쪽 변에 pivot(0,0.5) 배치) + 뒤에 **`BackDropButton`(`Game.GUI.Common`, 런타임 생성 풀스크린 투명 raycast)** 깔고 패널 `SetAsLastSibling`. `ItemActionPanel.useButton`→`Bind`의 onUse(`Accept(UseItem)`)+`OnCloseRequested`→View `CloseActionPanel`(패널+백드롭 파괴). 백드롭 클릭→`CloseActionPanel`. `OnToast`→로그(정식 위젯 7.x). 패널 prefab은 **Addressable**(`AddressKeys.UI.ItemActionPanel`, 슬롯 prefab과 동일 컨벤션 — `Inventory.LoadSlotPrefabsAsync`에서 로드) → `Default Local Group`에 주소=에셋경로로 등록(execute_code). Unity 컴파일 0오류. **클릭은 `ItemContentsSlot.itemButton`(Button.onClick, IPointerClickHandler 제거 — 리스너 1회 등록 가드)**, 패널 prefab은 **온디맨드 Addressable**(`InstantiateAsync`↔`ReleaseInstance`, eager 필드 없음). **플레이 검증 완전 통과(사용자, 2026-06-10)**: 슬롯 버튼 클릭→패널→사용→`ConsumeItem`(서버 차감)→Side Effect→**GAS `ASC.ApplyEffect` HP 회복**+토스트 전 경로 동작(`ConsumableCatalog.asset` 저작 후 `[ConsumableEffectHandler] 'potion_hp_small' 효과 적용` 로그 확인). → **3.8 코어 완결 ✅.** **후속(폴리시)**: ① **소모품만 사용 가능 제한**(현재 gold_pouch도 사용 버튼 노출·차감 — category==Consumable 필터) ② 정식 토스트 위젯(7.x).

### 2.20 Main 로컬 전투 — 클라 권위 몬스터/히트 (3.3 증분 9b·9c, 2026-06-09)
- **무엇**: Main(싱글)에서 클라가 권위로 몬스터를 스폰·전투·사망 판정. 던전(서버권위·`MonsterEntity` 보간)과 **다른 컴포넌트군**(서버 명령 수신이 아니라 클라가 판정). loot-drop.md §1.5.
- **3 신규 컴포넌트**(`Game.Gameplay.Character`):
  - `LocalMonster`(MonoBehaviour) — HP(`maxHp`)·간단 AI(`chaseRange` 내 추격/밖 Idle, 타깃=`LocalPlayerContext.AbilitySystem.transform` 지연조회)·`TakeDamage(dmg)`→HP≤0→`OnDied(this)`+Destroy. 콜라이더 필요(전투 수집용).
  - `MainMonsterSpawner`(IAsyncStartable) — **비-Joined(Main)일 때만** `MainMonsterSettings`(프리팹+스폰점) 으로 스폰·`InjectGameObject`. `OnDied`→디스폰(+9d 드랍 훅 TODO). Joined(던전)면 스폰 안 함(이중 방지).
  - `LocalCombat`(MonoBehaviour, 플레이어) — `PlayerCharacterAgent.OnAttackPerformed` 구독 → `Physics.OverlapSphere`로 근처 `LocalMonster` 수집 → **서버와 동일 `HitboxMath.Overlaps(SkillCatalog "basic_swing")`** 정밀판정 → `TakeDamage(StatCombatMath.MeleeDamage(10, AttackPower, 0))`(§2.25, 레벨 스탯 반영). `CharacterSpawner` Main 브랜치가 동적 부착(던전은 `CombatSyncSender`).
- **왜 이렇게**: ① 판정 로직은 **Shared.Gameplay DLL 공유**(던전 서버와 같은 `HitboxMath`/`SkillCatalog`) — 이미 배포된 DLL이라 9b/9c는 **DLL 재배치 불필요**(DropTableRoll만 9d 전 필요). ② 몬스터 수집은 `Physics.OverlapSphere`+`GetComponentInParent`로 — 별도 레지스트리 없이 Unity 관용(YAGNI). ③ AI는 Idle/Chase만(MonsterAiMath 미이동, 결정대로 간단). ④ damage 10 = 클라 `GameplayEffectCatalog "basic_attack_dmg"`(Instant Health -10)과 정렬.
- **위치**: `Gameplay/Character/{LocalMonster,MainMonsterSpawner,LocalCombat}.cs` · `CharacterSpawner.AttachLocalCombat`(Main 브랜치) · `VContainer/.../MainLifetimeScope.cs`(`localMonsterPrefab`/`monsterSpawnPoints[]`+`MainMonsterSettings`/`MainMonsterSpawner` 등록).
- **9d 드랍→줍기→지급 완료 2026-06-09**: `MainMonsterSpawner.HandleDied` → `DropTableDefinition.Get(monsterId)`(SO) → `DropEntryDef`→`DropEntry` 변환 → **`DropTableRoll.Roll`(서버 던전과 동일 공유 로직)** → `LocalGroundItem` 스폰. `LocalGroundItem`(IInteractable, `Game.Gameplay.Character`): E 줍기 → `IInventoryGrpcService.GrantItemAsync`(증분8, 서버 가드) → 성공 시 디스폰+토스트(로그). 던전 `GroundItemEntity`(C_PickupItem→서버 중재)와 달리 싱글이라 클라가 직접 지급. **클라 `Shared.Gameplay.dll` 재배치 완료**(DropTableRoll/DropEntry 반영, PowerShell Copy-Item). MainLifetimeScope: `DropTableDefinition` Resources 로드 등록 + `localGroundItemPrefab` 직렬화. **함정**: `Game.System` 네임스페이스가 `System`을 가려 `System.Random`이 `Game.System.Random`으로 오인 → `global::System.Random` 으로 해소(CLAUDE.md 테스트 규칙의 그 함정). Unity 컴파일 0오류.
- **E2E 2026-06-09**: `MainLootE2ETests.Main_슬라임_드랍을_굴려_GrantItem하면_인벤토리에_반영된다`(PlayMode, Docker) — 클라 `DropTableDefinition`(SO, Resources) → `DropTableRoll`(서버 던전과 공유 DLL) → `GrantItem` → `GetInventory` 반영 검증 **1/1 그린**. Main 경로 서버 통신은 GrantItem 1번뿐이라 이 chain 이 진짜 갭(MonoBehaviour 글루는 플레이 검증). **Dockerfile 수정**: 9a의 `Shared.Infrastructure→Shared.Gameplay` 참조로 **GameServer 가 Shared.Gameplay 전이 의존** → `GameServer/Dockerfile` restore 단계에 `Shared.Gameplay.csproj` COPY 누락 → `NETSDK1004`(project.assets.json) 빌드 실패 → COPY 추가로 해소(socketserver 는 원래 참조해 무영향).
- **검증**: Unity 컴파일 0오류 + 상기 E2E 1/1. 히트 수학은 `HitboxMathTests`(Shared), roll 은 `DropTableRollTests`(Shared)가 커버. **Main 플레이 검증 통과(사용자, 2026-06-10)**: 공격→LocalMonster 사망→바닥 드랍 스폰→E 줍기→인벤토리 아이콘 전체 1판 시각 확인(`LocalMonster.prefab`/`LocalGroundItem.prefab` 제작·할당). → **3.3 루트 시스템 던전·Main 양 경로 코드+E2E+플레이 완결 ✅.**
- **후속(범위 밖)**: 정식 획득 토스트 위젯(7.x UI — 현재 픽업 시 로그). 프리팹 제작·할당·플레이 검증은 위에서 완료.

### 2.19 DropTable 데이터화 — Shared 이동 + JSON 단일 소스 (3.3 증분 9a, 2026-06-09)
- **무엇**: 하드코딩 정적 클래스(`SocketServer/Server.Loot.DropTable`)였던 드랍 테이블을 **데이터(JSON) + 순수 roll 로직**으로 분리. 던전(서버)·Main(클라)이 같은 roll 로직을 공유하도록 재배치. 동작·데이터 동일(슬라임 potion 1.0 / gold 0.2) — **순수 리팩터**.
- **3분할(왜 이렇게)**:
  - `Shared.Gameplay`(netstandard, **클라 DLL**) = `DropEntry`/`DropResult`/`DropTableRoll.Roll(entries, rng)` 순수 로직. **JSON 의존 없음**(System.Text.Json 을 클라 배포 DLL에 넣지 않으려고 — Shared.Gameplay 는 순수 유지). 서버·클라가 이 함수로 굴려 결과 일관.
  - `Shared.Infrastructure`(서버 net10) = `DropTableCatalog` — 임베디드 `Loot/drop-tables.json` 파싱 → `monsterId→IReadOnlyList<DropEntry>` + `Roll(monsterId, rng)`(Get→DropTableRoll 위임). **spawn-layouts 와 동일 컨벤션**(SO 저작→JSON bake→임베디드 파싱). `Shared.Gameplay` 프로젝트 참조 추가.
  - 데이터 = `drop-tables.json`(임베디드). 클라는 ScriptableObject 로 저작(9a-2 예정) → 같은 JSON 으로 bake.
- **왜 SO 를 서버가 직접 못 쓰나**: `Shared.Gameplay` 는 Unity 밖 컴파일 DLL → `ScriptableObject` 불가, SocketServer 도 `.asset` 런타임 로드 불가. → 데이터는 SO(클라 저작)→JSON bake→서버 임베디드 파싱(spawn-layouts 와 같은 다리). roll 로직만 DLL 공유.
- **서버 교체**: `CombatHandler.SpawnDrops` 가 `DropTable.Roll` → `DropTableCatalog.Roll`. 기존 `Server.Loot.DropTable.cs` 삭제(같은 ns `GroundItem.cs` 는 유지).
- **위치**: `Shared/Shared.Gameplay/Loot/DropTable.cs`(순수, 2026-06-11 reorg로 `Loot/`로 이동) · `Shared/Shared.Infrastructure/Loot/{DropTableCatalog.cs, drop-tables.json}` · `SocketServer/PacketHandler/Handler/CombatHandler.cs`(교체).
- **테스트**: `Shared.Gameplay.Tests/DropTableRollTests` 5(순수 확률·수량) → **22/22** · `SocketServer.Tests/Loot/DropTableCatalogTests` 5(임베디드 데이터·파싱·위임) → **72/72**. 서버 빌드 0오류.
- **9a-2 SO 저작 레이어 완료 2026-06-09**: `Game.Gameplay.Loot.DropTableDefinition`(SO, monsterId별 drops·`Get(monsterId)` 클라 런타임 조회) + `Game.Gameplay.Editor.DropTableExporter`(`Tools/Loot/Export Drop Tables` SO→JSON bake·`Import` 부트스트랩) — MapDefinition/MapDataExporter 동일 컨벤션. **클라는 SO 직접 읽음**(Resources `Loot/DropTableDefinition`) → bake는 **서버 임베디드 `drop-tables.json`만** 기록(SO가 클라 단일 소스, 클라 JSON 미러 불요). Unity 컴파일 0오류. `.asset` 부트스트랩(사용자 `Tools/Loot/Import` 1클릭)으로 `Assets/GameData/Loot/DropTableDefinition.asset` 생성(현 위치 §2.45 — 단 클라 런타임은 SO 아닌 bake JSON 사용으로 정정됨, SO는 저작 전용).
- **파이프라인 E2E 검증 2026-06-09**: SO에 `goblin`(potion_hp_small 5~10) 추가 → `Tools/Loot/Export` → 임베디드 `drop-tables.json` 반영 → `DropTableCatalogTests.임베디드_goblin_데이터가_로드된다` 통과. **함정**: JsonUtility가 `0.2f`를 `0.20000000298..`(float→double 아티팩트)로 직렬화 → catalog 테스트의 chance 비교는 **근사(`Math.Abs<1e-6`)**. SocketServer.Tests **73/73**. socketserver Docker 리빌드·재배포(임베디드 JSON 갱신).
- **잔여**: ① **클라 `Shared.Gameplay.dll` 재배치**(`Client/Assets/Plugins/Shared.Gameplay/`) — 클라가 `DropTableRoll` 쓰는 9b 전 필수(복사 권한 거부로 보류). ② 9b~9d Main 로컬 전투/드랍/줍기 배선(`DropTableDefinition` SO + `DropTableRoll` 소비).

### 2.18 루트 Main 경로 — GrantItem gRPC + 서버 가드 (3.3 증분 8+10, 2026-06-09)
- **무엇**: 싱글 Main 경로의 인벤토리 지급 진입점. `inventory.proto`에 `rpc GrantItem(GrantItemRequest{item_id,qty}) → GrantItemResponse{result,new_quantity}` 추가 → 클라 `Generated/` 재생성. 던전(co-op·서버권위)과 달리 Main은 클라가 로컬에서 드랍/줍기를 판정하고(클라 신뢰), 줍은 순간 이 RPC로 **직접** 지급 호출(loot-drop.md §1.4).
- **왜 이렇게(권위·경계)**: 던전 경로(`LootGrantConsumer`)는 SocketServer 서버권위→Stream→GameServer라 클라 신뢰 0. Main은 싱글 PVE라 동기화·경쟁이 없어 클라 로컬 시뮬을 수용하되, **영속 지급만은 서버 경계로 가드**. 위조 가능성은 인지하되 싱글 구간+가드로 수용(포트폴리오 결정).
- **서버 가드 3겹**: ① **인증** = `AuthInterceptor`가 `[AllowAnonymous]` 없는 모든 RPC에 JWT 검증 자동 적용 → GrantItem도 무료로 보호(미인증 = `Unauthenticated`). ② **호출당 수량 상한** = `InventoryGrpcService.MaxGrantPerCall=99`, `qty≤0 || qty>99` → `Result.Failure(InvalidRequest)` 거부. ③ **catalog 검증** = `GrantItemAsync`가 `ItemCatalog.Get(itemId)==null`이면 실패(기존 재사용).
- **가드 배치의 핵심**: 수량 상한은 **gRPC 진입점(`InventoryGrpcService.GrantItem`)에만** 둔다. 도메인 `GrantItemAsync`엔 넣지 않음 — 던전 서버권위 경로(`LootGrantConsumer`)도 같은 메서드를 호출하므로 cap을 걸면 정당한 대량 지급이 막힌다. "신뢰 못 하는 클라 진입점"에만 제한.
- **멱등 없음(의도)**: Main 경로는 PickupId 없음(던전과 다름). 싱글 로컬 픽업이라 재시도 중복지급은 클라 책임·비치명. 던전은 PickupId Redis SET 멱등 유지.
- **위치**: `Shared/Shared.Contracts/Protos/inventory.proto`(rpc+메시지) · `GameServer.API/Services/InventoryGrpcService.cs`(`GrantItem` override, `MaxGrantPerCall`) · 클라 `Network/Https/Generated/`(재생성) · `Network/Https/Interfaces/IInventoryGrpcService.cs`·`Services/InventoryGrpcService.cs`(`GrantItemAsync` 래퍼).
- **테스트**: ① **서버 단위 `GameServer.Tests/API/InventoryGrpcServiceTests` 5종 — 5/5 그린**(정상지급+`new_quantity` · 수량상한초과 거부+미지급 · 0이하 거부 · 미존재itemId 거부 · 미인증 거부). 실 `InventoryService`+`FakeInventoryRepository` 합성, `ServerCallContext`는 최소 테스트 더블(`UserState["__HttpContext"].User` 클레임만 — Grpc 테스트 패키지 불필요). ② 클라 `InventoryE2ETests` 4종(동일 시나리오, Docker 서버 대상) — **PlayMode 실행 통과 6/6**(GrantItem 4 + 기존 GetInventory 2, 2026-06-09, GameServer 리빌드·재배포 후). 서버빌드·Game.Network 0오류. **테스트 격리 함정**: NUnit는 fixture 인스턴스를 테스트 간 공유 → `AccessToken`(인스턴스 필드)은 `[SetUp]`에서 초기화 안 됨 → 앞선 로그인 테스트 토큰이 남아 미인증 테스트가 인증됨. `GrantItem_미인증` 테스트는 `AccessToken=null` 명시 후 호출(기존 `GetInventory_미인증`은 알파벳 순 첫 실행이라 우연히 통과하던 것).
- **잔여(증분9, 콘텐츠)**: Main 씬 로컬 몬스터 sim + 로컬 드랍(`DropTable` 공유)/줍기 → `GrantItemAsync` 호출 배선. Main에 처치 가능한 몬스터가 있어야 함(별도 콘텐츠 작업). 정식 획득 토스트 위젯도 후속.

### 2.17 재접속 유예 창 (grace window) — 9.4 회귀 해소 + WBS 6.4 (2026-06-09)
- **무엇**: 크래시/네트워크 끊김(C_PlayerLeave 없음)으로 세션이 사라져도, **방에 다른 플레이어가 남아 있는 한** `PlayerState`를 `Room.ReconnectGraceMs`(60s) 동안 보존 → 그 안에 재접속하면 **보존 상태(위치 등) 그대로 던전 즉시 복귀**. 전원 끊겨 방이 비면 방은 즉시 제거(유예 없음, 클라는 "방 종료" 팝업=후속).
- **왜(회귀 배경)**: 9.4 부채 수정(2026-06-07)이 "**모든** Leave에서 `_playerStates` 즉시 제거"라, 크래시도 상태를 지워 **재접속 시 `RoomJoinLeaveHandler`의 `GetPlayerState==null` → `S_PlayerJoined{Success=false}`** → 재입장 불가 회귀(M4 그린이던 `크래시_후_재접속_성공`·`강제_연결_끊김_후_재접속_성공` 적색). 진단: 클라 임시 로그로 "재접속마다 `S_PlayerJoined` 1개 수신하나 Success=false"를 확인 → 서버 `_playerStates` 부재가 원인.
- **핵심 = 두 퇴장 경로 구분**: **명시 퇴장(C_PlayerLeave, graceful=false)** = 즉시 제거(영구, 9.4 유지) vs **크래시/타임아웃(graceful=true)** = 유예 보존. `Room.Leave(sessionId, graceful)`가 분기 — graceful이면 `_playerStates`를 지우지 않고 `PlayerState.DisconnectedAtMs`(Unix ms) 마킹.
- **AI 유령 회피(9.4 의도 유지)**: `Room.TickMonsters`가 `DisconnectedAtMs!=null` 플레이어를 타깃 후보에서 제외 → 끊긴 플레이어를 몬스터가 쫓지 않음(9.4가 막으려던 유령 잔류를 상태 제거 없이 해결).
- **재접속 복귀**: `RoomJoinLeaveHandler.HandlePlayerJoin`이 `GetPlayerState` 성공 후 `Room.MarkReconnected(userId)`로 마킹 해제 → 보존 위치로 활성화.
- **유예 만료 정리**: `RoomTickService.TickAllRooms`(10Hz)가 `RoomManager.SweepDisconnectedPlayers(nowMs)` 호출 → `Room.SweepExpiredDisconnected(now, grace)`로 만료 상태 제거 + `S_PlayerLeft` 브로드캐스트 + `PublishPlayerLeft`(association 정리). 그래서 ">60s 재접속은 거부"가 보장됨.
- **퇴장 확정 보류**: `RoomManager.LeaveRoom(session, graceful)` — graceful & 방에 인원 남으면 `S_PlayerLeft`/`PublishPlayerLeft`를 **보류**(아직 떠난 게 아님). 단 graceful이라도 방이 비면 즉시 확정. 빈 방 제거 시 보류됐던 끊김 플레이어들도 함께 association 정리(전원 크래시 누락 방지).
- **호출부**: 크래시=`Session.RunAsync` finally→`LeaveRoom(this, graceful:true)` / 타임아웃=`HeartbeatService`→`graceful:true` / 명시퇴장=`RoomJoinLeaveHandler.HandlePlayerLeave`→`LeaveRoom(session)`(기본 false).
- **위치**: `SocketServer/Player/PlayerState.cs`(`DisconnectedAtMs`) · `Room.cs`(`ReconnectGraceMs`·`Leave(graceful)`·`MarkReconnected`·`SweepExpiredDisconnected`·`TickMonsters` 필터) · `RoomManager.cs`(`LeaveRoom(graceful)`·`SweepDisconnectedPlayers`·`PublishPlayerLeft` 잔여정리) · `Monster/RoomTickService.cs`(스윕 호출) · `Session/Session.cs`·`Infrastructure/HeartbeatService.cs`(graceful:true) · `PacketHandler/Handler/RoomJoinLeaveHandler.cs`(MarkReconnected).
- **테스트**: SocketServer.Tests **72/72**(`ReconnectGraceTests` 6 신규: 보존·복귀·만료스윕·유예내무동작·마지막퇴장즉시·전원크래시정리) + **PlayMode 복귀 플로우 Green/Red 6종**(`SocketE2ETests`): 🟢 `크래시_후_유예내_재접속하면_이동한_위치가_보존되어_복귀한다`(보존 위치) · 🟢 `크래시_유예중에는_남은_플레이어에게_S_PlayerLeft가_즉시_오지_않는다`(보류) · 🔴 `명시퇴장_C_PlayerLeave_후_재접속하면_거부된다` · 🔴 `전원_끊기면_방이_사라지고_재접속은_거부된다` · 🔴 `유예_만료후_재접속하면_거부된다`(slow ~74s, 게스트 keepalive로 방유지+호스트 유예만료) + 기존 재접속 2(`강제_연결_끊김`·`크래시_후_재접속`). **SocketE2ETests 19/19 + GameSessionConnectorE2ETests 3/3 그린**. 범위 밖(후속): 재접속 실패/방종료 **클라 팝업**, 방 자체 유예(전원 끊김 시 방 60s 생존).

### 2.16 루트/드랍 던전 경로 서버 풀스택 (3.3 증분 1~5, 2026-06-08)
- **무엇**: 몬스터 처치 → 드랍 roll → 바닥 아이템 → 줍기(수동 F) → 인벤토리 영속 지급. **던전(co-op) 경로 = 서버 권위**(SocketServer 월드 + GameServer 인벤토리, Redis Stream 단방향). 설계=[loot-drop.md](loot-drop.md). 클라 렌더(증분 6)·풀 E2E(증분 7)는 완료(E2E PlayMode 실행만 사람 대기), Main 싱글 경로(증분 8~10)는 미착수.
- **권위 분리(핵심)**: roll·바닥·줍기중재 = **SocketServer**(월드, itemId 문자열만 앎) / 영속 지급·정의검증 = **GameServer**(`ItemCatalog`). 둘은 직접 RPC 금지 → `stream:game:loot:pickup` 단방향. 경계 데이터 = `ItemPickedUpMessage{UserId,ItemId,Qty,PickupId}`만.
- **드랍(SocketServer)**: `DropTable`(정적 카탈로그 `Loot/DropTable.cs`, `monsterId→DropEntry[]`, `slime→potion_hp_small **1.0(보장)** / gold_pouch 0.2`) — roll 은 `Random` 주입(테스트 결정론). **결정 (2026-06-08)**: potion 보장 드랍(1.0) 이유 = dungeon_01 슬라임 1마리뿐이라 확률 드랍이면 풀 루트 E2E(증분7)가 ~40% 거짓실패 → "흔한 몹 = 항상 소량 포션"으로 결정성 확보(밸런스로도 자연스러움). gold 는 확률(0.2) 유지. `CombatHandler.SpawnDrops`가 몬스터 사망 분기에서 roll→`Room.SpawnGroundItem`→`S_SpawnGroundItem`(1830) 브로드캐스트. **자동지급 아님** — 월드에 떨어뜨리고 플레이어가 줍기 선택.
- **바닥/줍기(SocketServer)**: `Room._groundItems`(groundId→`GroundItem`, lock) · `SpawnGroundItem`(GroundId 순차) · `GetAllGroundItems`(입장 로스터) · **`TryPickup(userId, groundId)`** = 거리검증(`PickupRange=3`) + 경쟁 중재(lock 안 Remove 1회만 성공=승자). `LootHandler`(`C_PickupItem` 1832) → TryPickup 성공 시 ① `S_GroundItemRemoved`(1831) 방 브로드캐스트 ② `S_ItemPickedUp`(1833) 본인 토스트 ③ `RoomManager.PublishItemPickup`(PickupId=`{RoomId}:{GroundId}`). 늦은 입장 = `RoomJoinLeaveHandler`가 바닥 로스터 재전송(몬스터 로스터와 동형).
- **지급(GameServer)**: `LootPickupMessageQueue`(Consumer Group `loot-grant-service`, `DungeonClearMessageQueue` 미러) + `LootGrantConsumer`(`ResilientStreamConsumer` 위임) → **PickupId Redis SET claim-first 멱등**(`RedisKeys.LootPickupProcessed`, GrantItem 비멱등이라 이중지급 차단) → `IInventoryService.GrantItemAsync`(Create/Update, 미존재 itemId 는 "unknown item" 스킵). DI=`InventoryInstaller`.
- **패킷/Union**: `Shared.Packet/Domains/LootPackets.cs` 4종 + Union **1830~1833**. 메시지 `Shared.Infrastructure/Messages/ItemPickedUpMessage.cs`. 발행자 `SocketServer/MessageQueue/{ILootPickupPublisher,LootPickupMessageQueue}` + Program.cs DI + `RoomManager`(생성자 5번째 인자).
- **테스트**: SocketServer 단위 **66/66**(`DropTableTests` 5 roll·확률임계·미등록·**보장드랍** / `LootRoomTests` 5 순차ID·범위·경쟁중재) + GameServer **4/4**(`LootGrantConsumerIntegrationTests` 3 지급·PickupId멱등·미존재itemId / 실 Redis Stream `LootGrantRewardE2ETests` 1). 서버 빌드 0오류.
- **풀 E2E (증분7, 2026-06-08, PlayMode 실행은 사람)**: `SocketE2ETests.RawSocket_슬라임_처치_드랍_줍기하면_GameServer_인벤토리에_지급된다` — 처치→`S_SpawnGroundItem`(보장 potion) 관측→드랍 위치로 `C_Move`(거리0)→`C_PickupItem`→`S_ItemPickedUp`/`S_GroundItemRemoved`→`GetInventory` 폴링(Stream 지급 비동기). `StartedRoomContext.HostAccessToken` 추가(재로그인 없이 GetInventory 인증). 클라 Union 1830~1833·미러·인벤토리 gRPC 정합 확인. ※unity-mcp 미연결로 PlayMode 실행은 Unity(사람). Main 싱글 경로(증분 8~10) 미착수.
- **클라(증분6, 2026-06-08)**: 패킷 미러 = **ClientCodegen** 재생성(`Network/Socket/Packets/LootPackets.cs` + `Packet.cs` union 1830~1833 / proto 아님이라 protoc 무관). `ISocketPacketState`에 바닥아이템 상태(`SocketGroundItemSnapshot`·`_groundItems`)·이벤트(`OnGroundItemSpawned`/`OnGroundItemRemoved`/`OnItemPickedUp`)·`AddGroundItem`/`RemoveGroundItem`/`NotifyItemPickedUp`/`GetAllGroundItems`. 핸들러 `Handler/Contents/LootPacketHandler.cs` 3종(SocketApiClient.Install 등록). 렌더/줍기 = `Gameplay/Character/GroundItemEntity.cs`(IInteractable → `ISocketSession.SendAsync(C_PickupItem)`, 로컬 제거 안 함=서버 권위) + `GroundItemSpawner.cs`(MonsterSpawner 미러, IAsyncStartable). DI = `CharacterPrefabSettings.GroundItemPrefab` + `DungeonLifetimeScope`(SerializeField + RegisterEntryPoint). 프리팹 `Assets/Prefabs/Character/GroundItem.prefab`(**Layer 7**=InteractionDetector mask 128, 트리거 SphereCollider, GroundItemEntity 동일 GO — `collider.GetComponent<IInteractable>()` 충족). 검증: 클라 dotnet 빌드(Game.Network/Gameplay/VContainer) 0오류(신규 .cs를 csproj에 임시주입 후 빌드·원복 — Unity 생성 csproj가 stale해 신규파일 미포함). **Unity 잔여(사람)**: 프리팹을 `DungeonLifetimeScope.groundItemPrefab`에 할당 + 서버 리빌드 후 플레이 시각검증. per-item 비주얼·정식 토스트 위젯은 후속(현재 단일 프리팹 + `OnItemPickedUp` 로그).

### 2.15 인벤토리 UI 클라 스택 (7.2, 2026-06-07)
- **무엇**: 인벤토리 창(서버 GetInventory 조회 → 슬롯 렌더). MVI 4레이어를 로비(DungeonLobby) 패턴 그대로.
- **레이어 체인**: `IInventoryGrpcService`/`InventoryGrpcService`(Network, GameApiClient 등록) → `IInventoryService`/`InventoryService`(System, proto→`InventoryItemData` 도메인 변환·`InventoryResult`) → `InventoryModel`+`InventoryState`/`InventoryIntent`/`InventoryItemModel`+`ItemDisplayCatalog`(SO)+`ItemCategory`(Presentation, R3 MVI) → `Inventory`(GUI View, `InventoryModel`만 주입)+`UniversalSlot`/`ItemContentsSlot`(generic 슬롯, Presentation 비참조).
- **정의 분리 일관성**: 서버처럼 클라도 표시 정의를 카탈로그로 — `ItemDisplayCatalog`(ScriptableObject, itemId→이름·Sprite·분류). proto는 itemId+qty만, View엔 합성된 `InventoryItemModel`만 노출.
- **분류(ItemCategory)**: 5종 Equipment/Consumable/Material/Quest/Etc + 탭의 All(=SelectedCategory null). 클라 카탈로그가 분류 소유(서버 부담 0).
- **창 열기**: HUD `btn_Inventory` → `GameHud`가 `InGameModel.Accept(ToggleInventory)` → `InGameModel.OnToggleInventory`(R3 Subject) → `InventoryViewController`(DungeonLifetimeScope EntryPoint)가 `AddressKeys.UI.Inventory` Addressable 로드(최초 1회)·Inject·SetActive 토글. I키도 같은 신호로 합류 예정(현재 던전 InputRouter 미등록 + `.inputactions` Inventory 액션 필요 → Unity 후속).
- **DI**: Network=GameApiClient(루트), System=`InventoryInstaller`(루트, ProjectLifetimeScope), Presentation/GUI=`DungeonLifetimeScope`(InventoryModel·ItemDisplayCatalog·InventoryViewController).
- **위치**: `Network/Https/{Interfaces,Services}/Inventory*`, `System/Inventory/*`, `Presentation/Inventory/*`, `GUI/Inventory/{Inventory,InventoryViewController}.cs`, `GUI/Common/Slots/*`. 테스트 `Tests/PlayMode/E2E/.../InventoryE2ETests`(빈목록·미인증).
- **⚠️ 검증 한계**: 클라 `dotnet build`는 Unity 생성 csproj가 stale해 불가(신규 .cs 미포함 + orphan Game.Input.csproj) → Unity에서 컴파일 검증 필요.
- **2026-06-08 수정/완성**(플레이 검증 중):
  - **열기 버그 수정**: `InventoryViewController`가 `DungeonLifetimeScope`에만 등록돼 **Main 씬에선 토글 신호 수신 불가**였음 → `MainLifetimeScope`에도 인벤토리 스택(ItemDisplayCatalog·InventoryModel·InventoryViewController) 등록. (증상: I키/버튼 누르면 `Accept`까지 가는데 `InventoryViewController.Initialize`가 안 떠 무반응)
  - **슬롯 구조 재설계**: `UniversalSlot`=컨테이너(빈 칸=컨테이너만), 아이템 칸만 `ItemContentsSlot`(Content) **동적 생성**(`EnsureContent`/`ClearContent`, 다른 prefab 요청 시 재생성=타입별 슬롯 대비). `Inventory.cs`는 `slotCount`(기본 30)만큼 `UniversalSlot`을 무조건 생성하고 탭/정렬/내용 변경 시 Content만 교체. 슬롯 두 prefab은 **Addressable 로드**(`AddressKeys.UI.UniversalSlot`/`ItemContentsSlot`, `LoadAssetAsync`→`InitializeAsync`서 로드→BuildSlots→구독→Refresh, OnDestroy Release).
  - **I키**: 생성된 `PlayerInputActions` 래퍼에 `Inventory` 액션 미반영(.inputactions엔 있음) + 던전 InputRouter 미등록 → 임시로 **`GameHud.Update`가 `Keyboard.current.iKey` 폴링**해 `ToggleInventory`(버튼과 동일 funnel). `Game.GUI.asmdef`에 Unity.InputSystem 참조 추가. 래퍼 재생성 후 InputRouter 경로로 이관 예정.
  - **아이콘 매칭**: `ItemDisplayCatalog.entries[itemId→{displayName,icon,category}]`. 서버 itemId(`potion_hp_small`/`potion_mp_small`/`gold_pouch`)와 정확히 일치해야 매칭. **경로 버그 수정**: 에셋이 `Resources/ItemDisplayCatalog`인데 코드가 `"Inventory/ItemDisplayCatalog"`로 로드 → Main 폴백 null이던 것을 `"ItemDisplayCatalog"`로 정정(양 스코프).
  - **던전 클리어 패널**: 입력은 원래도 안 막힘(클리어가 Player 입력맵을 끄지 않음). 막타 드랍 루팅 위해 `GameHud`가 결과 패널을 `dungeonClearPanelDelaySeconds`(기본 4s) **지연 표시**(상태는 즉시, 표시만 지연 — 모델/테스트 무영향).
  - **아이콘 매칭 완료**: `ItemDisplayCatalog.asset`(`Resources/`)에 서버 itemId 3종(`potion_hp_small`/`potion_mp_small`/`gold_pouch`) entry 작성 → 줍기→인벤토리 아이콘 표시 **플레이 검증 통과**.
  - **Unity 잔여(사람)**: Inventory.prefab 탭(Material/Quest/Etc) 토글 배선, 정식 획득 토스트 위젯(현재 `OnItemPickedUp` 로그). ※DropTable: 플레이검증 임시값은 원복했으나 **2026-06-08 E2E 결정성 위해 potion 을 정식 보장 드랍(1.0)으로 확정**(§2.16 결정 로그 참조 — 임시가 아닌 정식값).

### 2.27 장비 도메인 (3.2, 서버 풀스택, 2026-06-15)
- **무엇**: 착용 슬롯 + 장비 스탯 모디파이어 → 2.4 스탯 합산 합류. 서버 풀스택(도메인~gRPC) 완료. 클라 wrapper/UI는 7.2.
- **gRPC(3.2.6)**: `equipment.proto`(`EquipmentService`: Equip/Unequip/GetEquipment) — 도메인별 1 gRPC 서비스 패턴(Auth/User/Lobby/Chat/Inventory/Progression과 동일, Inventory에 안 합침). proto enum `EquipmentSlot`(UNSPECIFIED=0/WEAPON/ARMOR) ↔ 도메인 enum 매핑은 `EquipmentGrpcService`. 슬롯은 서버가 카탈로그로 결정(클라 지정 불가). `MiddlewareInstaller`에 MapGrpcService 등록. 클라 `Generated/`(Equipment.cs·EquipmentGrpc.cs) 재생성(서버 `Protos\*.proto` 와일드카드 = 자동, 클라는 protoc 수동).
- **스탯 노출 안 함**: proto는 itemId+slot만. 스탯은 서버 `GetStatsAsync` 합산이라 클라에 modifier 안 내림(클라는 착용 표시·장착 의도만).
- **설계 결정(3.2 확정)**: 스택형 재사용(개별 인스턴스 X — 강화/랜덤옵션=3.6 YAGNI)·Weapon/Armor 2슬롯·`EquipmentCatalog` 분리(ItemDef와 단일책임 분리). 합류점=`ProgressionService.GetStatsAsync` 한 곳에 Σ modifier(3.2.5, authority-model §4c). 착용상태(UserId,Slot→ItemId)만 DB 영속(3.2.4 `user_equipments`).
- **카탈로그 2분리(핵심)**: `ItemDef`(ItemCatalog)=표시/스택 메타, `EquipmentDef`(EquipmentCatalog)=슬롯+`EquipmentStatModifier`. 같은 itemId로 묶임. 장비는 ItemCatalog에 `Stackable:false,MaxStack:1`로도 등록(소유=InventoryItem 경로 통과). 두 카탈로그 동기는 테스트로 강제.
- **`EquipmentStatModifier`**: PlayerStats 합산항목과 1:1 가산 struct(`Add()` 세트 합산). 시드만 AP/Def 쓰지만 전 스탯 일반화 → 3.2.5에서 base(레벨룩업) 위에 그대로 Σ.
- **착용 상태(3.2.2)**: `UserEquipment` = (UserId,Slot)→ItemId 영속 엔티티(교체형, 슬롯당 1). `IEquipmentService`(`EquipAsync`/`UnequipAsync`/`GetEquippedAsync`/`GetEquippedStatsAsync`)·`EquipmentService`·`IEquipmentRepository`(Cache-Aside, 3.2.3 구현)·`EquipResult`.
- **합산 응집(핵심)**: `GetEquippedStatsAsync`가 착용 세트를 EquipmentCatalog로 Σ → `EquipmentStatModifier`. 3.2.5 `GetStatsAsync`는 base 위에 이 결과를 더하기만(합산 로직은 장비 도메인 소유).
- **검증 책임**: 장착 = `EquipmentCatalog.Get`(장비인가·슬롯) + `IInventoryService.GetInventoryAsync`(소유했는가, 인벤토리=소유 단일진실). 미보유/비장비 → Fail. 해제 = 멱등(빈 슬롯도 성공). 교체 = 같은 슬롯 SetAsync upsert(아이템은 인벤토리 잔존).
- **영속(3.2.3~4)**: `EquipmentRepository`(Cache-Aside+Delete, 인벤토리와 동형) — Redis Hash 1키 `game:user:equipment:{userId}`(field=(int)slot, value=itemId), MISS→DB(`AsNoTracking`)→캐시SET(TTL), Set/Clear→DB→캐시DEL. `user_equipments` (UserId,Slot) 복합키, Slot=enum→int. 마이그레이션 `AddUserEquipments`(raw SQL 멱등, EF 생성 후 Up/Down만 교체 = 스냅샷 일관). DI=`InventoryInstaller`(장비 전용 Installer 안 만듦 — YAGNI).
- **위치**: `GameServer.Domain/Entities/Equipment/`(`EquipmentSlot`·`EquipmentStatModifier`·`EquipmentDef`·`EquipmentCatalog`·`UserEquipment`) · `GameServer.Application/Domains/Equipment/`(`IEquipmentService`/`EquipmentService`·`Interfaces/IEquipmentRepository`·`EquipResult`) · `GameServer.Infrastructure/Domains/Equipment/EquipmentRepository.cs` + `Persistence/Configurations/Equipment/UserEquipmentConfiguration` + `RedisKeys.UserEquipment` + 마이그레이션 + `ItemCatalog`에 `sword_basic`(Weapon+5AP)·`armor_leather`(Armor+3Def) 등록.
- **합산 합류(3.2.5)**: `ProgressionService`가 `IEquipmentService` 주입 → `GetStatsAsync` = base(LevelTable) + `GetEquippedStatsAsync` Σ. 의존 방향 Progression→Equipment→Inventory(순환 없음). **이 한 곳만 바꾸면 SocketServer 전파(게임시작 메시지)·전투 데미지가 장비 반영** — SocketServer 무수정(authority-model §4c "합산 결과만 전달"). 버프 합류도 같은 자리(후속).
- **위치(gRPC)**: `Shared.Contracts/Protos/equipment.proto` · `GameServer.API/Services/EquipmentGrpcService.cs` · 클라 ClientCodegen 산출 `Client/.../Generated/Equipment*.cs` + 래퍼 `Interfaces/IEquipmentGrpcService.cs`·`Services/EquipmentGrpcService.cs`(auto-gen, 수기금지). 클라 wrapper는 ClientCodegen(`ServerAll/Tools/ClientCodegen`, ProtoDir 전체 스캔)이 proto에서 자동 생성 — 새 proto면 `dotnet run --project ServerAll/Tools/ClientCodegen -- <repoRoot>`.

### 2.28 재화(Wallet) 도메인 + 골드=통화 전환 (3.4, 서버 풀스택, 2026-06-17)
- **무엇**: 골드를 **인벤토리 아이템에서 통화(서버 권위 잔액)로 승격**. 드랍/킬 골드는 지갑 잔액으로 적립, 상점(3.5)이 증감. 인벤토리 도메인을 그대로 미러(단일값 버전).
- **도메인 코어**: `UserWallet`(UserId 단일키·`Balance` long·`Add`/`TrySpend`) + `IWalletService`(`GetBalanceAsync`/`AddAsync`/`TrySpendAsync`)·`WalletService`(서버권위, 카탈로그 검증 없음=통화) + `IWalletRepository`/`WalletRepository` + `WalletSpendResult`. 멱등은 호출자 책임(인벤·Exp와 동일).
- **영속**: `WalletRepository` = Cache-Aside+Delete, **Redis String** 1키 `game:user:wallet:{userId}`(정수 잔액). GET HIT 즉시 / MISS→DB(`AsNoTracking`)→SET(TTL). Add/Spend→DB(없으면 lazy create)→SaveChanges→캐시 DEL. **인벤 Hash 와 달리 0 도 캐시**(String "0"은 MISS와 구분 가능). 차감은 도메인 `TrySpend`로 원자 가드(부족→null, 인벤 RemoveQuantity 미러). `user_wallets`(UserId PK), 마이그레이션 `AddWallets`(raw SQL 멱등, EF 생성 후 Up/Down만 교체). DI=`InventoryInstaller` 합류(경제 클러스터, 별도 Installer 안 만듦 YAGNI).
- **gRPC**: `wallet.proto`(`GetWallet`만 — **조회 전용**. 증감 RPC를 안 두는 게 핵심: 클라가 골드 증감을 요청 못 함=치팅 차단. 증감은 서버 내부 루트/킬/상점에서만). `WalletGrpcService`(userId=JWT) + `MiddlewareInstaller` 매핑. 클라 Generated(`Wallet.cs`/`WalletGrpc.cs` + 래퍼)는 GameServer.API 빌드의 ClientCodegen이 자동 산출.
- **골드 리라우트(A안, 핵심 결정)**: 사용자 결정 = "골드=통화로 전환". 드랍 itemId `gold_pouch`→`gold`(drop-tables.json + 클라 `DropTableDefinition.asset` 동기), `ItemCatalog`에서 gold_pouch **제거**, `GameServer.Domain.Currencies.Gold="gold"`·`IsCurrency` 신설. 지급 chokepoint **2곳**이 통화면 인벤토리 대신 지갑으로 분기: `LootGrantConsumer.ProcessAsync`(줍기)·`MainSpawnClaimService.ClaimKillAsync`(Main 킬 roll 루프) → `IWalletService.AddAsync`. **SocketServer는 무수정** — gold를 일반 ground item 문자열로 취급(스폰/줍기/메시지 string-agnostic), GameServer 영속 경계에서만 라우팅. 의미상 키 정리라 Shared drop-tables(양 서버 임베디드) 변경 = 양 서버 Docker 리빌드 필요.
- **위치**: `GameServer.Domain/Entities/Wallet/UserWallet.cs`·`GameServer.Domain/Currencies.cs` · `GameServer.Application/Domains/Wallet/`(`IWalletService`/`WalletService`·`Interfaces/IWalletRepository`·`WalletSpendResult`) · `GameServer.Infrastructure/Domains/Wallet/WalletRepository.cs` + `Persistence/Configurations/Wallet/UserWalletConfiguration` + `Migrations/*_AddWallets` + `RedisKeys.UserWallet` + DbSet `UserWallets` · `GameServer.API/Services/WalletGrpcService.cs` · `Shared.Contracts/Protos/wallet.proto`. 리라우트: `LootGrantConsumer.cs`·`MainSpawnClaimService.cs`(+IWalletService 주입)·`ItemCatalog.cs`·`drop-tables.json`·클라 `DropTableDefinition.asset`.
- **검증**: 서버빌드0 + GameServer **329/329**(Wallet 23 = `UserWalletTests` 8·`WalletServiceTests` 6·`WalletRepositoryIntegrationTests` 9; `LootGrantConsumerIntegrationTests` 골드→지갑 멱등 통합 추가; `MainSpawnClaimServiceTests` 지갑 의존 보정) + SocketServer 103 + Shared 34(DropTableRoll gold) + 양서버 Docker 리빌드 후 PlayMode 던전줍기 E2E 1/1·MainLoot 4/4. 클라는 새 Wallet Generated로 컴파일(E2E 실행=암묵 검증).
- **클라 지갑 표시(2026-06-17)**: 인벤토리 UI에 골드 잔액 연동(인벤 MVI 미러). System `Game.System.Wallet`(`IWalletService`/`WalletService`=`IWalletGrpcService` 래핑 → proto 은닉, `long Gold` 노출 + `WalletResult`) + DI(`WalletInstaller` 루트 등록, `GameApiClient`에 `IWalletGrpcService` 등록). Presentation: `InventoryState.Gold`(불변, 복사메서드 carry) + `InventoryModel`이 `IWalletService` 선택주입(null-safe) → `RefreshAsync`가 인벤·장비와 함께 `GetWalletAsync` 로드. GUI: `Inventory` View가 기존 `_goldText`(SerializeField)에 `state.Gold.ToString("N0")` 바인딩. **View는 InventoryModel만 주입**(proto·System 비노출, MVI 규칙 준수). 골드 변동(루트/킬)은 인벤토리 열 때 새로고침으로 반영(실시간 push는 YAGNI). **검증: PlayMode InventoryModelTests 6/6(컴파일+회귀).** **잔여(Unity)**: Inventory 프리팹에 `_goldText`(TMP) 인스펙터 할당(미할당 시 null-check로 무해 스킵).
- **골드 드랍 밸런스(2026-06-17)**: slime `gold` = **항상 드랍(확률 1.0) + 10~30**(기존 0.2/1~3 → 상향, 사용자 요청). 매 처치마다 골드 보장 + potion(1.0) + 장비 8종(확률) = "골드 + 추가 보상". json + `DropTableDefinition.asset` 동기 + 양서버 Docker 리빌드. `DropTableCatalogTests` 2건 보정(gold 1.0/10/30 단언, "potion+gold 통과" roll). 검증 SocketServer DropTable 11/11 + 던전 줍기 E2E 1/1.
- **잔여(폴리시)**: 정식 지갑 위젯(상점 7.6과 공유)·클라 `ItemDisplayCatalog.asset` 의 사장된 gold_pouch 엔트리 정리·판매(Sell) UI(골드 수급 보조 경로).

### 2.29 상점(Shop) 도메인 + 클라 데이터층 (3.5, 2026-06-17)
- **무엇**: 골드(3.4)·인벤토리(3.1) 위 경제 도메인. 구매/판매 = 두 도메인 **조합**(상점 자체 영속 없음 — 정적 카탈로그 + 지갑·인벤 위임).
- **서버 도메인**: `ShopCatalog`(코드 정적, ItemCatalog/EquipmentCatalog 동형 — itemId→BuyPrice/SellPrice/ShopCategory) + `IShopService`/`ShopService`(IWalletService+IInventoryService 주입) + `ShopBuyResult`/`ShopSellResult`. DI=`InventoryInstaller`(경제 클러스터). Repository·마이그레이션 **없음**(영속은 Wallet/Inventory 소유).
- **원자성(핵심)**: 구매=`Wallet.TrySpendAsync`(차감, 부족 시 거부)→`Inventory.GrantItemAsync`(지급). 지급 실패 시 `Wallet.AddAsync` 환불(보상 트랜잭션 — 서버 단일 프로세스라 분산 트랜잭션 YAGNI). 판매=`Inventory.ConsumeItemAsync`(차감, 미보유 거부)→`Wallet.AddAsync`(적립). **차감 먼저** = "골드 안 내고 받기"/"아이템 두고 골드 받기" 복제 차단. 가격은 서버 권위(클라 위조 불가 — Buy/Sell 요청은 itemId+qty만).
- **gRPC**: `shop.proto`(GetShop/Buy/Sell). GetShop=진열+가격+스탯 미리보기(`ShopGrpcService`가 EquipmentCatalog에서 비0 스탯 파생 — 공개 표시정보, 권위 전투스탯과 별개). proto enum `ShopCategory`(Weapon/Armor/Accessory/Potion) ↔ 도메인 enum은 gRPC 서비스에서 alias로 매핑(`DomainShopCategory`/`ProtoShopCategory` — 같은 이름 모호성 회피). Middleware 매핑 + 클라 Generated(`Shop.cs`/`ShopGrpc.cs` + 래퍼) = ClientCodegen. **주의: `dotnet build -p:SKIP_CODEGEN=true`는 클라 코드젠을 건너뛴다** — 새 proto는 `dotnet run --project ServerAll/Tools/ClientCodegen -- <repoRoot>` 명시 실행 필요(이번에 wallet과 달리 SKIP로 빌드해 누락 → 수동 생성).
- **클라 데이터층(MVI, View 제외)**: System `Game.System.Shop`(`IShopService`/`ShopService` proto 은닉, `ShopItemData`/`ShopStatData`/`ShopCategory`/`ShopResult`; GetShop+Buy — Sell은 판매 UI 시) + `ShopInstaller`(루트) + `GameApiClient`에 `IShopGrpcService` 등록. Presentation `Game.Presentation.Shop`(`ShopModel`/`ShopState`/`ShopIntent`/`ShopItemModel`/`ShopCategory` — 탭/선택/수량/구매 의도 + 골드, InventoryModel 동형). 카테고리 enum 3겹(proto/System/Presentation)은 레이어 격리(GUI는 Presentation만 참조)라 불가피.
- **위치**: 서버 `GameServer.Domain/Entities/Shop/`(ShopCategory·ShopItemDef·ShopCatalog) · `GameServer.Application/Domains/Shop/`(IShopService/ShopService·ShopBuyResult·ShopSellResult) · `GameServer.API/Services/ShopGrpcService.cs` · `Shared.Contracts/Protos/shop.proto`. 클라 `Client/.../System/Shop/`·`Presentation/Shop/`·`VContainer/Installers/ShopInstaller.cs`.
- **클라 View 배선(2026-06-17)**: 상점 창 열기·렌더·구매 전 경로 연결. ① **열기**=GameHud 상점버튼(`btn_Shop`, `SideButtonType.Shop` 기존)만 — 키 없음(S는 WASD 후진과 충돌해 제외). 버튼 → `InGameIntent.ToggleShop` → `InGameModel.OnToggleShop` → `ShopViewController`(EquipmentViewController 동형 독립 토글, Addressable `AddressKeys.UI.Shop`=Shop.prefab 로드·InjectGameObject). **Main 전용**(MainLifetimeScope 등록, 던전 미등록=던전에선 상점버튼 무반응). ② **이동 차단**=`UiInputCaptureBehaviour`(OnEnable/OnDisable → `ShopModel.Begin/EndUiCapture` → IInputContext refcount, 인벤/장비와 동일). ③ **View**(`Shop.cs`)=`[Inject] ShopModel` + State 구독 Render(탭 필터·선택패널 이름/아이콘/스탯/수량/가격) + Intent(탭 onValueChanged→SelectTab, 슬롯 select→SelectItem, +/-·입력칸→SetQuantity, BuyButton→Buy). **리스트/스탯 슬롯은 Addressable prefab 동적 생성**(`Shop_Item`/`Status_Slot` → `AddressKeys.UI.ShopItem`/`ShopStatusSlot`, 인벤토리 LoadSlotPrefabsAsync 패턴 — `LoadAssetAsync`→`GetComponent`→사전배치 자식 제거→`Instantiate` 풀링, OnDestroy `Release`). 슬롯 `ShopItemSlotView.Bind`/`ShopItemStatusSlot.Bind` 추가. 탭 enum=GUI `ShopItemType`↔Presentation `ShopCategory` 매핑. ④ **구매 결과 토스트**: `ShopModel.OnToast`가 `Subject<string>`→`Subject<ShopToastMessage>`(메시지+`Success` 플래그)로 변경 — 구매 성공/실패/미선택을 결과별 메시지로 발행. View 가 **창 내 토스트 텍스트**(`toastText`, 성공=초록/실패=빨강, `toastSeconds` 후 UniTask 로 자동 숨김, 미할당 시 로그 폴백)로 표시. 정식 전역 토스트 위젯(7.x)과 별개의 상점 자체 피드백. **검증: ShopModelTests 7/7(진열/구매성공·실패 토스트/미선택/선택·수량클램프/탭) + InventoryModelTests 6/6(전체 컴파일) + InputRouter/InGameExpRelay 12/12(입력·HUD 무회귀) + 양서버 Docker 리빌드 후 컨테이너 healthy(Shop gRPC 호스트 부팅).**
- **검증(서버)**: 서버빌드0 + GameServer **340/340**(통합 포함, Shop 11=ShopCatalog 3·ShopService 8 incl 환불·구매·판매).
- **결정(사용자)**: 판매가=아이템별 명시 sellPrice / 클라 UI=구매만(WIP 맞춤) / 재고=무한(YAGNI) / 열기=**HUD 상점버튼만**(S키는 WASD 후진 충돌로 제거, 2026-06-17)·이동차단.
- **잔여(Unity/후속)**: **3개 prefab을 Unity Addressable로 마킹**(주소 = 코드 키와 정확히 일치: `Assets/Prefabs/GUI/Shop/Shop.prefab`·`Shop_Item.prefab`·`Status_Slot.prefab`) — 코드 키만 추가됐고 Inspector Address 지정은 사람 / QuickSetting 실행·`CloseButton`/`btn_Shop`/**`ToastText`(구매 결과 표시용 TMP — 미할당 시 로그 폴백)** 확인 / 골드 표시 필드(상점 prefab엔 없음) / 판매 UI. (양서버 Docker 리빌드는 완료, S키 제거 완료.)
- **E2E(3.2)**: `EquipmentE2ETests`(PlayMode, Docker GameServer 대상) 6 — 미보유거부/미인증거부/빈조회/멱등해제/미지정거부. happy-path(보유 장착)는 공개 API로 장비 소유 경로가 없어 서버 단위가 담당. `E2ETestBase.SetUp`이 토큰 리셋(미인증 테스트 순서 의존 버그 방지) — 전 Https E2E 공통.

### 2.30 캐릭터 진행 영속 합류 (6.1, 2026-06-17)
- **무엇**: 레벨·인벤토리·장비·지갑이 로그아웃→재로그인에 보존되는지 합류 검증. **영속 레이어는 이미 완비** — 네 도메인 전부 DB 테이블 + cache-aside(MISS→DB), 로그인은 토큰만 반환·클라가 도메인별 pull 로 DB 복원. 새 구현 없음 = **검증 작업**.
- **재접속 E2E**: `CharacterPersistenceE2ETests`(PlayMode, Docker) — register → `ClaimMonsterExp`(exp)+`ClaimKill`(potion 보장·gold 항상) 으로 진행 변경 → 스냅샷 → `Logout` → 재`Login`(새 토큰/세션) → GetProgression/GetInventory/GetWallet 가 스냅샷과 **동일**(=DB 복원). 1/1 그린. `E2ETestBase` 에 `WalletService` 추가(기존 미노출).
- **장비 제외 이유**: E2E 에서 장비 아이템을 결정적으로 획득하는 공개 경로가 없음(드랍=랜덤, 상점=골드 누적+쿨다운). 장비 영속은 동일 cache-aside 라 `EquipmentRepositoryIntegrationTests`(MISS→DB) 가 직접 증명. 각 도메인 개별 영속도 Repository 통합테스트(Wallet 9·Inventory·Equipment 8·Progression)로 기존 검증 — 6.1 은 그 위에 "재접속 전체 흐름"을 얹은 것.
- **위치**: `Client/.../Tests/PlayMode/E2E/Network/Https/CharacterPersistenceE2ETests.cs` · `E2ETestBase.cs`(WalletService).

#### 2.27a 장비 GUI 연동 + EquipmentType 공통화 (7.2, 2026-06-16)
- **공통 enum 통일(핵심)**: 서버 도메인 `EquipmentSlot`(Weapon/Armor 2값) → **`Shared.Gameplay.Equipment.EquipmentType`**(8값: None/Header/Armor/Shoose/Glove/Shield/Weapon/Ring/Necklace) 단일 소스. 클라(GUI)·서버 도메인이 같은 enum 사용. proto enum도 `EquipmentType`(9값)로 확장 — **정수값 1:1**이라 경계 매핑이 캐스팅(`(ProtoType)(int)x`)으로 단순화. 카탈로그는 Weapon/Armor만 채움(나머지 6 = GUI 표시·미래 확장 빈슬롯). `GameServer.Domain`이 `Shared.Gameplay` 참조 추가. user_equipments 테이블 비어 마이그레이션 영향 없음(Slot 열=int, 값 의미만 변경).
- **클라 동기화**: ClientCodegen 재실행(stub `Equipment.cs`·래퍼 `EquipmentGrpcService`) + `Shared.Gameplay.dll` → `Client/Assets/Plugins/Shared.Gameplay/` 재배치(서버 빌드 산출물 복사).
- **클라 MVI(신설)**: Network(`IEquipmentGrpcService` 자동생성) → System(`IEquipmentService`/`EquipmentService` `Client/.../System/Equipment/`, proto 은닉, **`OnChanged` 이벤트**=장착/해제 성공 시 발행) → Presentation(`EquipmentModel`/`EquipmentState`/`EquipmentIntent`/`EquipmentSlotModel` `Client/.../Presentation/Equipment/`, OnChanged 구독→Refresh, ItemDisplayCatalog 공유) → GUI(`Equipment` View `Client/.../GUI/Equipment/`, EquipmentModel만 주입, 8슬롯 아이콘 렌더).
- **장착 트리거(인벤토리→장비)**: `ItemActionPanel.Bind(itemId, onUse, onEquip, canUse, canEquip)` — 분류별 버튼(장비=장착/소모품=사용/그외 둘다 비활성). `Inventory.OpenActionPanel`이 `InventoryItemModel.Category`로 결정. 장착=`InventoryModel.Accept(EquipItem)` → `IEquipmentService.EquipAsync`(InventoryModel에 IEquipmentService 주입) → 성공 OnChanged → EquipmentModel 자동 Refresh(인벤토리에서 껴도 장비창 즉시 갱신).
- **해제 + 인벤토리 연동(2026-06-16)**: 장비창 슬롯 Button 클릭 → `ItemActionPanelController`(`GUI/Common`, Inventory와 공용 추출 = 패널 Addressable 로드/슬롯 우측 배치/백드롭 닫기) → `ItemActionPanel`(unEquipButton) → `EquipmentIntent.Unequip(slot)`. `ItemActionPanel.Bind(itemId, onUse, onEquip, onUnequip, canUse, canEquip, canUnequip)` — 호출처가 버튼 노출 결정(인벤토리=use/equip, 장비창=unequip). Equipment.Render: 미착용 슬롯 Icon·Button GameObject 비활성, 착용 시 활성+아이콘. **착용↔인벤토리 동기**: `InventoryModel`이 `IEquipmentService.GetEquippedAsync` 로 착용 itemId를 표시에서 제외 + `OnChanged` 구독 → 장착=인벤토리에서 사라지고 장비창 표시, 해제=반대(양쪽 동시 Refresh). 장비=스택1이라 itemId 매칭으로 충분. 테스트 `InventoryModelTests` 착용제외 필터 1 추가(6/6).
- **열닫 연동(req)**: I키/HUD = 인벤토리+장비 **쌍 토글**(`InventoryViewController`가 `EquipmentViewController.ShowAsync/Hide` 호출, 인벤토리 상태 기준 둘다 열기/닫기). K키(`<Keyboard>/k`) = 장비 **단독 토글**(`InGameIntent.ToggleEquipment`→`InGameModel.OnToggleEquipment`→`EquipmentViewController`). 각 창 X버튼 = 자기 SetActive(false) 독립. GameHud.Update가 iKey/kKey 임시 폴링(InputRouter 이관 대기). Addressable `AddressKeys.UI.Equipment`. DI: `EquipmentInstaller`(System, ProjectScope) + `EquipmentModel`·`EquipmentViewController`(Dungeon/Main scope).
- **검증**: 서버 306/306 + 장비통합 8 + E2E 6/6(enum 통일 후 재검) + 클라 컴파일 0에러 + InventoryModelTests 5/5.
- **⚠️ 미해결(별건, 사용자 입력 변경)**: `InputRouterTests` 9개 실패 — 사용자가 수정한 `InputSystem_Actions.inputactions`·`PlayerInputActions.cs`(Equipment 액션 추가) 영향. 장비 GUI와 무관(Input/ 미변경). 모든 Player 액션이 안 켜지는 증상 → 입력 에셋/생성코드 정합성 별도 점검 필요.
- **⚠️ 생성자 변경 회귀 교훈**: `ProgressionService` 생성자에 `IEquipmentService` 추가 → `IProgressionService`를 등록하던 **테스트 DI 호스트 4곳**(DungeonResultConsumer통합·DungeonResultReward E2E·GameStart E2E·RoomLifecycle)이 의존 미등록으로 DI 해석 실패→타임아웃. 실 스택 테스트는 장비 체인(Inv+Equip Repo/Service) 등록, Fake 테스트는 `FakeEquipmentService`(0 modifier) 등록으로 해소. 서비스 생성자 변경 후엔 단위만 보지 말고 전체(통합·E2E) 회귀를 돌릴 것([[always-run-full-e2e-suite]]).
- **테스트**: `EquipmentCatalogTests` 4 + `EquipmentServiceTests` 8(`FakeEquipmentRepository`) + `EquipmentRepositoryIntegrationTests` 8(Testcontainers) + `ProgressionServiceTests` 장비합산 1(`FakeEquipmentService`) + `EquipmentGrpcServiceTests` 7 = 28.

### 2.14 인벤토리 도메인 (3.1, 2026-06-07)
- **무엇**: 서버 권위 아이템 소유 영속 도메인. 모든 보상/장비/상점/루트의 공통 전제.
- **정의 vs 소유 분리(핵심)**: 아이템 *정의*(이름·등급·MaxStack·아이콘키)는 **코드 카탈로그 `ItemCatalog`**(DB 아님) — `GameplayEffectCatalog`·`MonsterCatalog`·spawn-layouts 와 동일 컨벤션(정적 기획데이터는 카탈로그). DB엔 **소유(수량)만**. → `items` 테이블 없음.
- **소유 모델**: `InventoryItem` = 스택형 `(UserId, ItemId) → Quantity` 복합키. 장비 인스턴스(개별 상태)는 3.2 Equipment로 미룸(YAGNI). 키=user_id(미래 character_id 이관, [[character-swap-direction]]).
- **캐시**: 유저당 Hash 1키 `game:user:inventory:{userId}`(field=itemId, value=qty). Cache-Aside+Delete. 빈 인벤토리는 캐시 안 함(HGETALL 빈결과=MISS 구분 불가 → DB 폴백, 트래픽 낮아 무해).
- **위치**: 엔티티/카탈로그 `GameServer.Domain/Entities/Inventory/`(`InventoryItem`·`ItemDef`·`ItemGrade`·`ItemCatalog`) · `GameServer.Application/Domains/Inventory/`(`IInventoryService`/`InventoryService`·`Interfaces/IInventoryRepository`·`ItemGrantResult`) · `GameServer.Infrastructure/Domains/Inventory/InventoryRepository.cs` + `RedisKeys.UserInventory` + `Configurations/Inventory/InventoryItemConfiguration`(복합키) + 마이그레이션 `AddInventoryItems`(raw SQL) · DI `InventoryInstaller`(Program.cs) · gRPC `InventoryGrpcService`(`MiddlewareInstaller`) · proto `inventory.proto`(GetInventory) + `ServerCallContextExtension.GetUserId()`.
- **진입점**: `IInventoryService.GrantItemAsync(userId, itemId, amount)` → 카탈로그 검증 + MaxStack clamp 적립 → `ItemGrantResult`. 보상/루트(3.3)가 호출(멱등은 호출자 책임 = Exp 보상과 동일). 조회 `GetInventoryAsync` → gRPC `GetInventory`(userId=JWT sub/NameIdentifier — MapInboundClaims 리매핑 대응 `ServerCallContextExtension.ResolveUserId`).
- **CRUD 완성(D 추가, 2026-06-07)**: `ConsumeItemAsync(userId,itemId,amount)` → `Repo.RemoveQuantityAsync`(보유 검증·차감·0이면 행삭제·캐시 DEL, 미보유/부족 → null) → `ItemConsumeResult`(남은수량). **클라가 직접 CRUD 안 함(서버 권위/치팅 방지)** — 획득=서버 드랍(3.3), 소비 클라 RPC·포션 효과=3.8. 현재는 도메인 C/U/R/D + 테스트만.
- **범위 밖**: 획득 push 알림(3.3), 인벤토리 UI(7.2 — proto는 itemId+qty만, 클라가 자기 카탈로그로 표시).
- **테스트**: 단위 13(엔티티 6·카탈로그 2·서비스 5) + Testcontainers 통합 7(Cache-Aside Delete 계약) = 20/20.

### 2.13 기술 부채 정리 1차 (9.1·9.3~9.7, 2026-06-07)
- **9.6 GetRooms N+1 제거**: 방 목록이 방마다 `GetPlayersByRoomIdAsync`+`GetByIdsAsync` 2왕복(2N)이던 것을 → `IDungeonRoomPlayerRepository.GetPlayersByRoomIdsAsync(roomIds)`(단일 AsNoTracking 쿼리) + 유저 1쿼리 배치로 축소. 조립은 `DungeonRoomExtensions.ToRoomInfo(this DungeonRoom, IReadOnlyList<User>)` **동기 오버로드**(추가 I/O 0). **단일 방 응답(생성/입장)은 기존 async `ToRoomInfo(repo,repo)` 유지** — 1방은 N+1 아님. `FakeDungeonRoomPlayerRepository`도 동일 메서드 구현. count/페이징은 proto(공개계약) 변경이라 보류. 위치: `GameServer.API/Services/DungeonLobbyGrpcService.GetRooms`, `GameServer.API/Extensions/DungeonRoomExtensions.cs`, `GameServer.Infrastructure/.../DungeonRoomPlayerRepository.cs`.
- **9.4 Room.Leave PlayerState 정리**: `SocketServer/Room/Room.Leave`가 `_playerSessions`만 지우고 `_playerStates`(userId→PlayerState)를 남겨 떠난 플레이어가 AI 타깃·위치 계산에 유령 잔류하던 버그. Leave가 제거 직전 `session.UserId`를 잡아 `_playerStates.Remove`. 테스트 `RoomManagerLeaveRoomTests.퇴장한_플레이어의_PlayerState는_정리된다`. **⚠️ 정정(2026-06-09, §2.17)**: "모든 Leave에서 즉시 제거"가 크래시=재접속 불가 회귀를 만들어 → **명시퇴장만 즉시 제거, 크래시는 유예 보존+AI 필터**로 세분화됨. 유령 회피는 `TickMonsters`의 `DisconnectedAtMs` 필터가 대체.
- **9.5 Consumer name**: `GameStartRequestedMessageQueue.ConsumerName` `socket-1`(상수) → `socket-{Environment.MachineName}`(static readonly). 수평 확장 시 PEL 추적 충돌 방지, 컨테이너 hostname 안정적이라 재시작 PEL 복구 유지.
- **9.1 SocketServer 설정 가시성**: IP는 이미 `ServerOptions`(Server 섹션·env)로 구성됨(코드 변경 0). `appsettings.json`에 `Server` 블록 명시 + docker `AdvertiseIp`에 "원격 배포 시 호스트 IP 교체" 주석.
- **9.3 단일 세션 강제 = 이미 구현 확인**(무변경): `UserSessionRepository.CreateSessionAsync`가 로그인 시 기존 세션 DB+캐시 제거, refresh 바인딩 실패도 세션 제거. 부채 설명이 stale이었음.
- **9.7 status.md**: stale 226줄 파일 **삭제** + 참조(CLAUDE.md·AGENTS.md "현황 확인" → plan.md, plan.md 자기참조 제거). 현황 진실원 = plan.md 단일화.
- **9.2 해소**(2026-06-25, =4.3): `DungeonRoom.MapId`(string) **방 생성 시 결정·영속**. 명명은 `DungeonId` 대신 **기존 스폰 카탈로그 키와 통일된 `MapId`**(식별자 1개 — 매핑 오버헤드 제거). 흐름: `CreateRoomRequest.map_id`(proto, 클라 Generated 재생성) → `CreateDungeonRoomAsync`가 빈값→`MapIds.Default`·`SpawnLayoutTable.IsKnown` 검증(실패=거부, Application 권위) → `DungeonRoom.Create(..,mapId)` → DB `dungeon_rooms.map_id`(EF `AddDungeonRoomMapId`, varchar(64) NOT NULL, 기존행 `dungeon_01` 백필) + Redis Hash `MapId`(구캐시엔 없어 파싱 시 Default 폴백). `StartGameAsync`는 **방의 `MapId`를 진실의 원천**으로 `GameStartRequestedMessage.MapId`에 적재(명시 param=E2E override 우선). Domain은 값 보관만(Shared 미참조). 위치: `Domain/Entities/DungeonRoom/DungeonRoom.cs`(`MapId`+`Create`/`Clone`/`FromRedis`)·`Application/.../DungeonLobbyService.cs`·`Infrastructure/.../DungeonRoomRepository.cs`(`ToHashEntry`/`ParseFromRedis`)·`.../Configurations/DungeonRoom/DungeonRoomConfiguration.cs`·`Shared.Infrastructure/Spawn/SpawnLayoutTable.IsKnown`. 테스트: 단위 64(도메인 3+서비스 4 신규)·통합 `MapId_ShouldRoundTripThroughDbRedisAndCacheMiss`(Docker). **후속 완료(2026-06-25)**: ① **던전 선택 UI** — `RoomInfo.map_id`(proto7) + `DungeonRoomExtensions.ToRoomInfo` 양 오버로드 + 클라 체인 `LobbyIntent.CreateRoom.MapId`→`LobbyModel`→`LobbyRepository`→`System.DungeonLobbyService`→`CreateRoomRequest.MapId` + `DungeonCatalog` SO(`Presentation/DungeonLobby`, mapId→표시이름) + `CreateDungeonRoomPopupView` TMP_Dropdown(폴백) + `DungeonRoomModel.MapId`. ② **데이터 추가 경로**: 던전 N개 = `MapDefinition` SO(`Assets/GameData/Maps/{mapId}.asset`) 저작 → `Tools/Spawn/Export`(`MapDataExporter`)로 `spawn-layouts.json`(클라 Resources+서버 임베디드) bake → 서버 재빌드. 몬스터=`MonsterCatalogDefinition`→`monsters.json`, 드랍=DropTable→`drop-tables.json`, 비주얼=`MapDefinition.visualPrefab`(클라 전용). ③ **expReward Export 결함 수정**: `MapDataExporter.MapDto`에 `expReward` 누락 → Export 시 `dungeon_01` 클리어보상 100 소실 footgun. `MapDefinition.expReward` 필드+왕복+`dungeon_01.asset` 100 설정으로 해소. 테스트: 서버 65(`ToRoomInfo MapId`)·통합 11(`MapId_ShouldRoundTrip…` 실DB/Redis)·SocketServer expReward 가드 7·클라 EditMode 3(`CreateRoom MapId 전파`)·**Docker 리빌드 후 PlayMode `DungeonLobbyE2ETests` 13/13**. **Unity 에셋 완료(2026-06-25)**: 샘플 던전 `dungeon_02`(`Maps/dungeon_02.asset`, expReward150) Export 반영(=A검증: dungeon_01 100 보존) + `GameData/DungeonCatalog.asset`(2항목) + 팝업 `CreateDungeonRoomPopup.prefab` TMP_Dropdown 배선. 신규 .cs/.asset `.meta` 커밋 시 `git add -f`([[unity-meta-gitignored]]).

### 2.12 진행/성장(Progression) Exp 영속 도메인 (M4 B 트랙)
- **무엇**: 플레이어 경험치 영속용 신규 도메인. `user_progressions`(users 1:1, PK=FK · `Level`/`Exp`/`UpdatedAt`) — `UserProfile` 컬럼이 아니라 **별도 테이블**.
- **왜 별도 테이블**: 미래 원신식 캐릭터 교체에서 Exp/Level은 **캐릭터 귀속** → 나중에 키를 `user_id`→`character_id`로 이관만 하면 됨(프로필·인증 무접촉). 지금은 계정당 암묵적 캐릭터 1개라 `user_id` 키. 캐릭터 시스템은 미구현(YAGNI). [[character-swap-direction]]
- **왜 던전 Exp는 DB 안 씀**: 던전→Exp 매핑은 정적 기획데이터 + 두 서버 공유 → **Shared 카탈로그(spawn-layouts.json)**. DB에 넣으면 SocketServer가 GameServer DB를 봐야 해 "서버 간 직접 참조 금지" 위반.
- **위치**: 엔티티 `GameServer.Domain/Entities/User/UserProgression.cs`(`AddExp` 누적·0이하 무시) · 인터페이스 `GameServer.Application/Domains/Progression/Interfaces/{IProgressionRepository,IProgressionService}` · 구현 `GameServer.Application/Domains/Progression/ProgressionService.cs` + `GameServer.Infrastructure/Domains/Progression/ProgressionRepository.cs`(Cache-Aside+Delete, lazy get-or-create, `AsNoTracking` 읽기) · `RedisKeys.UserProgression` · DI=`UserInstaller` · 마이그레이션 `AddUserProgressions`(raw SQL).
- **테스트**: 엔티티 5 + 서비스 단위 3 + Repository Testcontainers 통합 6(Cache-Aside Delete 계약) + 보상 컨슈머 통합 2(InMemory) + **실 Redis Stream E2E 1**(`DungeonResultRewardE2ETests` — 발행→Consumer Group→DB Exp).
- **연결(다음)**: `DungeonResultConsumer.ProcessAsync`(§2.9)가 `ProgressionService.AddExp`를 참가자별 호출(보상 지급 = B 트랙 잔여).

### 2.11 클라/서버 권위 판단 기준 문서화 (authority-model.md)
- **무엇/왜**: "이 값/동작을 클라가 소유하나 서버가 소유하나"를 기능마다 재논쟁하지 않도록 **판단 4축**(①치팅 ②일관성 ③반응성 ④결정론/공유공식)과 **본 프로젝트 매핑**을 정식 문서로 박제. 트리거 = "서버 권위 근거가 부족하게 느껴진다"는 피드백.
- **핵심**: 수치·판정·보상=서버 / 연출·입력=클라 / 결정론 가능=공유 코어(미전송). 비대칭(플레이어 HP=클라 결정론 vs 몬스터 HP=서버)은 각 대상의 지배 축이 달라서임을 명시.
- **데미지 표시 결정(A안)**: 숫자는 **서버 응답값**(시전자도 예측 안 함, 공식은 서버 소유). 연출만 입력 즉발. 구현=클라가 이전HP−새HP 델타로 플로팅 텍스트(패킷 무변경), 막타는 마지막 HP 근사. 정밀 필요 시 B안(`Damage` 필드)로 승격.
- **위치**: [authority-model.md](authority-model.md). 설계 시 먼저 이 4축에 대입.

### 2.10 입력 시스템 전역화 (PlayerInputActions·InputContext = 루트 Singleton)
- **증상**: 던전 진입 시 PlayerCharacter가 안 움직임(`PlayerInputComponent`의 Action 콜백 0). 또 UI 점유 입력 차단(`InputContext`)이 Main에서만 동작.
- **근본 원인**: `PlayerInputActions`가 `ProjectLifetimeScope`에서 **`Scoped`**로 등록되고 Main/Dungeon이 **각자 또 `Scoped` 재등록** → **스코프마다 다른 인스턴스 3개**. ① 던전 인스턴스의 `Player` 맵을 아무도 `Enable()` 안 함(Main은 `MainSceneInitializer`가 켰지만 던전엔 대응물 없음) → 콜백 0. ② `InputContext`를 루트에 두면 씬 인스턴스와 달라 "안 먹힘" → 팀이 `OutgameInstaller`(Main)로 우회 등록(증상 회피, 원인=Scoped 미해결).
- **수정(전역 단일 인스턴스)**: `PlayerInputActions` → **루트 `Singleton`**(`ProjectLifetimeScope`), Main/Dungeon **재등록 삭제**(자식 스코프가 부모 등록 resolve → `CharacterSpawner._container.InjectGameObject`가 루트 인스턴스 주입). `IInputContext→InputContext`도 **루트 Singleton**(전 씬 UI 게이팅). 맵 활성화는 **`GlobalInputInitializer`(루트 `IInitializable`)**가 진입 시 1회 Enable. 구 `MainSceneInitializer`/`DungeonSceneInitializer` **삭제**(전역으로 통합). `OutgameInstaller`의 `IInputContext` 등록 삭제(루트로 이동). `InputRouter`/`InteractionSystem`는 OutGame 전용이라 Main 스코프 유지(루트 싱글톤 resolve).
- **Dispose 함정**: 전역 공유이므로 씬 나갈 때 `PlayerInputActions.Dispose()` 금지(다른 씬 깨짐). 루트 Singleton이라 **VContainer가 앱 종료 시 1회 dispose** → `GlobalInputInitializer`는 Enable만, 수동 Dispose 안 함. `Title`은 입력 맵 미접촉(자동로그인만)이라 충돌 없음.
- **후속 버그(전역화로 드러남)**: 전역 싱글톤이 되자 **Main 스코프 전용 `InputRouter.Dispose()`가 `_actions.Player.Disable()`**로 그 싱글톤 맵을 껐다 → Main→Dungeon 전환 시 Main 스코프 파괴 → 던전 입력 사망(`PlayerInputComponent 구독 완료 ... Player.enabled=False` 로그로 확인). 수정: `InputRouter`에서 맵 Enable/Disable 제거(맵 소유 = `GlobalInputInitializer`만, 라우팅만 담당). **교훈: 씬 스코프 컴포넌트는 전역 입력 상태(enable/disable)를 Initialize/Dispose에서 건드리면 안 된다.**
- **남은 잠재 부채**: `InputRouter`가 전역 `PlayerInputActions.performed`에 람다 구독(Initialize) → Dispose에서 미해제(람다 캡처라 해제 어려움). Main 재진입 시 중복 구독 누수 가능(L키 이중 처리). 실사용 빈도 낮아 보류 — 명명 델리게이트로 unsubscribe 필요 시 후속.
- **원칙**: 입력은 게임 생명주기 전역 관심사 → 씬 스코프에 묶지 않는다(CLAUDE.md 원칙 3). 한 씬에서만 동작하는 입력/UI게이팅은 구조 오류.
- **검증**: Unity 컴파일 0오류 + EditMode **137/137** + 던전 진입 로그로 `Player.enabled` False→(수정 후)True 전환 확인 예정. ※실제 Main·Dungeon 양쪽 이동/UI게이팅은 사용자 플레이 확인.

### 2.9 던전 클리어 루프 (M4 A 트랙 — 전멸 감지 → 결과 → 로비 복귀)
- **무엇/왜**: 방의 몬스터 전멸을 **서버 권위로 1회만** 감지해 ① 클라에 결과 화면을 띄우고 ② GameServer에 보상 산정용 이벤트를 통지. DoD "클리어→복귀" 골격(보상은 B 트랙).
- **감지(SocketServer)**: `Room.TryMarkCleared()`(`Room.cs`) — `_monstersSpawned`(빈 방 오판 방지) && 살아있는 몬스터 0 && `!_cleared` 일 때 최초 1회 true(lock(_monsters), 중복 발화 차단). 사망 몬스터는 `DamageMonster`가 즉시 제거하므로 `_monsters.Count==0`==전멸. 호출 위치 = `CombatHandler.ApplyAttackToMonsters`(처치 후 `anyKilled && TryMarkCleared()`).
- **두 경로 통지**: ① 클라 — `room.Broadcast(S_DungeonClear{RoomId})`(Union **1820**, `Shared.Packet/Domains/DungeonPackets.cs`). ② GameServer — `session.RoomManager.PublishDungeonClear(room)` → `IDungeonResultPublisher`→`DungeonResultMessageQueue`(`stream:game:dungeon:result`) → `DungeonClearMessage{RoomId,MapId,Participants[]}`. 던전 식별은 현재 `MapId`(DB `DungeonId`는 B 트랙 부채 9.2). 발행 패턴 = `IRoomLifecyclePublisher` 미러.
- **소비(GameServer)**: `DungeonClearMessageQueue`(Consumer Group `dungeon-result-service`) + `DungeonResultConsumer`(`ResilientStreamConsumer` 위임, §2.8). **보상 지급(B, §2.12)**: `expReward = SpawnLayoutTable.Get(MapId).ExpReward`(Shared 카탈로그 = SocketServer 표시값과 동일 소스) → 참가자 전원 `IProgressionService.AddExp`(scope per 메시지). **멱등** = RoomId 를 Redis SET(`RedisKeys.DungeonResultProcessed`)에 claim-first(at-most-once, AddExp 비멱등이라 이중지급 차단). 보상은 **Exp 전용**(인벤토리·Outbox 제외, 2026-06-06 범위 확정). DI = `DungeonInstaller`.
- **클라(Presentation)**: codegen 미러 `S_DungeonClear{RoomId,RewardExp}` → `DungeonClearPacketHandler` → `ISocketPacketState.MarkDungeonCleared(exp)`/`OnDungeonCleared(long)`(`SocketApiClient.cs`) → `InGameModel` 구독 → `InGameResult.DungeonCleared(exp)`→`InGameState.IsDungeonCleared`+`RewardExp`→`GameHud`가 `DungeonClear` 패널 활성+`SetReward`. 결과 패널(`DungeonClear`/`DungeonFailed`)의 자체 return 버튼이 `InGameIntent.ReturnToLobby`(§2.1 복귀=`LoadSceneAsync("Main")`) 재사용. (※구 상시 `returnToLobbyButton`은 2026-06-25 제거 — 결과 패널 버튼으로 일원화. `IsReturning` 상태는 모델/테스트 유지.)
- **실패 경로(전원 다운, B)**: 클라 로컬 HP 0(`InGameModel.OnAttributeChanged` Health≤0) → `C_PlayerDead`(1822) 1회 송신(`_localDeadReported` 가드, 플레이어 HP=클라 권위) → 서버 `DungeonLifecycleHandler` → `Room.TryMarkFailed(userId)`(기대 로스터 전원 다운 시 1회) → `S_DungeonFailed`(1821) 방 브로드캐스트(보상 없음→GameServer 통지 X) → 클라 `DungeonFailedPacketHandler`→`MarkDungeonFailed`/`OnDungeonFailed`→`InGameState.IsDungeonFailed`→`GameHud` `DungeonFailed` 패널→ReturnToLobby. **클리어/실패 상호 배타** = `Room._outcome`(0/1/2) `Interlocked.CompareExchange` 단일 terminal claim. 테스트: SocketServer Room 6 + 클라 EditMode 4 + E2E(클리어 RewardExp·전원다운 실패) 2.
- **검증**: SocketServer.Tests **47/47**(`DungeonClearTests` 4: 전멸 1회·생존 시 false·미스폰 false·발행 참가자/MapId) + 서버 빌드 0오류 + Unity 컴파일 0오류. **남음**: A 트랙 E2E(2클라 처치→양쪽 `S_DungeonClear`), 결과 패널 아트(GameHud 프리팹, 사람).

### 2.8 컨슈머 복원력 중앙화 (일시적 Redis 오류에 안 죽는 BackgroundService)
- **무엇/왜**: 일시적 인프라 오류(Redis `LOADING`·연결끊김)에 BackgroundService 스트림 컨슈머가 outer `catch`로 루프를 빠져나가 **영구히 죽던 버그**(`.NET BackgroundService`는 `ExecuteAsync` 리턴 시 부활 안 함). 실사례: 컨테이너 재시작 직후 Redis 로딩 중 `GameStartRequestedConsumer`가 죽어 방 생성·`GameSessionReady` 발행이 멈춤 → 클라가 `GameSessionEvent` 대신 `UpdateEvent`만 받아 던전 입장 실패.
- **위치**: `Shared/Shared.Infrastructure/MessageQueue/ResilientStreamConsumer.cs` — `RunAsync(name, readStream, handleMessage, logger, ct, baseDelay?, maxDelay?)`. 3분류: 취소→정상종료 / 스트림 읽기 실패→지수백오프(+지터) 재시도(**안 죽음**) / 메시지 핸들러 실패(poison)→그 메시지만 skip(스트림 유지). 백오프 지연 주입 가능(테스트 단축).
- **이관**: `GameStartRequestedConsumer`(SocketServer)·`GameSessionReadyConsumer`·`RoomLifecycleConsumer`(GameServer) → 각 `ExecuteAsync` 한 줄 위임 + `ProcessAsync`(비즈니스 로직만). 큐는 `Shared.Infrastructure.MessageQueue.IMessageQueue<T>.DequeueAllAsync` 공용.
- **설계 의도**: 컨슈머마다 제각각 try/catch(일관성 X, 1개만 고쳐짐) → 복원력을 한 곳에 모아 신규 컨슈머도 자동 안전. 예방(readiness gate)은 startup 노이즈만 줄이고 Redis는 런타임에도 재시작하므로 **복원력이 본질**.
- **검증**: SocketServer.Tests **41/41**(복원력 3: 재시도·poison·취소) + 양 서버 리빌드·재배포, 컨슈머 3개 `consumer started` 로그 확인. plan.md §9.10.

### 2.7 CA-3 BasicAttack end-to-end (서버 권위 판정 + 클라 송신/연출)
- **무엇**: 공격을 입력→서버 권위 적중→데미지→클라 연출까지 완결. **데미지는 서버만이 진실**(로컬 이중 적용 제거).
- **서버 판정**(`SocketServer/PacketHandler/Handler/CombatHandler.cs`): `C_Attack` 수신 → `SkillCatalog.Get("basic_swing")` → `Room.GetAllPlayerStates()`의 시전자 위치/yaw로 `HitboxMath.Overlaps` 적중 재계산(순수 `SelectHitTargets`, 자기 제외) → 적중마다 `OnHitEffectIds`를 `S_ApplyEffect`로 방 브로드캐스트(권위 `Room.NextEffectInstanceId()`+StartTick). 위치는 `MovementHandler`가 `Room.UpdatePlayerState`로 갱신한 값. SocketServer.Tests 15/15.
- **클라 송신**: `PlayerCharacterAgent.HandleAttackInput`(`ConsumeAttackPressed`→히트리셋+공격애니)이 `OnAttackPerformed` 이벤트 발행 → 던전 전용 `CombatSyncSender`(`Gameplay/Character/`, `[Inject] ISocketSession`)가 구독→`SendAsync(new C_Attack{SkillId=0})`(Joined 가드). `ISocketSession.SendAsync(Packet, ct)`(SendMoveAsync 미러). `CharacterSpawner.AttachCombatSyncSender`(Joined일 때만 AddComponent+Inject — Move 핫스팟, 추가만).
- **로컬 데미지 제거 + 죽은 ability 클러스터 정리(완료)**: 데미지가 서버 권위로 완전 이관되어, 미부착·미사용이던 로컬 GAS *ability* 경로를 통째로 삭제. 제거 = `CharacterHitEventReceiver`·`HitDetector`(+프리팹 `AttackPoint` 자식)·`BasicAttackAbility`·`Ability`(base)·`AbilityActivationContext`·`BasicAttackAbilityTests` + ASC의 ability 멤버(`_abilities`/`Abilities`/`GrantAbility`/`TryActivateAbility`×2) + `PlayerCharacterAgent._hitEventReceiver`. **GAS *effect* 시스템(ASC.ApplyEffect·GameplayEffect·Attribute·버프)은 유지**(HUD·서버 동기화의 실사용 경로). 포트폴리오 GAS 쇼케이스 목록(Attribute/GameplayEffect/ASC/버프)에 ability 개념은 없어 서사 손실 0. (EditMode 131→128, ability 테스트 3개만 감소.)
- **HitStop**(`Gameplay/Character/HitStopController.cs` → `PlayerCharacter.prefab` 루트): per-actor `Animator.speed=0`(전역 `Time.timeScale` 금지). 자신 HP 감소(`ASC.OnAttributeChanged`) 자동 트리거 + 외부 `Begin()`. 즉 서버 데미지(`S_ApplyEffect`→로컬 ASC HP↓) 도착 시 그 캐릭터만 멈칫. unscaledTime 복원.
- **검증**: 클라 EditMode **131/131** + 서버 빌드 0 에러 + **E2E `SocketE2ETests` 8/8**(combat: 호스트 정면 1유닛 게스트 공격→게스트 `S_ApplyEffect{basic_attack_dmg}` 수신).
- **남음(정밀화)**: 공유 시계(StartTick 정밀 만료)·클라 예측/정정·**원격 캐릭터 ASC 라우팅**(현재 `EffectReceiver`는 로컬 대상만 → 원격 피격자 HitStop 미발동)·SkillId→Timeline 매핑·active-window 타이밍. JSON 로더/저작툴=CA-5.

### 2.6b Shared.Gameplay 폴더 정리 (GAS 구조 ⓐ, 2026-06-11)
- **무엇**: 플랫 11파일 → 개념 폴더 `{Attributes,Effects,Abilities,Combat,Loot}/`로 분산 + `Enums.cs`를 `Attributes/AttributeEnums.cs`+`Effects/EffectEnums.cs`로 분리. **`git mv`(이력 보존), ns(`Script.System.GamePlayAbilitySystem`) 무변경** → 클라 DLL 머지·서버 참조 무영향(SDK glob이라 .csproj 수정 0).
- **왜**: 플랫 나열이 찾기 어려움. ns를 폴더에 맞춰 바꾸면 클라 DLL 머지가 깨지므로 **폴더만**. 설계 전체 = [gas-architecture.md](gas-architecture.md).
- **검증**: Shared.Gameplay 빌드 0오류 + 단위 **22/22** + SocketServer 빌드 0오류(동작 보존, 테스트 신규 불필요 — 이동만).

**ⓑ 카탈로그 단일화 (서버 위임, 2026-06-11)**
- **무엇**: `GameplayEffectDefinition.cs`+`GameplayEffectCatalog.cs`를 Client→`Shared.Gameplay/Effects/`로 이사(순수, ns 동일). 서버 `CombatEffectCatalog`를 **자체 Dictionary 제거 → Shared `GameplayEffectCatalog` 위임**(`_shared.Get(id)?.Modifiers`)로 축소 = **effect 수치 단일소스**(문제① 해소). 클라 `GameplayEffectDefinition.cs.meta`/`Catalog.cs.meta`는 gitignore라 디스크 rm.
- **DLL 재배치**: `Shared.Gameplay.dll`(11→13.8KB, Definition/Catalog 포함) → `Client/Assets/Plugins/Shared.Gameplay/` 재복사(공개 API 변경이라 필수).
- **검증(서버)**: Shared 22/22 + **SocketServer 74/74**(신규 `CombatEffectCatalog는_Shared_단일소스를_위임한다` 회귀가드 포함, 기존 MonsterDamage 보존) + DLL 빌드 0오류. **클라 컴파일은 Unity 미실행(unity-mcp 미연결)으로 CLI 검증 불가 — Unity 인에디터 재컴파일 필요**(same-ns 이동이라 안전 예상). ※ Unity 생성 `Game.*.csproj`는 stale(삭제파일 잔존)이라 dotnet 빌드 검증 부적합.
- **남음(미착수)**: 결정2(클라 `GameplayEffect`/`AbilitySystemUtils`는 테스트 헬퍼 — 마이그레이션 후 삭제, 사용자 미결) · ⓔ 2.5.1 사망=`State.Dead` 태그.

**ⓒ GameplayTag 인프라 (2026-06-11)**
- **무엇**: Shared `Tags/GameplayTag.cs`(readonly struct, 값 동등성, 문자열 implicit, 정확일치)+`Tags/GameplayTagContainer.cs`(HashSet 기반 Add/Remove/HasTag/HasAny) 신설. `GameplayEffectDefinition`에 `GrantedTags[]`(선택 파라미터, 기본 빈목록) 추가 — 활성 Effect가 부여하는 상태태그(예: 스턴=State.Stunned). 클라 `AbilitySystemComponent`에 `_tags` 컨테이너 + `AddTag/RemoveTag/HasTag` — HasTag=직접태그 ∪ 활성Effect.GrantedTags(동적 합산). 사망 `State.Dead`는 Effect 없이 `ASC.AddTag` 직접.
- **왜**: 사망(2.5.1)·CC(2.6.2)·상태 Cue의 공통 인프라. 계층 부모 매칭은 YAGNI(정확일치만, 후속). GrantedTags는 데이터+ASC 동적합산만(Effect→Container 동기화 로직 불필요 — HasTag가 _active 스캔).
- **검증**: Shared.Gameplay.Tests **29/29**(태그 7 신규: 동등성·컨테이너·HasAny·GrantedTags 기본/지정) + SocketServer 빌드 0오류(GrantedTags 선택파라미터 후방호환) + DLL 재복사(15.3KB). **클라 ASC 글루는 Unity 인에디터 검증 필요**(small 추가, same-ns DLL 타입). 클라 컴파일 0오류 확인(사용자). ASC 태그 EditMode 테스트는 ⓔ 2.5.1 게이트 합류 시 추가.

**ⓓ 서버 발동 게이트 = 권위 쿨다운 (치팅 차단, 2026-06-11)**
- **무엇**: `C_Attack` 연사=폭딜 치팅을 서버에서 차단. Shared 순수 `SkillTimelineMath.CooldownElapsed(cooldownMs,lastCastMs,nowMs)` + 서버 `PlayerState.TryBeginSkill(skillId,cooldownMs,nowMs)`(스킬별 `Dictionary<int,long>` 마지막 발동시각, 쿨다운 경과 시 기록+true / 아니면 false) + `CombatHandler.HandleAttack` 진입부 게이트(거부 시 `return` → 데미지 0, 적중판정도 안 함). basic_swing 쿨다운=400ms.
- **왜**: 기존엔 서버가 cadence 미추적 → 매 `C_Attack`마다 hitbox 평가→데미지(authority-model ① 치팅 구멍). 쿨다운 데이터는 `SkillTimeline.CooldownMs`에 이미 존재 → 서버가 읽어 게이트만. active-window 정밀 시뮬은 YAGNI(쿨다운으로 1차 차단). 연출은 클라 즉발 유지(거부돼도 피격 Effect 없음→자연 정리).
- **위치**: `Shared.Gameplay/Abilities/SkillTimelineMath.cs`(CooldownElapsed) · `SocketServer/Player/PlayerState.cs`(TryBeginSkill) · `SocketServer/PacketHandler/Handler/CombatHandler.cs`(게이트 배선, 0단계).
- **검증**: Shared **30/30**(CooldownElapsed 경계 1) + SocketServer **78/78**(SkillCooldownGate 4: 첫발동·연사거부·쿨경과재발동·스킬별독립) + DLL 동기화. **서버 단독**(클라/패킷 무변경). ※E2E 검증·Docker 리빌드는 별도(연사 거부 E2E는 후속).
- **남음**: 위치도 lite권위(C_Move 릴레이값, 텔레포트핵)는 별개 부채. active-window 정밀(서버 tick)도 후속.

**ⓔ-1 로컬 사망 게이트 = 2.5.1 착수 (2026-06-11)**
- **무엇**: HP≤0(클라 결정론) → 로컬 플레이어 다운-잠금. Shared `Tags/GameplayTags.cs`(상수 `Dead="State.Dead"`, 매직스트링 방지). 클라 `PlayerCharacterAgent`: `AbilitySystem.OnAttributeChanged` 구독 → Health≤0 시 `ASC.AddTag(State.Dead)`(1회) → `Update`에서 `IsDead`면 `return`(Action 입력 무시 + `base.Update()` 미호출로 Locomotion FSM 정지 = 이동/공격/상호작용 한 번에 게이트). OnDestroy 구독해제.
- **왜**: 사망=FSM 상태 아님(두 축 규칙) → GAS 태그로(ⓒ 인프라 위). 던전=다운-잠금(씬 복귀/2.5.2 부활 전까지 유지), Main 타이머 리스폰은 별개. base.Update 미호출=가장 간단한 3축 동시 게이트(가사 시 freeze, 다운포즈는 후속).
- **위치**: `Shared.Gameplay/Tags/GameplayTags.cs` · `Client/.../Gameplay/Character/Agent/PlayerCharacterAgent.cs`.
- **검증**: Shared 30/30 + SocketServer 빌드 0오류 + DLL 재복사. **클라 컴파일 0오류(사용자)**. 테스트: ① EditMode `AbilitySystemTagTests` 4종(직접태그·무효·GrantedTags 합산·독립) — **사용자 실행 그린**. ② PlayMode `PlayerDeathGateTests` 2종(생존 시 공격 발동 / HP0→State.Dead+공격 억제) — `TestableAgent`로 Start(FSM) 스킵하고 Update 직접 구동(프레임 미yield로 컴포넌트 부작용 차단), `FakeInput` 컴포넌트. **사용자 Test Runner 실행 그린**(2/2). → **ⓔ-1 로컬 사망 게이트 EditMode 4 + PlayMode 2 자동검증 완결.** 
- **잔여(2.5.1)**: 신규 패킷 `S_PlayerDead{userId}`(Union 1823, 원격 다운 가시성·공개계약 승인 필요) · 다운 포즈/애니(Animator, 사용자) · Main 로컬 타이머 리스폰. 던전 내 부활=2.5.2.

**ⓔ-2 S_PlayerDead 원격 다운 가시성 (2026-06-11)**
- **무엇**: 서버 `DungeonLifecycleHandler.HandlePlayerDead`가 `C_PlayerDead` 수신 시 ① `S_PlayerDead{UserId}`(신규, **Union 1823**) 방 브로드캐스트(개별 다운 가시성) ② 기존 `TryMarkFailed`(전원다운→S_DungeonFailed). 클라: ClientCodegen 미러 재생성 → `PlayerDeadPacketHandler`(SocketApiClient.Install 등록) → `ISocketPacketState.OnPlayerDead`/`NotifyPlayerDead` → `CharacterSpawner.HandlePlayerDead`.
- **다운 처리(현재=로그+Destroy, 다운포즈 후속)**: **원격** 사망 → `DespawnRemote`(다른 플레이어가 다운을 봄=핵심). **로컬** 사망 → **destroy 안 함**(이미 ⓔ-1 State.Dead 게이트로 입력 정지 + 자기 GO destroy 시 `CharacterCameraFollow.CinemachineCameraTarget`/HUD NRE) → 로그만.
- **위치**: `Shared.Packet/Packets/Domains/DungeonPackets.cs`(S_PlayerDead) · `Packet.cs`(Union 1823) · `SocketServer/PacketHandler/Handler/DungeonLifecycleHandler.cs` · 클라 `Network/Socket/SocketApiClient.cs`(iface+impl+등록) · `Handler/Contents/PlayerDeadPacketHandler.cs` · `Gameplay/Character/CharacterSpawner.cs`.
- **검증**: SocketServer **80/80**(`DungeonPacketSerializationTests` 2 신규: S_PlayerDead 라운드트립·Union 복원) + socketserver Docker 리빌드·재배포. **클라 컴파일/원격 가시성 플레이(MPPM)는 Unity 검증 필요**. ※브로드캐스트 자체는 소켓 fire-and-forget이라 단위 캡처 불가 → 직렬화+E2E(후속)로 커버.
- **플레이 검증(2026-06-11)**: MPPM 2-창 — HP 10→5→0(`EffectReceiver` 진단로그)→`C_PlayerDead`→`S_PlayerDead(UserId)`→**다른 창에서 그 캐릭터 디스폰** 전 경로 확인. 로컬 캐릭터는 의도대로 유지(게이트 정지).
- **버그 픽스(플레이 중 발견)**: 다운된 플레이어를 몬스터가 **계속 타깃·공격**(HP0 후에도 `monster_attack_dmg`) → `Room.TickMonsters`가 타깃 후보에서 **`_downed` 플레이어 제외**(끊김 `DisconnectedAtMs` 제외와 동일 자리, `_downed` 스냅샷 후 필터). SocketServer **81/81**(`다운된_플레이어는_몬스터_공격_대상에서_제외된다` 신규) + Docker 리빌드. ※`EffectReceiver`에 임시 진단로그(효과수신/적용HP/비-내대상 Δ) — 검증 후 제거 예정.
- **남음(2.5.1)**: 다운 포즈/애니(로그+Destroy 대체) · Main 로컬 타이머 리스폰. 던전 내 부활=2.5.2.

**플레이어 HP 서버 권위 — 증분 1: 데미지+사망감지 (2026-06-11)**
- **규칙 선행**: authority-model **§0 권위 결정 규칙**(기본=서버, 코옵/PvP 공유상태=서버, "문서에 적힘≠결정됨", "할 수 있다≠소유해야") + **§4 플레이어 HP 서버 권위 승격 결정**(기존 클라 결정론은 사용자 미승인 가정·부채였음 → 불사 핵). §3·§7 표 갱신.
- **무엇**: `PlayerState.Hp`/`MaxHp`(입장 시 `Room.DefaultMaxHp=100`) + `Room.ApplyPlayerEffect(userId, mods)`(`GameplayEffectMath.Aggregate`로 데미지/회복 공용, 클라와 동일함수→값 일치) + `Room.MarkPlayerDowned`(TryMarkFailed 일반화, NewlyDowned/FailClaimed로 중복발화 dedup) + **`Room.TickMonsters`가 monster_attack_dmg 발행 시 서버 HP 누적→HP≤0 직접 감지→`S_PlayerDead`(+전원다운 `S_DungeonFailed`)**. 락순서 `_monsters→_playerStates→_downed`(역순 없음).
- **C_PlayerDead 격하**: 클라 `InGameModel`이 더 이상 송신 안 함(서버가 감지). 서버 `DungeonLifecycleHandler`는 보조/하위호환 — `MarkPlayerDowned` NewlyDowned로 dedup(서버 틱이 먼저면 무시). E2E는 수동 C_PlayerDead 송신이라 무관.
- **불사 핵 차단(부분)**: 서버 HP=damage-only 누적 → "데미지 무시"·"가짜 회복" 둘 다 서버 HP가 떨어져 사망 감지. **단 정상 회복도 서버 미적용 → false-kill(증분 2가 복구)**.
- **위치**: `SocketServer/Player/PlayerState.cs`(Hp/MaxHp/IsDowned) · `Room/Room.cs`(DefaultMaxHp·ApplyPlayerEffect·MarkPlayerDowned·TickMonsters) · `PacketHandler/Handler/DungeonLifecycleHandler.cs`(dedup) · 클라 `Presentation/InGame/InGameModel.cs`(송신 제거).
- **검증**: SocketServer **85/85**(`PlayerHpServerAuthorityTests` 4 신규: 입장 만피·데미지누적+다운·회복+클램프·**C_PlayerDead 없이 서버 사망감지→S_PlayerDead**) + 클라 EditMode `InGameDungeonResultRelayTests` 갱신(HP0→C_PlayerDead 미송신). 클라 컴파일/Docker는 증분 2 후 일괄.
**플레이어 HP 서버 권위 — 증분 2: 회복 서버 동기 (2026-06-11)**
- **무엇**: 정상 회복이 서버 HP에 반영되도록 크로스-서버 흐름. `클라 → ConsumeItem gRPC(GameServer 검증·차감) 성공 → GameServer가 PlayerConsumedMessage{UserId, EffectId=itemId} 발행(Redis stream:game:player:consumed) → SocketServer PlayerConsumedConsumer가 userId로 방 조회(GetAssignedRoom) → Room.ApplyPlayerEffect(+heal) + S_ApplyEffect(effectId) 브로드캐스트 → 클라 EffectReceiver 미러`. 회복 효과 = **Shared GameplayEffectCatalog "potion_hp_small"(Health +100)**, effectId==itemId 규칙(별도 카탈로그 불필요).
- **왜**: 차감 검증=GameServer 권위 유지(가짜회복 차단), 회복은 검증 *후* SocketServer 적용(무한힐 불가). 키=userId라 GameServer가 던전 컨텍스트 몰라도 됨(SocketServer가 방 조회, Main은 방 없어 no-op). SocketServer→GameServer 직접 RPC 없음(Redis 단방향).
- **클라 게이트**: 던전은 `ConsumableEffectHandler` **미등록**(DungeonLifetimeScope) → 로컬 회복 적용 안 함(서버 S_ApplyEffect로 미러, 이중적용 방지). Main은 유지(클라 권위, §2 솔로).
- **위치**: `Shared.Infrastructure/Messages/PlayerConsumedMessage.cs` · `Shared.Infrastructure/MessageQueue/PlayerConsumedMessageQueue.cs`(양측 공용 단일 큐) · `SocketServer/MessageQueue/Consumer/PlayerConsumedConsumer.cs`(+Program.cs 등록) · GameServer `API/Services/InventoryGrpcService.cs`(ConsumeItem 성공 발행)·`InventoryInstaller.cs`(큐 등록) · Shared `GameplayEffectCatalog`(potion_hp_small) · 클라 `DungeonLifetimeScope`(핸들러 제거).
- **검증**: GameServer **252/252**(`InventoryGrpcServiceTests` +2: 소비성공→PlayerConsumed 발행·EffectId==itemId / 실패시 미발행) + SocketServer **85/85** + 솔루션 빌드 0오류 + DLL 재복사 + 양 서버 Docker 리빌드·재배포. **클라 컴파일·E2E(소비→서버회복→HP복구)·MPPM 플레이는 Unity 검증 필요**(unity-mcp 미연결).
- **E2E(작성, Unity 실행 대기)**: `SocketE2ETests` 2종 — ① `던전에서_포션_소비하면_서버가_회복_S_ApplyEffect를_브로드캐스트`(빠름: GrantItem→ConsumeItem gRPC→S_ApplyEffect(potion_hp_small) 관측 = 크로스서버 회복 전경로) ② `몬스터에게_죽으면_서버가_C_PlayerDead_없이_S_PlayerDead`(**~2초**: test_arena 맵의 fixture 몬스터 `test_brute`가 호스트를 빠르게 죽임 → 서버 HP0 직접감지→S_PlayerDead = 불사핵 차단 직접증명). 양 서버 Docker 리빌드·재배포.
- **맵 선택 슬림 배선(4.3 부분 상환, 2026-06-11)**: `StartRoomRequest.map_id`(proto, optional) → `DungeonLobbyGrpcService.StartRoom`→`StartGameAsync(...,mapId,...)`→`GameStartRequestedMessage.MapId`(비우면 `MapIds.Default`). DungeonRoom 엔티티/Redis **무변경**(StartGame 시점에만 맵 지정 — 정식 던전선택 UI 생기면 방 생성으로 승격). 테스트 fixture: `MonsterCatalog.test_brute`(사거리·aggro 무한, 쿨다운 50ms, 고HP) + `spawn-layouts.json`(서버+클라) `test_arena` 맵. 클라 proto 재생성(StartRoomRequest.MapId). GameServer 252/252 + SocketServer 85/85(시그니처 변경 후방호환).
- **잔여 부채**: 회복 수치 이중정의 → **단일소스화 진행(아래 §2.6c)**. ~~EffectReceiver 진단로그~~(제거 완료). E2E 2종 Unity 실행 **그린 확인(사용자, 2026-06-11)**.

### 2.6c 소모품 회복 수치 단일소스 — SO 저작 → bake → 서버 (2026-06-13, 2.5.1 잔여)

> **교리 정본 = [gas-architecture.md §2.5](gas-architecture.md)** (SO 저작/Shared 검증). 본 절은 그 교리의 소모품 구현 로그. 전투 effect 위임은 §2.6b ⓑ.

**문제(이중정의의 경위)** — 같은 회복 수치(potion_hp_small = Health +100)가 두 곳에 손으로 유지되어 드리프트 위험:
1. **3.8 소모품(2026-06-10)**: 회복=클라 권위로 설계 → 클라 `ConsumableCatalog` SO에 수치 저작. 서버 무관여.
2. **2.5.1 증분2(2026-06-11)**: 던전 플레이어 HP를 서버 권위로 승격 → 서버도 회복 수치가 필요 → 단일소스 파이프라인(SO→bake)을 까는 대신 `GameplayEffectCatalog` 코드 시드에 `potion_hp_small`을 **손으로 복사**(주석 "클라와 정렬")하고 부채로 미룸.

**설계 원칙 (DropTable 컨벤션과 1:1 동일 — 기획자는 JSON을 만지지 않는다)**:
```
[기획자] Unity Inspector 에서만 편집
   └─ ConsumableCatalog.asset (ScriptableObject)   ◀── 유일 저작면(진실원)
        ├─(런타임 직접 읽기)──▶ 클라: ConsumableEffectHandler(Main 적용) / ConsumableCatalogSeeder(던전 EffectReceiver 미러용 등록)
        └─[Tools/Consumables/Export] (에디터 메뉴 1버튼)
              └─ bake ▶ consumable-effects.json   ◀── 기계 산출물(기획자 안 봄·git에만)
                          └─(Shared.Infrastructure 임베디드)─▶ 서버: ConsumableEffectCatalog → CombatEffectCatalog static ctor 가 Register 로 흡수 → effectId 조회
```
- **편집면 = `ConsumableCatalog.asset` Inspector 한 곳.** JSON 은 서버가 UnityEngine(SO)을 못 읽어서 익스포터가 자동 생성하는 **서버 전용 산출물** — 기획자 비노출.
- effectId == itemId 규칙으로 클라(SO)·서버(bake JSON)가 동일 수치. Shared `GameplayEffectCatalog` 코드 시드에서 `potion_hp_small` 제거(소모품은 더 이상 코드 시드 아님) = 이것이 그 카탈로그가 예고한 "2단계 JSON 로더"의 실현.
- 전투 효과(basic_attack_dmg/monster_attack_dmg)는 **서버 게임밸런스 권위라 코드 시드 유지** — 소모품(기획 콘텐츠)만 SO 저작. 두 출처를 effectId 단일 조회로 합류.

**위치**: 클라 `Presentation/Inventory/ConsumableCatalog.cs`(SO, 저작면) · 신규 Editor `ConsumableEffectExporter`(SO→JSON, DropTableExporter 자매) · 신규 클라 `ConsumableCatalogSeeder`(SO→DI `GameplayEffectCatalog` 등록) · 신규 `Shared.Infrastructure/Consumables/{ConsumableEffectCatalog.cs, consumable-effects.json(임베디드)}` · `SocketServer/Combat/CombatEffectCatalog.cs`(static ctor 흡수) · `Shared.Gameplay/Effects/GameplayEffectCatalog.cs`(potion 시드 제거).
- **상태(2026-06-13)**: ✅ 코드 완료. 서버 = `ConsumableEffectCatalog`(임베디드 `consumable-effects.json`) + `CombatEffectCatalog` static ctor 흡수 + `GameplayEffectCatalog` potion 시드 제거(빌드 0오류). 클라 = `ConsumableCatalogSeeder`(SO→DI `GameplayEffectCatalog`, Dungeon+Main 등록 — **코드시드 제거로 깨진 던전 회복 미러 회귀 복구**) + Editor `ConsumableEffectExporter`(SO→JSON). 클라 컴파일·플레이는 Unity 검증 대기.

### 2.6d Main 획득 B-lite 서버 검증 — 무한 파밍 핵 차단 (2026-06-13)
- **정본 = [main-spawn-claim.md](main-spawn-claim.md) / authority-model §4b.** 무엇/왜/시나리오·흐름·증분은 거기. 여기는 코드 위치 요약.
- **핵**: Main 획득이 클라 권위(`GrantItem(itemId,qty)` 무검증) → 무한 스폰·무한 GrantItem = 만렙 핵. **결정 = B-lite**(서버가 map 스폰 데이터 보유 → 클라는 슬롯만 지목, 서버가 검증·roll·grant).
- **서버**(완료·검증, 빌드0+단위14 그린): 스키마 `MonsterSpawnDef`/`SpawnLayoutTable`(`SlotId`/`RespawnCooldownMs` additive)+`spawn-layouts.json` `main_field_01` · `IMainSpawnClaimService`/`MainSpawnClaimService`(슬롯검증+쿨다운+`DropTableCatalog.Roll`+`GrantItemAsync`) · `IClaimCooldownStore`/`RedisClaimCooldownStore`(`SET NX PX`=원자 쿨다운) · gRPC `ClaimKill` 추가/`GrantItem` 제거(`inventory.proto`·`InventoryGrpcService`·Generated·ClientCodegen) · `InventoryInstaller` DI. 테스트 `MainSpawnClaimServiceTests`(슬롯/없는맵/유효지급/쿨다운거부) + `InventoryGrpcServiceTests`(ClaimKill 매핑·미인증).
- **클라**(코드 완료, Unity 검증 대기): `SpawnLayoutProvider`/`MapSpawnLayout`+`MonsterSpawn`(monsters 파싱) · `LocalMonster.Configure(slotId,mapId)` · `MainMonsterSpawner`(슬롯 스폰+클레임 드랍, **클라 roll 제거**) · `LocalGroundItem`→`ClaimKillAsync` · `MainLifetimeScope`(`MainMonsterSettings{prefab,mapId,groundItem}`). E2E `MainLootE2ETests`(정상/쿨다운차단/위조슬롯) · `SocketE2ETests`(포션 시드 ClaimKill) · `InventoryE2ETests`(GrantItem 제거).
- **9b~9d 영향**: LocalMonster 스폰·렌더 유지, **roll이 클라→서버 / GrantItem→ClaimKill** 교체. loot-drop §1.4 구결정 폐기(배너).
- **검증(2026-06-13)**: ✅ 서버 솔루션 374 그린(Docker 리빌드 후) + Unity 컴파일 + **PlayMode E2E 그린**(`MainLootE2ETests` 3·`SocketE2ETests` 회복, 사용자) + **플레이: 처치→오브→E 줍기→ClaimKill→potion 지급(보유 누적) 확인**.
- **플레이 중 보완(2026-06-13)**: 처치 후 슬롯이 영구히 비는 문제 → `MainMonsterSpawner.ScheduleRespawn`(`UniTask.Delay(RespawnCooldownMs)` 후 `Spawn(slot)` 재스폰, `_cts` Dispose 취소). 서버 claim 쿨다운과 동일 값 → 재스폰 시점에 보상도 다시 가능.

### 2.6e Main 타이머 리스폰 + 다운 포즈 (2.5.1 마무리, 2026-06-13)
- **다운 포즈(던전·Main 공통, 로컬)**: `AnimationTriggerType.Dead` + `CharacterAgentAnimations.m_animationDeathTrigger` 신설. `PlayerCharacterAgent.OnAttributeChanged` 가 HP≤0 시 `AddTag(Dead)` + `SetTrigger(Dead)` + **다운 로그**. **Animator "Dead" 클립 배선은 의도적 보류**(서버 우선, 클라 발전 시) → 지금은 **로그로 관찰**. `SetTrigger/ResetTrigger` 는 파라미터명 빈 값(미배선)이면 조용히 스킵(Animator 경고 방지).
- **부활**: `PlayerCharacterAgent.Revive(spawnPos)` — `RemoveTag(Dead)` + Health `SetCurrent(Max)` + `ResetTrigger(Dead)` + CharacterController-safe 텔레포트. 게이트(`IsDead`)가 풀려 이동·Action 재개.
- **Main 타이머 리스폰**: `LocalRespawnController : ITickable` — **MainLifetimeScope 에만 등록**(던전 미등록 = 다운잠금 유지, 의도된 비대칭). Dead 상승엣지 감지 → `RespawnDelaySec`(3s) 후 `Revive`(스폰 = 맵 레이아웃 첫 Point/원점). death-respawn-2-5-1-direction.
- **Main 사망 트리거**: `LocalMonster` 근접 공격 추가 — `attackRange` 내에서 `attackCooldownSec` 마다 플레이어 ASC 에 즉발 `local_monster_attack`(Health -dmg, 클라 로컬 권위). 다운 플레이어는 공격 안 함. (없으면 Main 에서 죽을 수단이 없어 리스폰 잠복.)
- **위치**: `Gameplay/Character/{Agent/PlayerCharacterAgent, LocalRespawnController, LocalMonster, CharacterAgentAnimations}.cs` · `VContainer/.../MainLifetimeScope.cs`.
- **남음**: Animator "Dead" 배선(아트)·Unity 컴파일·플레이 검증.

### 2.6 CA-2 SkillTimeline 스키마 + 서버 권위 설계 (Shared.Gameplay)
- **무엇**: `Shared.Gameplay`(ns `Script.System.GamePlayAbilitySystem`)에 스킬 결정론 코어 — `SkillTimeline`(Id·Startup/Active/Recovery/Cooldown ms·`HitboxSpec`·`OnHitEffectIds[]`), `HitboxSpec`(Box/Sphere, `System.Numerics.Vector3`), `ESkillPhase`/`EHitboxShape`, `SkillTimelineMath`(PhaseAt/IsActive), `HitboxMath.Overlaps`(yaw로 월드→로컬 변환+박스/구 겹침, 엔진 비의존), `SkillCatalog`(코드 시드).
- **서버 권위 모델(핵심)**: 스키마에 **cue/애니/VFX 없음**(클라 전용) → 서버가 **데이터(active window+hitbox)만으로** 판정. 흐름: 클라 예측 → `C_ActivateSkill`(skillId·tick·pos·yaw) → 서버가 같은 `SkillTimeline`+`HitboxMath`로 적중 재계산(권위) → `OnHitEffectIds`→GameplayEffect → **EF-2d `S_ApplyEffect` 재사용** 브로드캐스트(데미지=Instant/디버프=Duration) → 클라 정정. 판정모델 A(서버 재계산)/B(클라 후보+검증)는 CA-3 확정. fixed-point 아님.
- **공유**: 서버 ProjectReference, 클라 `Plugins/Shared.Gameplay.dll`(재빌드→복사). xUnit **17/17**.
- **남음(CA-3)**: `C_ActivateSkill` 패킷·서버 판정+emit·클라 예측/정정·`basic_attack_dmg`를 GameplayEffectCatalog에 추가. JSON 로더/저작툴=CA-5.

### 2.5 CA-1 두 축 분리 (Action을 Locomotion FSM에서 제거)
- **무엇**: 캐릭터를 Locomotion FSM(`Ground/Jump/Fall/Land`) + Action(공격/상호작용, FSM 아님) 두 축으로 분리. `AttackState`·`InteractState`+전이 4개·`StateKind.{Attack,Interact}` 제거. Factory/Builder/`CharacterStateContext`(HitEventReceiver·InteractionDetector) 정리. SO(`PlayerStateConfig`·`CharacterStateConfig`)에서 Attack/Interact StateDefinition 삭제. 규칙: `.claude/rules/unity-gameplay-state.md` 갱신.
- **발동(로컬)**: `PlayerCharacterAgent`가 입력 폴링 — `HandleAttackInput`(`ConsumeAttackPressed`→히트리셋+공격애니), `HandleInteractInput`(`ConsumeInteractPressed`→`InteractionDetector.CurrentInteractable.Interact(gameObject)`+상호작용애니). `CharacterAgent`(FSM 구동자)는 무접촉 → StateKind 제네릭이라 config/factory/builder만 수정. Move 핫스팟 충돌 0.
- **데미지(GAS)**: CA-1 시점엔 로컬 GAS(`CharacterHitEventReceiver`→`HitDetector`→`BasicAttackAbility`→GE) 유지였으나 **CA-3에서 서버 권위로 이관 + 로컬 ability 클러스터 삭제**(§2.7).
- **상호작용 경로**: 던전 실작동 = `Game.Gameplay.Character`(detector+`IInteractable.Interact(GameObject interactor)`, instigator 보유→아이템 친화). `Game.Gameplay.Input.InteractionSystem`(리치·라우터)은 **아웃게임 등록·인게임 휴면**(detector 프리팹 미배선) → 통합 대상 아님(후속 정리). 아이템/문/NPC=`IInteractable` 구현, 소비아이템만 interactable이 `ASC.ApplyEffect` 호출.
- **검증**: EditMode **129/129**(`LocomotionStateMachineTests` 회귀 추가). **남음**: `StateDefinition.InvokeDelay`/`LocomotionSettings.InteractReturnDelay` 死코드 정리(선택), `InteractionSystem` 중복 정리, 스윙 GAS化(CA-3).

### 2.4 GameplayEffect 버프/디버프 + HUD 연동 (EF-1, 클라 단독·sync-ready)
- **무엇**: 지속형 버프/디버프를 가역적으로 처리하고 HUD에 표시. 기획: [effect-system.md](effect-system.md).
- **Attribute 두 종류**(`EAttributeKind`): Resource(HP/MP)=즉발로 Current 영구 변경(기존 `ApplyModifier` 유지), Stat(AttackPower/Defense/MoveSpeed)=Current는 **파생**(`GameplayAttribute.SetCurrent` ← `GameplayEffectMath.Aggregate(Base, 활성 modifier, Max)`, 정수). 버프 만료 시 재계산으로 자동 복원.
- **모델**: `GameplayEffectDefinition`(정적·Sprite 없음·string id) / `ActiveGameplayEffect`(런타임·StartMs) / `GameplayEffectCatalog`(코드 시드, 2단계 JSON 교체) / `ActiveEffectSnapshot`(표시용). 위치: `Client/Assets/Script/System/GameplayAbilitySystem/{Attribute,Effects}/`.
- **ASC**: `ApplyEffect`(Instant→Resource 즉발 / Duration·Infinite→active 등록+스택정책) / `RemoveEffect` / `Tick`(만료 제거) / `RecalculateStats` / `GetActiveEffectSnapshots`. 이벤트 `OnActiveEffectsChanged`(추가/제거/만료만 — 남은시간은 View 로컬 카운트다운).
- **표시(Presentation, Sprite는 여기서만)**: `EffectIconCatalog`(SO, Category→Sprite + buff/debuff 색) + `BuffView` DTO. `InGameModel`이 snapshot→BuffView 변환(polarity = `PolarityOverride ?? modifier 부호합`), `InGameState.Buffs`로 발행. `GameHud`가 `buffSlotContainer`에 `BattleEffectSlot` 풀 렌더(`buffSlotPrefab` 인스턴스화, `BattleEffectSlot.Bind`+로컬 카운트다운).
- **레이어**: GUI는 System 타입 비노출(BuffView만 봄). 동기화 대상은 EffectId뿐 — 서버는 Sprite 모름.
- **EF-2(서버 동기화) 진행**: ① `Shared.Gameplay`(netstandard2.1, **ns `Script.System.GamePlayAbilitySystem`**, assembly명 Shared.Gameplay) = 결정론 코어(enums·`GameplayAttributeModifier`·`GameplayEffectMath`·`EffectTiming`). 서버는 ProjectReference, **클라는 `Client/Assets/Plugins/Shared.Gameplay.dll` 단일 소스**(중복 8개 삭제, 동일 ns라 클라 코드 무수정). xUnit 9/9 = 클라 EF-1과 동일 벡터. ② 패킷 `S_ApplyEffect`/`S_RemoveEffect`(Union **1640/1641**, `Shared.Packet`) — StartTick+EffectId만(남은시간 미전송), 클라 미러는 ClientCodegen 생성. ③ **EF-2d 동기화 루프 완성**: 서버 `CombatHandler`(`C_Attack`→대상 디버프 `S_ApplyEffect` 방 브로드캐스트, 권위 `Room.NextEffectInstanceId()`+StartTick) → 클라 `Effect{Apply,Remove}PacketHandler`→`ISocketPacketState`(effect 이벤트)→`EffectReceiver`(Presentation; `AuthSession`로 타겟 라우팅→`ASC.ApplyEffectAuthoritative`, 서버 InstanceId 키). E2E `SocketE2ETests`(A 공격→B `S_ApplyEffect` 수신) 1/1. **남음**: 공유 시계(StartTick 정밀)·클라 예측·원격 ASC 라우팅(현재 로컬만)·실전 combat(CA-3, 현 테스트 디버프/SkillId 무시 대체).
- **DI**: `DungeonLifetimeScope`에 `GameplayEffectCatalog`(Register) + `EffectIconCatalog`(serialized, 미할당 시 빈 인스턴스 폴백) 등록. VContainer는 ctor 기본값 무시 → 둘 다 등록 필수.
- **검증**: EditMode 113/113(EffectSystemTests 가역성·만료·스택·즉발, InGameBuffRelayTests 아이콘/polarity) + PlayMode(GameHudBuffIntegrationTests 슬롯 렌더, HP/MP 회귀).
- **남음**: `EffectIconCatalog` 에셋 생성+아트 Sprite+씬 할당. EF-2 서버 동기화.

### 2.3 결정론적 스폰 (Shared 데이터 + 서버·클라 미러 리졸버)
- **무엇**: 스폰 좌표를 네트워크로 전송하지 않는다. 서버·클라가 **같은 데이터 + 같은 순수 함수**로 각자 계산한다.
  - 데이터: `spawn-layouts.json`(맵별 명시적 스폰 포인트 목록). 정본=서버 `Shared.Infrastructure/Spawn/`(임베디드 리소스), 클라=`Client/Assets/Script/Gameplay/Resources/spawn-layouts.json`(미러 — 패킷 미러 컨벤션과 동일).
  - 리졸버: `SpawnResolver.Resolve(layout, spawnIndex)` 순수 함수(모듈러 순환). 서버 `Shared.Infrastructure.Spawn`, 클라 `Game.Gameplay.Spawn`에 **동일 알고리즘 미러**.
  - 식별자: `MapId`(현재 단일 `dungeon_01`, `MapIds.Default`). `GameStartRequestedMessage.MapId`로 흐르고 `Room.MapId`에 저장. 던전 선택 UI 생기면 여기만 채우면 됨.
- **흐름**: 게임시작 → 서버가 `Resolve`로 `InitPlayerState`(권위) → `S_PlayerJoined{MapId,SpawnIndex,Pos}` 송신. 클라 **로컬**은 (MapId,내 SpawnIndex)로 `Resolve`해 스폰(좌표 신뢰 X, self 스냅샷 대기 후). **원격**은 서버가 보낸 현재 Pos에 스폰(이미 움직였을 수 있어 결정론 계산 안 함) + `RemoteDriver` 보간.
- **로스터 회신**: `RoomJoinLeaveHandler`가 입장자에게 본인+브로드캐스트 외에 **기존 멤버 전원의 `S_PlayerJoined`를 회신**. 없으면 늦게 입장한 플레이어가 먼저 들어온 플레이어를 영영 못 봄. `CharacterSpawner`는 **구독을 초기 스폰보다 먼저** 해 그 사이 패킷 유실 방지(`_remotes.ContainsKey` 중복가드).
- **DI 미러링(Main+Dungeon 공통 스포너)**: `CharacterSpawner`는 Main·Dungeon 양 씬 공용(Main=로컬만, Dungeon=로컬+원격). 생성자 의존 중 `LocalPlayerContext`·`SpawnLayoutProvider`는 **두 LifetimeScope 모두에 등록 필수**(소켓/Auth는 부모 Singleton). Main 스코프에 빠지면 `VContainerException`으로 Main 씬 스폰 전체가 죽음 — 결정론 스폰 도입 시 누락됐다가 복구(2026-06-03). Main은 미연결이라 `SpawnLayoutProvider.Get()`은 호출 안 됨(생성자 충족용).
- **왜**: 기존 서버 하드코딩 `ResolveSpawn` switch는 맵 무관·서버 단독 지식 → 맵별 스폰 불가, 클라가 스폰 레이아웃을 모름. 데이터 단일소스 + 결정론으로 맵 추가=데이터 추가, 좌표 미전송.
- **계약 변경**: `S_PlayerJoined`에 `MapId`/`SpawnIndex` 필드 추가(Union ID 불변, 클라 미러는 ClientCodegen 재생성). `GameStartRequestedMessage.MapId`, `PlayerState.SpawnIndex`, `Room.MapId`, `Room.InitPlayerState(+spawnIndex,+rotY)`.
- **검증**: 서버 `SpawnLayoutTests` 4 + 클라 EditMode `SpawnResolverTests`(동일 기대 벡터 = drift 가드) 통과. **남음**: PlayMode `SocketE2ETests`에 "늦은 입장자가 기존 플레이어 스폰" 보강(Docker 필요).
- **저작 레이어(SO + Export 툴)**: 스폰 데이터의 진실원은 **`MapDefinition`(SO, 맵당 1개, `Gameplay/Spawn/`, 에셋 `Assets/GameData/Maps/{mapId}.asset`)** — 디자이너가 편집. 필드: `mapId`, **`visualPrefab`(맵 배경 모델, 클라 전용)**, `playerSpawns[]`(추후 monsterSpawns 합류). 스폰 좌표 런타임은 SO 직접 아닌 **bake된 JSON**(서버는 UnityEngine 의존 0이라 SO 불가 → JSON이 유일 교환 포맷, parity 자명).
  - **맵 비주얼**: `MapLoader`(IAsyncStartable, `Gameplay/Spawn/`)가 Dungeon 진입 시 서버 mapId 대기 → `Resources.Load<MapDefinition>("Maps/{mapId}")` → `visualPrefab` 인스턴스화. `DungeonLifetimeScope`에 등록. 프리팹은 JSON에 안 들어가므로(서버 무관) **이 경로만 SO 직접 읽음**.
  - **툴**(`Game.Gameplay.Editor`): `MapDataExporter` — 메뉴 `Tools/Spawn/Export Map Data`(SO→JSON, 클라 Resources + 서버 임베디드 동시 기록; 서버는 재빌드 시 반영) + `Import Map Data from JSON`(JSON→SO 부트스트랩) + `BakeAll()`(다이얼로그 없는 재사용 bake). 에셋 위치 `Assets/GameData/Maps/{mapId}.asset`.
  - **프리뷰 씬 저작 UX**: `MapEditorWindow`(메뉴 `Tools/Spawn/Map Editor Window`) — New Editor Scene → MapDefinition 선택 → **Load to Scene**(visualPrefab + `SpawnPointMarker`(런타임 Gizmo, 저작전용) 생성) → 드래그 배치/Add → **Save to SO & Export**(`Spawns` 자식 sibling순=SpawnIndex로 write-back + BakeAll). 인덱스 라벨은 Editor `DrawGizmo`(런타임 마커는 UnityEditor 미참조).
  - **몬스터 스폰 스키마**: `MapDefinition.monsterSpawns[]`(`MonsterSpawn{monsterId,position,rotationY,count,wave}`) — 저작/Export(JSON `maps[].monsters[]`)만 구현. 실제 스폰/AI는 M3(서버 권위). 서버 파서는 unknown 필드 무시라 forward-compatible.
  - **PlayMode 검증**: `CharacterSpawnMultiClientTests`(Fake 소켓, Docker 불필요) 3/3 — ①다중클라(로컬1 결정론좌표+원격2 현재위치) ②늦은 입장 동적 스폰 ③MapLoader visualPrefab 인스턴스화. 실서버 다중클라는 `SocketE2ETests`(Docker, 7/7).
  - **전원 입장 → 인게임 준비**: 서버가 MemberCount==MaxMembers 시 `S_GameStatus(InProgress)` 브로드캐스트(기존) → 클라 `GameStatusPacketHandler`(신규, `Network/Socket/Handler/Contents`)→`ISocketPacketState.MarkDungeonReady()`→`OnDungeonReady` 이벤트. **별도 S_DungeonReady 패킷 안 만듦**(중복 회피, 원칙1). 두 소비자: ① `InGameModel`→`InGameResult.DungeonReady`→`InGameState.IsDungeonReady`(GUI 바인딩용) ② 로딩 게이트(아래). EditMode `InGameStatusRelayTests` 5/5.
  - **로딩 게이트(전원 입장까지 Loading 유지 → Fader reveal)**: `IGameSceneManager.LoadSceneAsync(scene, ct, Func<UniTask> holdUntil=null)`. `GameSceneManager`가 씬 활성화 후 `holdUntil` 완료까지 **Loading을 띄운 채 대기**(씬은 뒤에서 스폰), 완료 시 Fader FadeOut으로 reveal. `GameSessionConnector`가 입장 전 `OnDungeonReady`를 `UniTaskCompletionSource`로 래치(레이스 방지) → `holdUntil=()=>WaitForDungeonReadyAsync`(30s 타임아웃 가드, 초과 시 진행). 씬매니저는 player 의미 모름(관심사 분리). 대기 중 Loading에 "다른 플레이어를 기다리는 중…" 표시(`ILoadingView.SetMessage`). 흐름 로그: `GameStatusPacketHandler`(S_GameStatus 수신)→`GameSessionConnector`(전원입장 신호/대기시작/완료·타임아웃)→`GameSceneManager`(holdUntil 시작/완료)→`InGameModel`(IsDungeonReady 전환). 검증: `GameSessionConnectorTests` 3/3 + **E2E `SocketE2ETests` "전원_입장하면_양쪽_S_GameStatus_InProgress_수신"**(두 실클라→서버 브로드캐스트, 8/8).
  - **왜 런타임은 JSON(SO 직접 아님)**: 빌드타임 bake 패턴 — Resources에 SO 산포/런타임 SO 로딩 회피, 서버·클라 동일 바이트라 결정론 parity가 자명. SO는 *저작*, JSON은 *런타임/교환 산출물*. (예외: `visualPrefab`은 클라 전용이라 `MapLoader`가 SO 직접 읽음.)
  - **맵 관리 방향(합의)**: Scene-per-map ❌(`.unity`는 서버가 못 읽음·머지지옥), **Data(SO)+Prefab** ✅. 오픈월드는 *보류*(스키마 단일 MapDefinition로 시작, cell좌표/AoI 미구현).

### 2.2 Character 관리 아키텍처 방향 (FSM 두 축 분리 + GAS + 공유 코어)
- **무엇**: 캐릭터를 (1) **Locomotion FSM**(이동 모드) + (2) **Action=GAS**(공격/상호작용/스킬)로 **두 축 분리**. Character=합성+교체 **Driver**(Local/Network), Animation=관찰 **View**(MM=이동 / Action=트리거 클립), 전투=**서버 권위 판정**(데이터 active window)+클라 연출(HitStop per-actor), 스킬=**`Shared.Gameplay`**(netstandard, UnityEngine 의존 0) 공유 결정론 코어 + **JSON 데이터**(SO 아님).
- **왜**: 기존 FSM이 Locomotion/Action 축 혼용 → 전이 폭발·공격 이중화(AttackState↔BasicAttackAbility). 서버도 GAS·데이터 공통으로 결정론적 판정·예측 reconcile.
- **결정론 수준**: Co-op = 서버 권위+클라 예측(reconcile). fixed-point/롤백은 PvP 전용 → 지금 과설계.
- **안 함**: ECS/DOTS, Unity Timeline 전투구동, SO를 데이터 진실원, 처음부터 재작성.
- **상세 설계**: [character-architecture.md](character-architecture.md) · **구현 점진 순서**: [plan.md](plan.md) Character 리팩터 섹션.

### 2.0 클라 폴더/어셈블리 구조 정리 (과분리 통합)
- **무엇**: 스프롤한 Script 구조/어셈블리를 통합. (1) 빈 `Game.asmdef` 제거, (2) `System/AuthSystem`+`Auth` → `System/Auth` + ns `Game.System.Auth`, (3) `Game.Main`→**`Game.Gameplay`**(폴더 `Main/`→`Gameplay/`, ns 일괄), (4) **`Game.Input`→`Game.Gameplay`**(폴더 `Gameplay/Input/`, ns `Game.Gameplay.Input`; GUI가 Gameplay 참조), (5) **`Game.OutGame`+`Game.InGame`→`Game.Presentation`**(폴더 `Presentation/{Title,DungeonLobby,InGame}`, ns `Game.Presentation.*`). 어셈블리 14→11개(Game/Game.Input/Game.InGame 제거).
  - 충돌 처리: InteractionDetector가 `Game.Gameplay.Input`로 이동하며 형제 ns `Game.Gameplay.Camera`와 `Camera` 충돌 → `UnityEngine.Camera`로 정규화.
- **왜**: CLAUDE.md 최우선 원칙 #1(간결)·#2(과분리 금지). 어셈블리/네임스페이스 혼용(`Script.*` vs `Game.*`, Auth 이중 폴더, 모호한 "Main")을 도메인 기준으로 통일.
- **주의**: MVI 레이어 경계(`GUI→OutGame→System→Network`)는 의도된 의존 방향이라 유지. 남은 `Script.*`(MotionSystemV2 29·Ability 10·Startup 3)는 **점진** — 해당 영역 작업 시 통일(MotionV2는 별도 활성 작업이라 보류).
- **검증**: 컴파일 0 에러, EditMode 106 통과. asmdef name 참조처가 없어(전부 GUID) 리네임 안전.

### 2.1 던전 퇴장 → 플레이어 단위 association 정리 (이벤트 일반화)
- **무엇**: 플레이어가 인게임에서 나가면(ReturnToLobby/타임아웃/연결끊김), **퇴장마다** 플레이어 단위 이벤트를 발행해 GameServer가 그 유저의 방 연결을 정리한다. 1인/N인 모두 동일 경로 → 재로그인 복원 안 됨.
- **흐름**:
  `Client InGameModel.ReturnToLobby` → `SocketSession.LeaveRoomAsync(C_PlayerLeave)`
  → `SocketServer RoomManager.LeaveRoom` → **퇴장마다** `PlayerLeftRoomMessage{RoomId,UserId,RoomEmptied}` 발행
  → Redis `stream:game:room:lifecycle`
  → `GameServer RoomLifecycleConsumer` → `DungeonLobbyService.RemovePlayerFromRoomAsync(roomId, userId)`
  → ① `DungeonRoomPlayer` association 제거 ② 채팅 방 구독 해제
     ③ 잔여 0명 → **방 삭제** / 잔여 ≥1명 → 떠난 사람이 호스트면 **호스트 이양** + Update + 로비 브로드캐스트
  → 다음 로그인 `GetByUserIdAsync==null` → `CurrentRoomId=0` → 복원 안 함.
- **핵심 결정**:
  - `CurrentRoomId`는 로그인 시 `DungeonRoomPlayer`(userId→roomId)로 해석 → **association 제거가 복원 차단의 핵심**.
  - GameServer는 SocketServer의 `RoomEmptied` 힌트를 맹신하지 않고 **DB 잔여 인원으로 재판정**(영속 진실).
  - 빈 방은 status=Closed로 두지 않고 **삭제**(gRPC `LeaveRoomAsync` 빈방 경로와 통일).
  - 서버 간 직접 RPC 금지 → **Redis Stream**으로만 통지.
- **파일**:
  - Shared: `Shared.Infrastructure/Messages/PlayerLeftRoomMessage.cs`
  - 서버: `GameServer.Application/Domains/DungeonLobby/DungeonLobbyService.cs` (`RemovePlayerFromRoomAsync`)
  - 서버: `GameServer.Infrastructure/Common/Consumer/RoomLifecycleConsumer.cs`, `.../MessageQueue/PlayerLeftRoomMessageQueue.cs`
  - 소켓: `SocketServer/Room/RoomManager.cs` (`LeaveRoom`/`PublishPlayerLeft`), `MessageQueue/RoomLifecycleMessageQueue.cs`, `IRoomLifecyclePublisher.cs`
  - 클라: `Client/Assets/Script/InGame/InGameModel.cs`, `Network/Socket/Session/SocketSession.cs`, `OutGame/DungeonLobby/LobbyModel.cs`(RestoreRoom Closed 분기)
  - 테스트: `SocketServer.Tests/Room/RoomManagerLeaveRoomTests.cs`, `GameServer.Tests/Application/Services/DungeonLobbyServiceTests.cs`(RemovePlayer*), `GameServer.Tests/E2E/RoomLifecycleConsumerIntegrationTests.cs`
- **이전 구멍(해결됨)**: 과거엔 방이 빌 때만 `RoomClosedMessage`를 발행해 N인 부분 퇴장 시 그 1명 association이 남았다. → 플레이어 단위 이벤트로 일반화하여 해결.

### 2.2 IRoomLifecyclePublisher 인터페이스 추출
- **무엇**: `RoomManager`가 Redis 구체 클래스(`RoomLifecycleMessageQueue`) 대신 `IRoomLifecyclePublisher`에 의존.
- **파일**: `SocketServer/MessageQueue/IRoomLifecyclePublisher.cs`, `Program.cs`(DI), `Room/RoomManager.cs`
- **왜**: 발행 동작을 fake로 단위 테스트하기 위해. Redis 없이 `FakeRoomLifecyclePublisher`로 검증.

### 2.3 GameHud를 Addressable로 런타임 로드
- **무엇**: HUD를 씬에 미리 배치하지 않고 `GameHudController`가 Dungeon 진입 시 프리팹을 Addressable 로드·생성·DI 주입.
- **파일**: `Client/Assets/Script/GUI/Hud/GameHudController.cs`, `GameHud.cs`, `VContainer/LifetimeScopes/Scenes/DungeonLifetimeScope.cs`, `GUI/AddressKeys.cs`(`UI.GameHud`)
- **왜**: 씬 미배치 → `RegisterComponentInHierarchy<GameHud>()`가 "not in scene" 예외. `LobbyViewController`의 검증된 Addressable 로드 패턴과 동일하게 처리.

### 2.4 GameSessionConnector 방 입장 재시도
- **무엇**: `GameSessionReady` 수신 후 TCP 접속+`C_PlayerJoin`을 최대 10회 재시도.
- **파일**: `Client/Assets/Script/System/InGame/GameSessionConnector.cs`
- **왜**: SocketServer가 `GameStartRequested`를 소비해 방을 만드는 시점과 클라 접속 사이 **레이스**. 단발 실패 시 옛 코드는 포기 → `Failed`. 분산 connect/create 순서는 본질적으로 재시도가 정답.

---

## 3. 테스트 하네스 (어디에/어떻게)

| 계층 | 위치 | 하네스 |
|------|------|--------|
| 서버 단위 | `GameServer.Tests/Application/Services/*Tests.cs`, `SocketServer.Tests/**` | fake 레포(`GameServer.Tests/Infrastructure/Fakes/`), `FakeRoomLifecyclePublisher` |
| 서버 통합 | `GameServer.Tests/Infrastructure/Integrations/*IntegrationTests.cs` | `[Collection("RepositoryIntegrationTests")]` + `RepositoryTestFixture`(Testcontainers Postgres+Redis) |
| 서버 풀스택 | `GameServer.Tests/E2E/GameStartE2ETest.cs` | `TestGameServerHost`(인메모리 Kestrel+gRPC, fake 인프라) |
| 클라 EditMode | `Client/Assets/Script/Tests/EditMode/**` | NUnit + VContainer + fake |
| 클라 PlayMode E2E | `Client/Assets/Script/Tests/PlayMode/E2E/**` | `E2ETestBase` (Docker 서버 대상) |

- TDD 순서: 실패 테스트 먼저(Red) → 구현(Green). 게임플레이/도메인 테스트 메서드명은 한국어.

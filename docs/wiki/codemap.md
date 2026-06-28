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
| **스킬 데이터(SkillTimeline)** | 저작=클라 `Gameplay/Abilities/{SkillDefinition,SkillCatalogDefinition,SkillCatalogProvider}` + `Editor/SkillCatalogExporter` → bake `Shared.Infrastructure/Skills/skills.json` → 서버 `Shared.Infrastructure.Skills.SkillCatalog` → `CombatHandler.ResolveSkill`. 자산 `Assets/GameData/Skill/`. 아래 §2.49 | gas-architecture §2.5 |
| **상태이상(CC) — 스턴·슬로우·넉백** | 정의=`Shared.Gameplay` `GameplayTags.Stun/Slow`+`GameplayEffectCatalog`(stun_1_5s/slow_3s,GrantedTags)+`Combat/CcConfig`. 게이트=클라 `PlayerCharacterAgent`(스턴)·`GroundState`(슬로우). 부여=던전 `monsters.json onHitEffectId`→`Room.TickMonsters` S_ApplyEffect / Main `LocalMonster.onHitCcId`. **넉백**=`Gameplay/Character/KnockbackDriver`+`PlayerCharacterAgent.ApplyKnockback`(public, Ability 융합용). 아래 §2.48 | [authority-model.md](authority-model.md) |
| **게임플레이 카메라(3인칭 Follow)** | `Gameplay/Camera/{GameplayCameraRig,CharacterCameraFollow}` — rig가 `LocalPlayerContext.OnSet`→vcam.Follow 런타임 바인딩. 아래 §2.47 | — |
| SocketServer(TCP/방/세션) | `ServerAll/SocketServer/SocketServer/{Room,Session,PacketHandler}` | [socketserver.md](socketserver.md) |
| Redis 스트림/큐 | `Shared/Shared.Infrastructure/MessageQueue/`, `Messages/` | [redis.md](redis.md) |
| **루트/드랍(던전 경로)** | 드랍/줍기 = `SocketServer/Loot/`(DropTable·GroundItem)·`Handler/{CombatHandler.SpawnDrops,LootHandler}`·`Room`(GroundItem·TryPickup) / 지급 = `GameServer.Infrastructure/Common/{Consumer/LootGrantConsumer,MessageQueue/LootPickupMessageQueue}` → `IInventoryService.GrantItemAsync`. 아래 §2.16. **Main(싱글) 경로 지급 = `GameServer.API/Services/InventoryGrpcService.GrantItem`(gRPC+가드, §2.18)** | [loot-drop.md](loot-drop.md) |
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

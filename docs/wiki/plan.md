# 작업 플랜 (현재 진행 상황)

> **새 채팅 시작 시 이 파일을 먼저 읽어라.**  
> Phase가 완료될 때마다 즉시 갱신한다.  
> 마지막 갱신: 2026-06-03 (**EF-2 버프/디버프 서버 동기화 루프 완성** — EF-2a~d. `Shared.Gameplay`(netstandard2.1) 결정론 코어를 **DLL 단일소스**로 클라 공유(중복 8개 삭제, 동일 ns). 패킷 `S_ApplyEffect`/`S_RemoveEffect`(Union 1640/1641). 서버 `CombatHandler`(`C_Attack`→권위 `S_ApplyEffect` 브로드캐스트) → 클라 `EffectReceiver`(타겟 라우팅→`ASC.ApplyEffectAuthoritative`). **E2E A공격→B수신 1/1**, EditMode 127/127, SocketServer 13/13. 남음=공유시계·예측·원격ASC라우팅·실전 combat(CA-3). 상세 = [effect-system.md](effect-system.md)·codemap §2.4)
>
> 직전 갱신: 2026-06-03 (MPPM 멀티 클라 검증 — 블로커 2건 모두 해소: ① Docker 이미지 리빌드 → SocketE2ETests 6/6 ② `MainLifetimeScope` DI 누락(`LocalPlayerContext`/`SpawnLayoutProvider`) 수정. 상세 = [mppm-testing.md](mppm-testing.md))

---

## 🎯 최종 목표 — 포트폴리오 게임 완성

**완성 정의(DoD)**: 2명이 (MPPM) 접속 → 로비에서 방 생성·시작 → 던전 입장(서로 보임·이동) → 몬스터 협력 처치 → 클리어 → 보상 수령 → 로비 복귀. **전 과정 서버 권위 + E2E 통과 + 데모 영상.**

**범위**: Co-op 던전 **버티컬 슬라이스** + **PVE 오픈월드 맛보기**(싱글 탐험/퀘스트 최소). 폴리시(애니메이션·스킬·아이템·사운드)는 **코어 루프 이후**.

| 마일스톤 | 목표 |
|---|---|
| ✅ M0 | 인증·로비·채팅·소켓·던전 입퇴장·DB/캐시·Unity OutGame (기반) |
| 🔄 M1 | 인게임 진입 — 로컬/원격 캐릭터 스폰·이동, 인게임 UI 전환 |
| M2 | 전투 코어 — Character 두 축 리팩터(GAS) + 서버 권위 Attack/Hit/Damage |
| M3 | 몬스터 — Spawn/AI/State/Dead 동기화 |
| M4 | 던전 루프 완성 — Clear → 보상 → 로비 복귀 **(= DoD)** |
| M5 | 폴리시/콘텐츠 — 애니(MotionMatching V2)·스킬·아이템·사운드 + PVE 맛보기 |
| M6 | 마감 — 데모·부하/E2E 검증·배포/포트폴리오 문서 |

> Character 아키텍처([character-architecture.md](character-architecture.md))는 M1(합성/Driver)·M2(GAS)에 **통합**됨. 상세 태스크는 아래 "작업 순서".

---

## 📋 작업 순서 (M1 → M6, 단일 정렬 로드맵)

> 각 단계 = **서버 + 클라 + 테스트(TDD)** 3축, 끝에 **MPPM 2-client** 검증.
> Character 아키텍처(CA)는 의존성에 맞춰 M1(합성/Driver)·M2(GAS)·M5(툴)에 통합.
> **안 함**: ECS/DOTS·Unity Timeline 전투구동·SO를 데이터 진실원·fixed-point/롤백(Co-op)·재작성.

### 🔄 M1 — 인게임 진입: "던전에서 서로 움직임이 보인다"
선행: M0. **현 Locomotion FSM 그대로**(전투 미포함). Dungeon 씬은 그린필드 → 게임플레이 DI/스폰 신규 구성.
- [x] 소켓 진입 흐름(구 A-1/A-2): `SocketApiClient` 등록·`DungeonLifetimeScope`·`GameSessionConnector`(OnGameSessionReady→TCP→`C_PlayerJoin`, 재시도·중복 가드), `C_Auth` 제거, Redis 검증 재작성, 포트폴리오 챕터 11
- [x] 던전→Main 복귀(`InGameModel` MVI·`GameHud`·`GameHudController` Addressable)
- [x] 던전 퇴장 일관성(`PlayerLeftRoomMessage` 일반화·`RemovePlayerFromRoomAsync`·멱등성) — [codemap §2.1](codemap.md)
- [x] 원격 동기화 데이터 토대 — `ISocketPacketState.GetAllPlayers()` (※ reconciler 클래스는 제거, diff는 presenter/spawner 내부로)
- [x] **`AddressableLoader`(Util)** 통합 — Lobby/Hud 중복 로드 일원화
- [x] **`CharacterSpawner`(생성/삭제)** + **Character 합성 + `Driver`(LocalInput/Network)** [CA-4] — Dungeon 씬 게임플레이 DI 구성
- [x] **GameHud HP/MP ↔ GAS 연동** — `GameplayAttribute.OnChanged`→`ASC.OnAttributeChanged`→`LocalPlayerContext`→`InGameModel`→`SliderBall`. EditMode(릴레이)·PlayMode(prefab 동적생성 렌더) 테스트 통과
- [x] 로컬 PlayerCharacter 스폰(SpawnIndex, 결정론 Resolve) + 이동→`C_Move` 송신(`MoveSyncSender`) — PlayMode `CharacterSpawnMultiClientTests` 검증
- [x] 원격 NetworkCharacter 스폰 + 스냅샷 보간(`RemoteDriver`) — 다중 원격·늦은 입장 동적 스폰 PlayMode 3/3 통과. 맵 비주얼=`MapLoader`(MapDefinition.visualPrefab). 저작=`MapEditorWindow`(SO→JSON bake). `monsterSpawns[]` 스키마 선반영(M3 스폰 로직 대기)
- [x] 전원 입장 → 인게임 진입(로딩 게이트 + Fader) — 기존 `S_GameStatus(InProgress)` 재사용(새 패킷 X): `GameStatusPacketHandler`→`ISocketPacketState.OnDungeonReady`→ ① `InGameModel.IsDungeonReady` ② `GameSessionConnector`가 `LoadSceneAsync(holdUntil)`로 **전원 입장까지 Loading 유지("다른 플레이어를 기다리는 중…") → 완료 시 Fader reveal**(30s 타임아웃 가드). 흐름 로그 全 hop. **E2E `SocketE2ETests` 8/8**(전원 입장 시 양쪽 S_GameStatus 수신) + EditMode 릴레이 5/5 + `GameSessionConnectorTests` 3/3
- [ ] **MPPM 2-client** "서로 보임·이동" 검증 — 선행 블로커 2건 ✅해소([mppm-testing.md](mppm-testing.md)): ① 서버 이미지 리빌드(E2E 8/8 그린) ② `MainLifetimeScope` DI 누락 수정. 데이터 선행(로컬/원격 스폰·전원입장 전환·로딩 게이트) ✅ → **남은 건 MPPM 2-창 시각 검증(사람 조작)**

### M2 — 전투 코어 (Character 두 축 리팩터 동반)
선행: M1. 상세 기획: [effect-system.md](effect-system.md)

**Effect/버프 시스템 — 1단계 (클라 단독·sync-ready, M1 중에도 착수 가능)**
- [x] **EF-1a**: Attribute 재구조 — Resource vs Stat(`EAttributeKind`) 분리, `SetCurrent`(파생·정수 `GameplayEffectMath.Aggregate`). 즉발은 Resource만 영구 변경
- [x] **EF-1b**: `GameplayEffectDefinition`/`ActiveGameplayEffect`/`EEffectCategory`/`EDurationPolicy`/`EStackPolicy`/`GameplayEffectCatalog`(string id 시드) — 순수 데이터, Sprite 없음
- [x] **EF-1c**: ASC `ApplyEffect`/`RemoveEffect`/`Tick`(만료) + 추가/제거/만료 시 `RecalculateStats` → `OnActiveEffectsChanged`, `GetActiveEffectSnapshots`
- [x] **EF-1d**: `EffectIconCatalog`(Presentation SO·표시전용·카테고리→Sprite+polarity 색) + `BuffView` DTO + `InGameState.Buffs` + `InGameModel` 중계
- [x] **EF-1e**: `GameHud` 버프슬롯 풀 렌더 + `BattleEffectSlot.Bind`+로컬 카운트다운. EditMode 122/122(가역성·만료·스택·카탈로그 로드) + PlayMode(슬롯 렌더·HP/MP) 통과
- [x] **EF-1f**: `EffectIconCatalog` 에셋(`GameData/Resources/Effects/`) — fg4_icons PSD 멀티스프라이트(firesword/shield/boots) + 버프/디버프 색. `DungeonLifetimeScope`가 Resources 폴백 자동 로드(씬 수정 0). **UI Toolkit 커스텀 인스펙터**(색상 편집 + 카테고리별 아이콘 버프/디버프 색 미리보기)

**Effect/버프 시스템 — 2단계 (M2 합류·서버 권위)**
- [x] **EF-2a**: `Shared.Gameplay`(netstandard2.1) 결정론 코어 — enums/`GameplayAttributeModifier`/`GameplayEffectMath.Aggregate`/`EffectTiming`. xUnit golden **9/9**(클라 EF-1 `EffectSystemTests`와 동일 벡터 = parity). sln 등록
- [x] **EF-2c (서버)**: `S_ApplyEffect`/`S_RemoveEffect` 패킷(`Shared.Packet`, Union **1640/1641**) — 남은시간 미전송, StartTick+EffectId만. SocketServer.Tests 직렬화/Union **11/11**
- [x] **EF-2b**: DLL 단일 소스 — `Shared.Gameplay.dll`(netstandard2.1)을 `Client/Assets/Plugins/`에 배치, 클라 중복 순수 8개(enums·`GameplayAttributeModifier`·`GameplayEffectMath`) 삭제. DLL 타입을 클라와 **동일 ns(`Script.System.GamePlayAbilitySystem`)**로 둬 클라 코드 수정 0. 테스트 asmdef 2개에 DLL 참조 추가. EditMode **122/122**
- [x] **EF-2c (클라)**: 패킷 미러 — `ClientCodegen` 재생성으로 `S_ApplyEffect`/`S_RemoveEffect`(Union **1640/1641**) 클라 반영(`Network/Socket/Packets/EffectPacket.cs`). 컴파일+회귀 122 통과 (gRPC 생성물 idempotent)
- 🔄 **EF-2d** (서버 전투 CA-1/3 선행 — 대부분 combat-gated):
  - [x] **ASC 서버-권위 적용 시드**: `ApplyEffectAuthoritative(def, instanceId, stacks)` — 서버 InstanceId를 키로 사용(클라 id 생성 안 함)·멱등 재적용·`RemoveEffect(serverId)` 권위 제거. 타이밍은 로컬 clock(공유 시계 전). EditMode 124/124. (Game.System 격리 — `SocketApiClient` 미수정)
  - [x] **서버 emit(테스트 등급)**: `CombatHandler.HandleAttack`(`C_Attack` → 대상에 디버프 `S_ApplyEffect` 방 브로드캐스트, 서버 권위 `Room.NextEffectInstanceId()`+StartTick). 순수 빌더 `BuildAttackEffect` 단위검증. SocketServer.Tests **13/13**. ※ 풀 combat 불필요 — active-window/HP시뮬/SkillId매핑은 CA-3 대체
  - [x] **수신 핸들러 배선(클라)**: `SocketApiClient`의 `ISocketPacketState`에 effect 이벤트(`SocketEffectApply`)+`Effect{Apply,Remove}PacketHandler` → `EffectReceiver`(Presentation, 카탈로그+`AuthSession` 타겟 라우팅 → `ApplyEffectAuthoritative`). EditMode **127/127**(EffectReceiver 라우팅 3)
  - [x] **E2E 2-클라 검증**: `SocketE2ETests` — A `C_Attack`→서버 `S_ApplyEffect` 브로드캐스트→B 수신(InstanceId>0·Source/Target 일치) **1/1**(Docker 리빌드+force-recreate)
  - [ ] **남은 정밀화**: 공유 시계(서버 tick, StartTick 정밀 만료) + 클라 예측/정정 + 원격 캐릭터 ASC 라우팅(현재 로컬 대상만) — CA-3 합류 시

**Character 리팩터 + 서버 권위 전투**
- [x] **CA-1**: 두 축 분리 — Action을 Locomotion FSM에서 들어냄. **`CharacterAgent` 무접촉**(config/factory/builder에서만 제거) = Move 핫스팟 충돌 0. EditMode **129/129**.
  - [x] **Attack (증분1)**: `AttackState`/`GroundToAttack`/`AttackToGround`/`StateKind.Attack` 제거 + Factory/Builder/`CharacterStateContext.HitEventReceiver` 정리 + SO Attack 항목 삭제. 발동=`PlayerCharacterAgent.HandleAttackInput`(`ConsumeAttackPressed`→히트리셋+애니). 데미지는 기존 GAS 체인(AnimEvent→`CharacterHitEventReceiver`→`BasicAttackAbility`) 유지. `LocomotionStateMachineTests` 추가.
  - [x] **Interact (증분2)**: `InteractState`/`GroundToInteract`/`InteractToGround`/`StateKind.Interact`/`CharacterStateContext.InteractionDetector` 제거 + SO Interact 항목 삭제. 발동=`PlayerCharacterAgent.HandleInteractInput`(`ConsumeInteractPressed`→`InteractionDetector.CurrentInteractable.Interact(gameObject)`+애니). **실작동 구 Character-ns 경로 유지**(`IInteractable.Interact(GameObject)` = instigator 보유 → 아이템 친화). ※ 프리팹 점검 결과 신 `InteractionSystem`은 **아웃게임 등록·인게임 휴면**(detector 프리팹 미배선)이라 통합 대상 아님.
  - [x] **규칙갱신**: `.claude/rules/unity-gameplay-state.md` → "두 축 분리 / Attack·Interact=Action축(FSM 아님)"로 갱신.
  - [ ] **별도 정리(후속)**: `Game.Gameplay.Input.InteractionSystem`(리치·라우터, 휴면 중복) 제거 또는 인게임 일원화 결정. 아이템 인벤토리 합류 시 instigator 흐름 확정.
- [x] **CA-2**: `Shared.Gameplay`에 SkillTimeline 스키마 + 결정론 코어 — `SkillTimeline`(Startup/Active/Recovery/Cooldown ms + `HitboxSpec`(Box/Sphere, System.Numerics) + `OnHitEffectIds`[]), `SkillTimelineMath`(PhaseAt/IsActive), `HitboxMath.Overlaps`(시전자 yaw 로컬변환+박스/구 겹침, 엔진 비의존), `SkillCatalog`(코드 시드 `basic_swing`). **서버 권위 설계**: cue/애니 스키마 제외(클라 전용), 적중=`OnHitEffectIds`→GameplayEffect→**EF-2d `S_ApplyEffect` 재사용**(데미지 Instant/디버프 Duration). xUnit **17/17**. DLL 재빌드→Plugins 갱신(클라 컴파일 클린). ※JSON 로더/저작툴=CA-5, on-hit `basic_attack_dmg`는 GameplayEffectCatalog에 추가 필요(CA-3)
- [ ] **CA-3**: BasicAttack end-to-end — 같은 JSON, **서버 권위 active window 판정 + GE 데미지**, 클라 예측/애니/**HitStop(per-actor)**
- [ ] 2-client 공격·피격 동기화 검증

### M3 — 몬스터
선행: M2.
- [ ] `CharacterSpawner`로 몬스터 스폰(NetworkCharacter + 간단 AI: 추적/공격)
- [ ] GAS 피격/사망 동기화 (`SpawnMonster`/`MonsterState`/`MonsterDead`)

### M4 — 던전 루프 완성 (= DoD)
선행: M3.
- [ ] 클리어 조건(전멸/보스) → `DungeonClear`
- [ ] 보상(경험치/아이템 GE) 적용 → 로비 복귀
- [ ] 완전한 Co-op 1판 루프 E2E (MPPM 2-client)

### M5 — 폴리시 + PVE 맛보기
- [ ] 애니메이션(MotionMatching V2 액션 블렌딩)·HUD 다듬기·스킬1~2·아이템 최소·사운드
- [ ] **CA-5**: Skill Timeline 에디터 툴(공유 JSON read/write)
- [ ] PVE 오픈월드 맛보기(싱글 탐험/퀘스트 최소)

### M6 — 마감
- [ ] 데모 영상/gif·부하/E2E 검증·배포/포트폴리오 문서

---

## ✅ 완료된 Phase

| Phase | 내용 |
|-------|------|
| 서버 인프라 | Clean Architecture, JWT 인증, DeviceId Binding, Token Rotation |
| 던전 로비 | gRPC 방 CRUD, SubscribeRoom Streaming |
| 게임 시작 E2E | Outbox → Redis Stream → SocketServer 방 생성 → IP:Port 알림 |
| SocketServer | C_Auth, C_PlayerJoin, C_Move/S_Move, Ping/Pong |
| 채팅 | Redis Streams, Global/Room/Whisper |
| 분산 로그 | Serilog + Graylog, TraceId 전파 |
| DB/캐시 | PostgreSQL + Redis Cache-Aside, Testcontainers 통합 테스트 |
| 클라이언트 OutGame | gRPC 로그인/로비 UI, VContainer DI, MVI 아키텍처 |

---

## 참고 파일

- 전체 현황: [`docs/wiki/status.md`](status.md)
- 패킷 규칙: [`docs/wiki/packets.md`](packets.md)
- SocketServer 규칙: [`docs/wiki/socketserver.md`](socketserver.md)
- 서버 흐름: [`docs/wiki/gameflow.md`](gameflow.md)

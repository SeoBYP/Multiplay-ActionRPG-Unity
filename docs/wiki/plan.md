# 작업 플랜 (현재 진행 상황)

> **새 채팅 시작 시 이 파일을 먼저 읽어라.**
> Phase가 완료될 때마다 즉시 갱신한다.
> 마지막 갱신: 2026-06-04 (**WBS 통합** — 전체 RPG 스코프 트리(`§전체 범위`)를 plan.md로 흡수하고 `wbs.md` 삭제. 마일스톤 ↔ WBS ID 매핑, 서버 도메인 세션 트랙(`§세션 트랙`) 명시. 마일스톤(실행 순서)과 WBS(구조)를 공유 ID로 일원화.)
>
> 직전 갱신: 2026-06-03 (**M1 던전 입장 코어 완료 + 커밋**(`feature/m1-dungeon-entry-foundations`). ① 결정론 스폰: `spawn-layouts.json`+`SpawnResolver`(서버·클라 미러)·`CharacterSpawner`. ② 맵 저작: `MapDefinition`+`MapLoader`+에디터 툴. ③ 전원 입장→로딩 게이트→Fader(`S_GameStatus` 재사용). **E2E `SocketE2ETests` 그린** + MPPM 2-창 시각 검증 완료. codemap §2.3 / **M2 CA-3 증분②까지 완료** — 증분③(2-client 검증)만 남음)

---

## 🎯 최종 목표 — 포트폴리오 게임 완성

**완성 정의(DoD)**: 2명이 (MPPM) 접속 → 로비에서 방 생성·시작 → 던전 입장(서로 보임·이동) → 몬스터 협력 처치 → 클리어 → 보상 수령 → 로비 복귀. **전 과정 서버 권위 + E2E 통과 + 데모 영상.**

**범위**: Co-op 던전 **버티컬 슬라이스** + **PVE 오픈월드 맛보기**(싱글 탐험/퀘스트 최소). 폴리시(애니메이션·스킬·아이템·사운드)는 **코어 루프 이후**.

**범위 제외(2026-06-03 결정)**: **던전 난이도/시즌 · 가챠 · 우편함**은 포트폴리오 범위에서 제외 — WBS·로드맵에 넣지 않는다.

| 마일스톤 | 목표 | WBS 노드 |
|---|---|---|
| ✅ M0 | 인증·로비·채팅·소켓·던전 입퇴장·DB/캐시·Unity OutGame (기반) | 1.* |
| ✅ M1 | 인게임 진입 — 로컬/원격 캐릭터 스폰·이동, 인게임 UI 전환 | 1.6 |
| 🔄 M2 | 전투 코어 — Character 두 축 리팩터(GAS) + 서버 권위 Attack/Hit/Damage | 2.1·2.2·2.5.1·2.6.2 |
| M3 | 몬스터 — Spawn/AI/State/Dead 동기화 | 4.1.* |
| M4 | 던전 루프 완성 — Clear → 보상 → 로비 복귀 **(= DoD)** | 2.3·3.1·4.2·4.3·6.1·6.2·7.1 |
| M5 | 폴리시/콘텐츠 — 애니(MotionMatching V2)·스킬·아이템·사운드 + PVE 맛보기 | 2.4·2.6·2.7·3.2~3.8·4.4~4.7·5.*·6.3~6.4·7.2~7.8·8.* |
| M6 | 마감 — 데모·부하/E2E 검증·배포/포트폴리오 문서 | 9.8·9.9 |

> Character 아키텍처([character-architecture.md](character-architecture.md))는 M1(합성/Driver)·M2(GAS)에 **통합**됨. 상세 태스크는 아래 "작업 순서".

---

## 🌳 전체 범위 — WBS 트리

> RPG 게임 완성에 필요한 **전체 작업의 계층 분해(Work Breakdown Structure)**. 마일스톤(아래 "작업 순서")이 *실행 순서* 뷰라면, 이 트리는 *구조* 뷰다. 둘은 WBS ID로 묶인다.
>
> **범례** — 상태: `✅`완료 `🔄`진행 `⬜`미착수 / **Tier**: **T1** 코어·DoD 필수 · **T2** RPG 확장 · **T3** 향후/선택 / **Owner**: 🟢 서버 도메인 세션 · 🔵 GAS/전투 세션 · 🟣 애니(MotionMatching V2) 세션 · ⚪ 미배정. (말단에만 `상태 | Tier | Owner` 태그)
>
> **YAGNI**: 카탈로그는 *가시화* 목적. 실제 착수는 Tier 순(T1→T2→T3)으로 마일스톤에 편입될 때만.

### 1. 기반 시스템 — ✅ 완료 (M0/M1) → 상세는 `§완료된 Phase`
- **1.1** 계정/인증 · **1.2** 유저/프로필 · **1.3** 로비/매칭 · **1.4** 채팅 · **1.5** 게임 세션 연결 — ✅
- **1.6** 인게임 진입(입장·이동·결정론 스폰·전원입장 게이트·HUD·로비 복귀) — ✅
- **1.7** 인프라(PostgreSQL·Redis·Docker·Serilog+Graylog·Testcontainers·E2E) — ✅
- **1.8** GAS 기반(Attribute·GameplayEffect·ASC·버프/디버프 서버 동기화 EF-1·EF-2) — ✅

### 2. 캐릭터 시스템
- **2.1** 전투 코어 — 서버 권위 Attack/Hit/Damage (GAS) — 🔄 | T1 | 🔵
- **2.2** 스킬/어빌리티 — SkillTimeline, `basic_swing` 외 확장 — 🔄 | T1 | 🔵
- **2.3** 진행/성장(Progression) — 레벨·경험치·스탯 성장 영속 (`Progression` 신규) — ⬜ | T1 | 🟢
- **2.4** 스탯 산식 — 레벨/장비/버프 합산 서버 권위 재계산 — ⬜ | T2 | ⚪
- **2.5 사망/부활**
  - **2.5.1** 사망 처리 — HP 0 다운/리스폰 (전투 루프 필수) — ⬜ | T1 | 🔵
  - **2.5.2** Co-op 부활 — 다운된 아군 살리기 — ⬜ | T2 | ⚪
- **2.6 전투 보조**
  - **2.6.1** 회피/구르기(Dodge) — 무적 프레임·모션 (입력 `DodgePressed` 존재) — ⬜ | T2 | ⚪
  - **2.6.2** 상태이상/CC — 스턴·슬로우·넉백 (GAS 태그/이펙트 확장) — ⬜ | T2 | 🔵
  - **2.6.3** 타겟팅/락온 — ⬜ | T2 | ⚪
- **2.7** 스킬 트리/습득·강화 — ⬜ | T3 | ⚪
- **2.8** 직업/클래스 — ⬜ | T3 | ⚪

### 3. 아이템 / 경제 시스템
- **3.1 인벤토리(Inventory)** — `Inventory` 신규 도메인(서버 권위 영속) — ⬜ | T1 | 🟢 ← **즉시 착수 후보**
  - **3.1.1** Domain 엔티티 — `Item`(정의)·`InventoryItem`(소유)
  - **3.1.2** Application — `IInventoryService`/`Service`·`IInventoryRepository`·`ItemGrantResult`
  - **3.1.3** Infrastructure — `InventoryRepository` (PostgreSQL + Redis Cache-Aside+Delete, `AsNoTracking`)
  - **3.1.4** EF 마이그레이션 + DbContext 등록 + DI Installer
  - **3.1.5** `inventory.proto` (GetInventory/획득 알림) + 클라 Generated 재생성
  - **3.1.6** 단위 + Testcontainers 통합 테스트(캐시 무효화 계약)
- **3.2** 장비(Equipment) — 착용 슬롯·장비 스탯 모디파이어 → 2.4 합산 — ⬜ | T2 | 🟢
- **3.3** 루트/드랍(Loot) — 드랍 테이블·보상 산정 — ⬜ | T2 | ⚪
- **3.4** 재화(Wallet) — 골드 보유·증감(서버 권위) — ⬜ | T2 | 🟢
- **3.5** 상점(Shop) — 구매/판매·가격·재고 — ⬜ | T2 | ⚪
- **3.6** 강화/크래프팅 — ⬜ | T3 | ⚪
- **3.7** 아이템 등급/레어도 + 도감 — ⬜ | T2 | 🟢
- **3.8** 소모품/포션 — HP/MP 회복 (인벤토리 소비 → GAS 효과) — ⬜ | T2 | ⚪

### 4. 콘텐츠 시스템
- **4.1 몬스터**
  - **4.1.1** 패킷 `S_SpawnMonster`(1810)/`S_MonsterState`(1811)/`S_MonsterDead`(1812) + Union — ⬜ | T1 | ⚪
  - **4.1.2** 서버 `MonsterManager` — `monsterSpawns[]`(선반영) 결정론 스폰 — ⬜ | T1 | 🟢
  - **4.1.3** 서버 AI 틱 — 추적/공격(권위) → `S_MonsterState` — ⬜ | T1 | ⚪
  - **4.1.4** 서버 히트/사망 판정 → `S_MonsterDead` (GAS 합류) — ⬜ | T1 | 🔵
  - **4.1.5** 클라 `MonsterEntity` 스폰/보간/사망 (`CharacterSpawner`·`NetworkCharacter` 재사용) — ⬜ | T1 | ⚪
  - **4.1.6** 몬스터 웨이브/스폰 페이즈 — ⬜ | T2 | ⚪
- **4.2 던전 클리어/보상(DungeonResult)** — `DungeonResult` 신규 도메인
  - **4.2.1** 패킷 `S_DungeonClear`(1820) + Union — ⬜ | T1 | ⚪
  - **4.2.2** SocketServer 클리어 감지 → `DungeonClearMessage` → Redis Stream — ⬜ | T1 | 🟢
  - **4.2.3** GameServer `DungeonResultConsumer` → 보상 산정(경험치/아이템) — ⬜ | T1 | 🟢
  - **4.2.4** 보상 지급 — 3.1 Inventory + 2.3 Progression 호출(Outbox 원자화) — ⬜ | T1 | 🟢
- **4.3** 던전 메타 — `DungeonRoom.DungeonId` 추가(=9.2 부채) — ⬜ | T1 | 🟢
- **4.4** 퀘스트(Quest) — 수주/진행/완료·보상 (`Quest` 신규) — ⬜ | T2 | ⚪
- **4.5** NPC/대화(Dialogue) — 상호작용·대화 트리 (`Npc` 신규) — ⬜ | T2 | ⚪
- **4.6 월드/존(World)** — 오픈월드 PVE 맛보기 (`World` 신규)
  - **4.6.1** 존 맵·존 전환·포탈 — ⬜ | T2 | ⚪
  - **4.6.2** 텔레포트/패스트트래블 — ⬜ | T2 | ⚪
- **4.7 상호작용 오브젝트** (`IInteractable` 확장)
  - **4.7.1** 문/상자/채집 노드·파괴 가능 오브젝트 — 🔄 | T2 | ⚪
  - **4.7.2** 함정/환경 기믹 — ⬜ | T3 | ⚪
- **4.8** 보스/특수 몬스터 — ⬜ | T3 | ⚪

### 5. 소셜 / 멀티플레이
- **5.1** 파티 — 방=파티(현 로비 일부 충족) + 인게임 파티 UI — 🔄 | T2 | ⚪
- **5.2** 인게임 핑/이모트 — Co-op 의사소통 — ⬜ | T2 | ⚪
- **5.3** 플레이어 간 거래(Trade) — ⬜ | T3 | ⚪
- **5.4** 길드/클랜 — ⬜ | T3 | ⚪
- **5.5** 랭킹/리더보드 — ⬜ | T3 | ⚪
- **5.6** 친구/소셜 — ⬜ | T3 | ⚪

### 6. 메타 / 영속
- **6.1** 캐릭터 진행 영속 — 레벨·인벤토리·장비 DB 영속 (2.3/3.1/3.2 합류) — ⬜ | T1 | 🟢
- **6.2** 던전 결과 → 로비 복귀 — 결과 처리 + 기존 던전→Main 복귀 재사용 — ⬜ | T1 | ⚪
- **6.3** 설정/옵션 — 그래픽·사운드·키 바인딩 + 영속 — ⬜ | T2 | ⚪
- **6.4** 재접속/세션 복구 — 인게임 끊김 복구 — 🔄 | T2 | ⚪
- **6.5** 통계/플레이 기록 — ⬜ | T3 | ⚪
- **6.6** 도전과제/업적 — ⬜ | T3 | ⚪
- **6.7** 튜토리얼/온보딩 — ⬜ | T3 | ⚪

### 7. UI / UX (클라 프레젠테이션 — MVI, View는 자기 Model만 참조)
- **7.1** 결과/보상 화면 (대응 6.2/4.2) — ⬜ | T1 | ⚪
- **7.2** 인벤토리/장비 UI (3.1/3.2) · **7.3** 캐릭터 정보/스탯창 (2.3/2.4) — ⬜ | T2 | ⚪
- **7.4** 퀘스트 UI/추적 HUD (4.4) · **7.5** 대화 UI (4.5) · **7.6** 상점 UI (3.5) — ⬜ | T2 | ⚪
- **7.7** 미니맵 HUD (4.6) · **7.8** 설정 메뉴 (6.3) — ⬜ | T2 | ⚪

### 8. 오디오
- **8.1** BGM/환경음 — ⬜ | T3 · **8.2** 전투 SFX/타격감(HitStop 연계) — ⬜ | T2 · **8.3** UI SFX — ⬜ | T3

### 9. 인프라 / 품질 / 기술 부채
- **9.1** SocketServer IP 하드코딩 → appsettings.json — ⬜ | 높음 | 🟢
- **9.2** `DungeonRoom.DungeonId` 부재 (= 4.3) — ⬜ | 중간 | 🟢
- **9.3** Auth: Binding 실패 시 세션 강제 만료 미구현 — ⬜ | 중간 | 🟢
- **9.4** `Room.Leave` 시 `_playerStates` 정리 누락 — ⬜ | 낮음 | ⚪
- **9.5** Redis Consumer name `socket-1` 고정 → 동적 생성 — ⬜ | 낮음 | ⚪
- **9.6** `GetRooms` count/페이징 정책 — ⬜ | 낮음 | ⚪
- **9.7** status.md stale → plan.md 일원화 — ⬜ | 낮음 | 🟢
- **9.8** 부하 테스트 + 전체 E2E 회귀 자동화 — ⬜ | 마감 | ⚪
- **9.9** 배포 문서 + 포트폴리오 챕터 마감 — ⬜ | 마감 | ⚪

---

## 🟢 세션 트랙 — 서버 도메인(이 세션) vs 🔵 GAS 세션

GAS 세션(2.*·4.1.4)과 **파일·패킷 충돌 없이 병행** 가능한 서버 도메인 트랙:

```
3.1 Inventory ──→ 2.3 Progression ──→ 3.4 Wallet ──→ 4.2 DungeonResult(보상지급)
      │                                                      │
      └──→ 3.2 Equipment (2.4 스탯합산)            6.1 영속 ◀─┘
(+ 부채 9.2 DungeonId · 9.1 IP · 9.3 세션만료 동반 해소)
```

**즉시 착수 후보**: **3.1 인벤토리 도메인**(3.1.1 엔티티부터). 모든 보상/장비/상점/루트의 공통 전제 — 서버 권위 영속 + Cache-Aside 쇼케이스.

---

## 📋 작업 순서 (M1 → M6, 실행 정렬 로드맵)

> 각 단계 = **서버 + 클라 + 테스트(TDD)** 3축, 끝에 **MPPM 2-client** 검증.
> Character 아키텍처(CA)는 의존성에 맞춰 M1(합성/Driver)·M2(GAS)·M5(툴)에 통합.
> **안 함**: ECS/DOTS·Unity Timeline 전투구동·SO를 데이터 진실원·fixed-point/롤백(Co-op)·재작성.

### ✅ M1 — 인게임 진입: "던전에서 서로 움직임이 보인다" (완료) [WBS 1.6]
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
- [x] 전원 입장 → 인게임 진입(로딩 게이트 + Fader) — 기존 `S_GameStatus(InProgress)` 재사용(새 패킷 X): `GameStatusPacketHandler`→`ISocketPacketState.OnDungeonReady`→ ① `InGameModel.IsDungeonReady` ② `GameSessionConnector`가 `LoadSceneAsync(holdUntil)`로 **전원 입장까지 Loading 유지("다른 플레이어를 기다리는 중…") → 완료 시 Fader reveal**(30s 타임아웃 가드). 흐름 로그 全 hop. **E2E `SocketE2ETests` 8/8** + EditMode 릴레이 5/5 + `GameSessionConnectorTests` 3/3
- [x] **MPPM 2-client** "서로 보임·이동" 검증 — 선행 블로커 2건 ✅해소([mppm-testing.md](mppm-testing.md)): ① 서버 이미지 리빌드(E2E 8/8 그린) ② `MainLifetimeScope` DI 누락 수정. 데이터 선행 ✅ → **MPPM 2-창 시각 검증 완료(사람 조작, 2026-06-03)**. **M1 전체 완료.**

### 🔄 M2 — 전투 코어 (Character 두 축 리팩터 동반) [WBS 2.1·2.2·2.5.1·2.6.2]
선행: M1. 상세 기획: [effect-system.md](effect-system.md)

**Effect/버프 시스템 — 1단계 (클라 단독·sync-ready, M1 중에도 착수 가능)**
- [x] **EF-1a**: Attribute 재구조 — Resource vs Stat(`EAttributeKind`) 분리, `SetCurrent`(파생·정수 `GameplayEffectMath.Aggregate`). 즉발은 Resource만 영구 변경
- [x] **EF-1b**: `GameplayEffectDefinition`/`ActiveGameplayEffect`/`EEffectCategory`/`EDurationPolicy`/`EStackPolicy`/`GameplayEffectCatalog`(string id 시드) — 순수 데이터, Sprite 없음
- [x] **EF-1c**: ASC `ApplyEffect`/`RemoveEffect`/`Tick`(만료) + 추가/제거/만료 시 `RecalculateStats` → `OnActiveEffectsChanged`, `GetActiveEffectSnapshots`
- [x] **EF-1d**: `EffectIconCatalog`(Presentation SO·표시전용·카테고리→Sprite+polarity 색) + `BuffView` DTO + `InGameState.Buffs` + `InGameModel` 중계
- [x] **EF-1e**: `GameHud` 버프슬롯 풀 렌더 + `BattleEffectSlot.Bind`+로컬 카운트다운. EditMode 122/122 + PlayMode(슬롯 렌더·HP/MP) 통과
- [x] **EF-1f**: `EffectIconCatalog` 에셋(`GameData/Resources/Effects/`) — fg4_icons PSD 멀티스프라이트 + 버프/디버프 색. `DungeonLifetimeScope` Resources 폴백 자동 로드. **UI Toolkit 커스텀 인스펙터**

**Effect/버프 시스템 — 2단계 (M2 합류·서버 권위)**
- [x] **EF-2a**: `Shared.Gameplay`(netstandard2.1) 결정론 코어 — enums/`GameplayAttributeModifier`/`GameplayEffectMath.Aggregate`/`EffectTiming`. xUnit golden **9/9**(클라 EF-1 parity). sln 등록
- [x] **EF-2c (서버)**: `S_ApplyEffect`/`S_RemoveEffect` 패킷(`Shared.Packet`, Union **1640/1641**) — StartTick+EffectId만. SocketServer.Tests **11/11**
- [x] **EF-2b**: DLL 단일 소스 — `Shared.Gameplay.dll`을 `Client/Assets/Plugins/`에 배치, 클라 중복 순수 8개 삭제. DLL 타입을 클라와 **동일 ns**로 둬 클라 코드 수정 0. EditMode **122/122**
- [x] **EF-2c (클라)**: 패킷 미러 — `ClientCodegen` 재생성으로 클라 반영(`Network/Socket/Packets/EffectPacket.cs`). 회귀 122 통과
- 🔄 **EF-2d** (서버 전투 CA-1/3 선행 — 대부분 combat-gated):
  - [x] **ASC 서버-권위 적용 시드**: `ApplyEffectAuthoritative(def, instanceId, stacks)` — 서버 InstanceId 키·멱등 재적용·`RemoveEffect(serverId)`. EditMode 124/124.
  - [x] **서버 emit(테스트 등급)**: `CombatHandler.HandleAttack`(`C_Attack`→디버프 `S_ApplyEffect` 방 브로드캐스트). SocketServer.Tests **13/13**
  - [x] **수신 핸들러 배선(클라)**: `SocketEffectApply`+`Effect{Apply,Remove}PacketHandler` → `EffectReceiver`(카탈로그+`AuthSession` 라우팅). EditMode **127/127**
  - [x] **E2E 2-클라 검증**: `SocketE2ETests` — A `C_Attack`→`S_ApplyEffect`→B 수신 **1/1**
  - [ ] **남은 정밀화**: 공유 시계(서버 tick, StartTick 정밀 만료) + 클라 예측/정정 + 원격 캐릭터 ASC 라우팅 — CA-3 합류 시

**Character 리팩터 + 서버 권위 전투**
- [x] **CA-1**: 두 축 분리 — Action을 Locomotion FSM에서 들어냄. **`CharacterAgent` 무접촉**. EditMode **129/129**.
  - [x] **Attack (증분1)**: `AttackState`/`StateKind.Attack` 제거. 발동=`PlayerCharacterAgent.HandleAttackInput`(`ConsumeAttackPressed`→히트리셋+애니). 데미지는 기존 GAS 체인 유지. `LocomotionStateMachineTests` 추가.
  - [x] **Interact (증분2)**: `InteractState`/`StateKind.Interact` 제거. 발동=`HandleInteractInput`(`ConsumeInteractPressed`→`InteractionDetector.CurrentInteractable.Interact(gameObject)`). 실작동 구 Character-ns 경로 유지.
  - [x] **규칙갱신**: `.claude/rules/unity-gameplay-state.md` → "두 축 분리"로 갱신.
  - [ ] **별도 정리(후속)**: `Game.Gameplay.Input.InteractionSystem`(휴면 중복) 제거 또는 일원화 결정. 아이템 인벤토리(3.1) 합류 시 instigator 흐름 확정.
- [x] **CA-2**: `Shared.Gameplay`에 SkillTimeline 스키마 + 결정론 코어 — `SkillTimeline`·`SkillTimelineMath`·`HitboxMath.Overlaps`·`SkillCatalog`(`basic_swing`). 적중=`OnHitEffectIds`→GE→EF-2d `S_ApplyEffect` 재사용. xUnit **17/17**.
- ✅ **CA-3**: BasicAttack end-to-end — 서버 권위 판정 + GE 데미지, 클라 송신/연출/HitStop (정밀화는 §197 후속)
  - [x] **증분① 서버 권위 적중 판정**: `CombatHandler` 업그레이드 — `C_Attack`→Room 위치/yaw로 `SkillCatalog`+`HitboxMath.Overlaps` 적중 재계산(권위) → `basic_attack_dmg` `S_ApplyEffect` 브로드캐스트. SocketServer.Tests 15/15 + E2E 1/1.
  - [x] **증분②a 클라 데미지 적용**: 클라 `GameplayEffectCatalog`에 `basic_attack_dmg`(Instant Health -10) → `EffectReceiver` 수신 시 ASC Health 즉발 감소. EditMode **130/130**.
  - [x] **증분②b 인게임 송신+이중데미지 제거+HitStop**: 공격 시 **C_Attack 송신**(`CombatSyncSender`+`OnAttackPerformed`+`CharacterSpawner.AttachCombatSyncSender`) + 로컬 권위 데미지 제거(데미지는 서버 `S_ApplyEffect`만). **HitStop**=`HitStopController`(per-actor `Animator.speed`) → `PlayerCharacter.prefab` 루트 부착. EditMode **131/131**.
  - [x] **증분③ 2-client 공격·피격 동기화 검증**(E2E): 신선 Docker 서버 대상 `SocketE2ETests` **8/8 그린** — combat 케이스(`RawSocket_호스트가_정면의_게스트를_공격하면_서버권위_적중_S_ApplyEffect`: 호스트 정면 1유닛의 게스트 공격 → 서버 hitbox 판정 → 게스트 `S_ApplyEffect{basic_attack_dmg}` 수신) 포함. ※MPPM 2-창 시각 검증(HitStop 연출)은 사람 조작 영역.
  - [x] **증분④ 죽은 ability 클러스터 정리**(YAGNI): 서버 권위 이관으로 미사용·미부착이던 로컬 GAS *ability* 경로 삭제 — `CharacterHitEventReceiver`·`HitDetector`(+프리팹 `AttackPoint`)·`BasicAttackAbility`·`Ability`·`AbilityActivationContext`·`BasicAttackAbilityTests` + ASC ability 멤버. GAS *effect*(ASC.ApplyEffect·GameplayEffect·버프)는 유지. 컴파일 0 + EditMode **128/128**(ability 테스트 3개만 감소). codemap §2.7.

### M3 — 몬스터 [WBS 4.1]
선행: M2.
- [ ] 패킷 — `S_SpawnMonster`/`S_MonsterState`/`S_MonsterDead`(1810~) + Union [4.1.1]
- [ ] 서버 `MonsterManager` — `monsterSpawns[]`(선반영) 결정론 스폰 [4.1.2]
- [ ] 서버 AI 틱(추적/공격, 권위) → `S_MonsterState` 브로드캐스트 [4.1.3]
- [ ] GAS 피격/사망 동기화 → `S_MonsterDead` [4.1.4]
- [ ] 클라 `MonsterEntity` 스폰/보간/사망 (`CharacterSpawner`·`NetworkCharacter` 재사용) [4.1.5]

### M4 — 던전 루프 완성 (= DoD) [WBS 2.3·3.1·4.2·4.3·6.1·6.2·7.1]
선행: M3. **이번 세션(서버 도메인 🟢) 핵심 영역.**
- [ ] **인벤토리 도메인** — Item/InventoryItem·Service·Repo(Cache-Aside)·proto·테스트 [3.1] ← 즉시 착수
- [ ] **진행/성장(레벨·경험치) 도메인** [2.3]
- [ ] **던전 클리어/보상(DungeonResult)** — 클리어 감지→Stream→Consumer 보상 산정·지급 [4.2]
- [ ] 던전 구분 — `DungeonRoom.DungeonId` [4.3 / 부채 9.2]
- [ ] 캐릭터 진행 영속(레벨/인벤/장비) [6.1] + 결과/보상 화면→로비 복귀 [6.2 / 7.1]
- [ ] 완전한 Co-op 1판 루프 E2E (MPPM 2-client)

### M5 — 폴리시 + PVE 맛보기 [WBS 2.4·2.6·2.7·3.2~3.8·4.4~4.7·5.*·6.3~6.4·7.2~7.8·8.*]
- [ ] 애니메이션(MotionMatching V2 액션 블렌딩, 🟣)·HUD 다듬기·스킬1~2·아이템 최소·사운드(8.*)
- [ ] 장비/루트/재화/상점/소모품 [3.2~3.8] + 관련 UI [7.2~7.8]
- [ ] 전투 보조(회피·CC·타겟팅·Co-op 부활) [2.6·2.5.2]
- [ ] **CA-5**: Skill Timeline 에디터 툴(공유 JSON read/write) [2.7]
- [ ] PVE 오픈월드 맛보기 — 월드/존·퀘스트·NPC/대화·상호작용 [4.4~4.7]
- [ ] 소셜(핑/이모트)·설정/옵션·재접속 [5.2·6.3·6.4]

### M6 — 마감 [WBS 9.8·9.9]
- [ ] 데모 영상/gif·부하/E2E 검증·배포/포트폴리오 문서

---

## ✅ 완료된 Phase (= WBS 1.* 상세)

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

- 전체 현황: [`docs/wiki/status.md`](status.md) (※ 일부 stale — 부채 9.7)
- 코드맵 + 설계 결정 로그: [`docs/wiki/codemap.md`](codemap.md)
- 패킷 규칙: [`docs/wiki/packets.md`](packets.md)
- SocketServer 규칙: [`docs/wiki/socketserver.md`](socketserver.md)
- 서버 흐름: [`docs/wiki/gameflow.md`](gameflow.md)
- Effect/버프 시스템: [`docs/wiki/effect-system.md`](effect-system.md)

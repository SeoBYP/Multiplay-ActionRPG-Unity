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
| SocketServer(TCP/방/세션) | `ServerAll/SocketServer/SocketServer/{Room,Session,PacketHandler}` | [socketserver.md](socketserver.md) |
| Redis 스트림/큐 | `Shared/Shared.Infrastructure/MessageQueue/`, `Messages/` | [redis.md](redis.md) |
| **루트/드랍(던전 경로)** | 드랍/줍기 = `SocketServer/Loot/`(DropTable·GroundItem)·`Handler/{CombatHandler.SpawnDrops,LootHandler}`·`Room`(GroundItem·TryPickup) / 지급 = `GameServer.Infrastructure/Common/{Consumer/LootGrantConsumer,MessageQueue/LootPickupMessageQueue}` → `IInventoryService.GrantItemAsync`. 아래 §2.16. **Main(싱글) 경로 지급 = `GameServer.API/Services/InventoryGrpcService.GrantItem`(gRPC+가드, §2.18)** | [loot-drop.md](loot-drop.md) |
| 클라 gRPC | `Client/Assets/Script/Network/Https/` | [unity-client.md](unity-client.md) |
| 클라 소켓 | `Client/Assets/Script/Network/Socket/` | `.claude/rules/networking.md` |
| 클라 MVI 모델 (타이틀·로비·인게임) | `Client/Assets/Script/Presentation/{Title,DungeonLobby,InGame}` (asmdef `Game.Presentation`, ns `Game.Presentation.*`) — GUI가 바인딩하는 MVI 모델 레이어 | `.claude/rules/unity-client.md` |
| 클라 게임플레이/캐릭터/입력 | `Client/Assets/Script/Gameplay/` (asmdef `Game.Gameplay`, ns `Game.Gameplay.*`) — Character(`CharacterSpawner`·`MoveSyncSender`·`RemoteDriver`)·Camera·**Input**·**Spawn**(결정론 리졸버, §2.3) | `.claude/rules/unity-gameplay-state.md` |
| 스폰 레이아웃/맵(서버·클라 공용) | 진실원 `MapDefinition`(SO, `Gameplay/Spawn/`, 에셋 `Assets/GameData/Resources/Maps/`) → **bake** → 서버 `Shared/Shared.Infrastructure/Spawn/spawn-layouts.json`(임베디드)·클라 `Gameplay/Resources/spawn-layouts.json`. 맵 비주얼=`MapLoader`. 툴 `Gameplay/Editor/`: `MapDataExporter`(Export/Import/BakeAll)·`MapEditorWindow`(프리뷰 저작) | 아래 §2.3 |
| 클라 인증 | `Client/Assets/Script/System/Auth/` (ns `Game.System.Auth`) | — |
| 클라 GUI/HUD | `Client/Assets/Script/GUI/` | — |
| 클라 DI(VContainer) | `Client/Assets/Script/VContainer/` | `.claude/rules/unity-client.md` |
| 테스트 하네스 | 아래 §3 | `.claude/rules/testing.md` |

---

## 2. 설계 결정 로그 (왜 — append-only, 최신이 위)

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
  - `LocalCombat`(MonoBehaviour, 플레이어) — `PlayerCharacterAgent.OnAttackPerformed` 구독 → `Physics.OverlapSphere`로 근처 `LocalMonster` 수집 → **서버와 동일 `HitboxMath.Overlaps(SkillCatalog "basic_swing")`** 정밀판정 → `TakeDamage(10)`. `CharacterSpawner` Main 브랜치가 동적 부착(던전은 `CombatSyncSender`).
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
- **9a-2 SO 저작 레이어 완료 2026-06-09**: `Game.Gameplay.Loot.DropTableDefinition`(SO, monsterId별 drops·`Get(monsterId)` 클라 런타임 조회) + `Game.Gameplay.Editor.DropTableExporter`(`Tools/Loot/Export Drop Tables` SO→JSON bake·`Import` 부트스트랩) — MapDefinition/MapDataExporter 동일 컨벤션. **클라는 SO 직접 읽음**(Resources `Loot/DropTableDefinition`) → bake는 **서버 임베디드 `drop-tables.json`만** 기록(SO가 클라 단일 소스, 클라 JSON 미러 불요). Unity 컴파일 0오류. `.asset` 부트스트랩(사용자 `Tools/Loot/Import` 1클릭)으로 `Assets/GameData/Resources/Loot/DropTableDefinition.asset` 생성·검증 완료.
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
- **9.2 보류**(YAGNI): `DungeonRoom.DungeonId`는 B트랙 MapId 카탈로그 우회와 정합 — 다중 던전 생기는 M5에 착수.

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
- **클라(Presentation)**: codegen 미러 `S_DungeonClear{RoomId,RewardExp}` → `DungeonClearPacketHandler` → `ISocketPacketState.MarkDungeonCleared(exp)`/`OnDungeonCleared(long)`(`SocketApiClient.cs`) → `InGameModel` 구독 → `InGameResult.DungeonCleared(exp)`→`InGameState.IsDungeonCleared`+`RewardExp`→`GameHud`가 `DungeonClear` 패널 활성+`SetReward`. 패널 자체 return 버튼/기존 `returnToLobbyButton` 모두 `InGameIntent.ReturnToLobby`(§2.1 복귀=`LoadSceneAsync("Main")`) 재사용.
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
- **잔여 부채**: 회복 수치 이중정의(클라 ConsumableCatalog SO +100 vs Shared potion_hp_small +100) — Main도 Shared로 수렴하면 단일소스(후속). ~~EffectReceiver 진단로그~~(제거 완료). E2E 2종 Unity 실행 **그린 확인(사용자, 2026-06-11)**.

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
- **저작 레이어(SO + Export 툴)**: 스폰 데이터의 진실원은 **`MapDefinition`(SO, 맵당 1개, `Gameplay/Spawn/`, 에셋 `Assets/GameData/Resources/Maps/{mapId}.asset`)** — 디자이너가 편집. 필드: `mapId`, **`visualPrefab`(맵 배경 모델, 클라 전용)**, `playerSpawns[]`(추후 monsterSpawns 합류). 스폰 좌표 런타임은 SO 직접 아닌 **bake된 JSON**(서버는 UnityEngine 의존 0이라 SO 불가 → JSON이 유일 교환 포맷, parity 자명).
  - **맵 비주얼**: `MapLoader`(IAsyncStartable, `Gameplay/Spawn/`)가 Dungeon 진입 시 서버 mapId 대기 → `Resources.Load<MapDefinition>("Maps/{mapId}")` → `visualPrefab` 인스턴스화. `DungeonLifetimeScope`에 등록. 프리팹은 JSON에 안 들어가므로(서버 무관) **이 경로만 SO 직접 읽음**.
  - **툴**(`Game.Gameplay.Editor`): `MapDataExporter` — 메뉴 `Tools/Spawn/Export Map Data`(SO→JSON, 클라 Resources + 서버 임베디드 동시 기록; 서버는 재빌드 시 반영) + `Import Map Data from JSON`(JSON→SO 부트스트랩) + `BakeAll()`(다이얼로그 없는 재사용 bake). 에셋 위치 `Assets/GameData/Resources/Maps/{mapId}.asset`.
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

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

### 2.13 기술 부채 정리 1차 (9.1·9.3~9.7, 2026-06-07)
- **9.6 GetRooms N+1 제거**: 방 목록이 방마다 `GetPlayersByRoomIdAsync`+`GetByIdsAsync` 2왕복(2N)이던 것을 → `IDungeonRoomPlayerRepository.GetPlayersByRoomIdsAsync(roomIds)`(단일 AsNoTracking 쿼리) + 유저 1쿼리 배치로 축소. 조립은 `DungeonRoomExtensions.ToRoomInfo(this DungeonRoom, IReadOnlyList<User>)` **동기 오버로드**(추가 I/O 0). **단일 방 응답(생성/입장)은 기존 async `ToRoomInfo(repo,repo)` 유지** — 1방은 N+1 아님. `FakeDungeonRoomPlayerRepository`도 동일 메서드 구현. count/페이징은 proto(공개계약) 변경이라 보류. 위치: `GameServer.API/Services/DungeonLobbyGrpcService.GetRooms`, `GameServer.API/Extensions/DungeonRoomExtensions.cs`, `GameServer.Infrastructure/.../DungeonRoomPlayerRepository.cs`.
- **9.4 Room.Leave PlayerState 정리**: `SocketServer/Room/Room.Leave`가 `_playerSessions`만 지우고 `_playerStates`(userId→PlayerState)를 남겨 떠난 플레이어가 AI 타깃·위치 계산에 유령 잔류하던 버그. Leave가 제거 직전 `session.UserId`를 잡아 `_playerStates.Remove`. 테스트 `RoomManagerLeaveRoomTests.퇴장한_플레이어의_PlayerState는_정리된다`.
- **9.5 Consumer name**: `GameStartRequestedMessageQueue.ConsumerName` `socket-1`(상수) → `socket-{Environment.MachineName}`(static readonly). 수평 확장 시 PEL 추적 충돌 방지, 컨테이너 hostname 안정적이라 재시작 PEL 복구 유지.
- **9.1 SocketServer 설정 가시성**: IP는 이미 `ServerOptions`(Server 섹션·env)로 구성됨(코드 변경 0). `appsettings.json`에 `Server` 블록 명시 + docker `AdvertiseIp`에 "원격 배포 시 호스트 IP 교체" 주석.
- **9.3 단일 세션 강제 = 이미 구현 확인**(무변경): `UserSessionRepository.CreateSessionAsync`가 로그인 시 기존 세션 DB+캐시 제거, refresh 바인딩 실패도 세션 제거. 부채 설명이 stale이었음.
- **9.7 status.md**: stale 226줄 → plan.md/codemap 포인터 문서로 축소(진실원 일원화).
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

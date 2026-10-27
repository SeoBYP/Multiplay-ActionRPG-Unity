# 결함·정리 백로그

> **미해결만 둔다.** 해소된 항목은 지운다. 이력은 `git log` 와 [codemap.md](codemap.md) §2 에 있다.
> 각 항목엔 **위치 · 무엇이 깨지나(시나리오) · 선행 결정**을 적는다. 근거 없는 "정리하면 좋겠다"는 넣지 않는다.
> 진척은 [plan.md](plan.md) 가 관리한다.
>
> 근거 표기: **✔ 실측** = 코드를 직접 읽었거나 실패 테스트로 재현 · **리뷰** = 2026-10-14 리뷰 에이전트 보고, 재현 전
> ID 접두사: `G` GameServer · `S` SocketServer · `C` Unity 클라 · 그 외(A·B·D·F)는 기존 항목
> 마지막 갱신: 2026-10-15

---

## P0 — 즉시: 악용되거나 데이터가 사라진다 (RED 테스트 있음)

작업 방식: 테스트는 이미 있다. **사용자가 구현하고, 테스트가 그린이 되면 닫는다.** 아래 "수정 가이드"는 방향이며 정답 코드가 아니다.

### G1. GameServer 기동마다 Redis 전체 FLUSH — ✔ 실측

- **위치**: `GameServer.API/Installers/ServiceInstaller.cs:31-43` — `#if DEBUG` 밖에서 `server.FlushAllDatabases()`
- **시나리오**: GameServer 재시작 → ① 미소비 보상 스트림 + 컨슈머 그룹 PEL 삭제 → 보상 영구 미지급 ② Redis 가 1차 저장소인 Main 위치(`UserPositionRepository`) 소실 ③ SocketServer 가 읽는 `gamesession:player:*` 삭제 → 진행 중 던전 재입장 불가 ④ 세션·직전 refresh 토큰 삭제
- **RED**: `GameServer.Tests/Infrastructure/Integrations/RedisStartupPreservationTests.cs` — `기동이 미소비 보상 스트림을 지웠다.`
- **수정 가이드**: 블록 삭제. `AllowAdmin = true` 도 FLUSH 용이었다면 함께 뺀다. 개발 중 초기화가 필요하면 `docker compose down -v` 로 한다(코드에 남기지 않는다).
- **주의**: 고친 뒤 **Docker E2E 전체** 실행 — "기동 = 깨끗한 Redis" 에 기대던 E2E 가 있을 수 있다(미실측).

### S1. 플레이어가 몬스터·테스트 어빌리티를 발동한다 — ✔ 실측

- **위치**: `SocketServer/PacketHandler/Handler/CombatHandler.cs:52,70` — `AbilityCatalog.Get(skillId)` 가 null 만 아니면 통과
- **시나리오**: `C_Attack{SkillId=105}` → `test_brute_attack`(데미지 9999·쿨다운 50ms) 발동 → 던전 즉시 클리어 → 보상. 109(`leviathan_slam` 90)도 동일. 쿨다운이 어빌리티별이라 서로 다른 SkillId 를 동시에 보내면 전부 맞는다.
- **RED**: `SocketServer.Tests/Combat/AttackAbilityTrustTests.cs` — 대조군(SkillId 0) 그린, 105·109·100 RED(`Expected: 40 / Actual: 28`)
- **수정 가이드**: 핸들러가 `ResolveAbility` 직후 "플레이어 어빌리티인가"를 한 줄로 거른다. 판별 기준이 선행 결정이다.
  - 최소안: 현재 카탈로그는 플레이어 = networkId 0~5, 몬스터 = 100~ 로 나뉘어 있다 → `AbilityDef` 에 `IsPlayerAbility` 를 **한 곳**에 정의하고 `AbilityCatalogTests` 에 "몬스터 어빌리티는 전부 100 이상" 가드 추가.
  - 정석안: Ability SO 에 소유 구분 필드 → bake JSON → `AbilityDef`. 데이터 주도지만 클라 SO·Exporter·Shared 를 함께 바꾼다.
  - 덤: `test_brute_attack` 이 배포 카탈로그에 들어 있는 것 자체도 의문 — 테스트 전용이면 테스트 데이터로 옮긴다.

### S3. `C_Move` 를 검증하지 않는다 (NaN · 순간이동) — ✔ 실측

- **위치**: `MovementHandler.cs:22` → `ActorStore.SetPosition` 그대로. 거리 비교 `GroundItemStore.cs:65` · `DungeonProgress.cs:133` 은 `d² > r²` 형태
- **시나리오**: `PosX=NaN` → 모든 `NaN > r²` 가 false → **"범위 밖" 검사를 항상 통과** → 맵 어디서든 줍기·부활. 좌표를 몬스터 옆으로 보내 순간이동 공격. 서버가 건 슬로우·스턴도 이동엔 적용되지 않는다.
- **RED**: `SocketServer.Tests/Room/MoveInputValidationTests.cs` — `서버 위치가 (NaN, 0) 가 됐다` · `한 패킷에 707m 이동이 그대로 받아들여졌다` · NaN 으로 30m 밖 아이템 줍기 성공
- **수정 가이드**:
  1. 핸들러 입구에서 `float.IsFinite` 4개(x·y·z·rotY) 아니면 버린다.
  2. 이동 거리 상한: `직전 위치 ~ 새 위치 거리 ≤ 최대속도 × 경과시간 + 허용치`. 경과시간은 **서버 수신 시각** 기준(클라 TimeStamp 는 신뢰 경계 밖). 넘으면 거부하거나 직전 위치로 정정. 최대속도 값이 선행 결정(스프린트·회피 대시 포함).
  3. `room.Bounds` 로 clamp — 단 `Room` 기본값이 `MapBounds.Unbounded` 라 이것만으론 테스트가 안 풀린다(2번이 본체).
  4. 심층 방어: 거리 비교를 `!(d² <= r²)` 로 쓰면 NaN 도 "범위 밖"이 된다.

### G3. 경제 쓰기 경로의 lost update / 이중 차감 — ✔ 실측

- **위치**: `WalletRepository.cs` `AddBalanceAsync`·`TrySpendBalanceAsync` · `InventoryRepository.cs` `AddQuantityAsync`·`RemoveQuantityAsync` · `ProgressionRepository.AddExpAsync`. 엔티티 설정 전체에 동시성 토큰 0건, 원자 UPDATE 0건.
- **시나리오**: 전부 "추적 쿼리로 읽기 → 메모리에서 검사·증감 → SaveChanges". 동시 요청 두 개가 같은 값을 읽고 각자 덮어쓴다.
- **RED**: `EconomyConcurrencyTests.cs` (10개 동시)

  | 테스트 | 기대 | 실제 |
  |---|---|---|
  | 잔액 100에서 100골드 결제 10회 | 성공 1 | **10** |
  | 골드 +10 × 10 | 101 | **11** |
  | 5개 보유, 1개씩 차감 10회 | 성공 ≤ 5 | **10** |
  | 아이템 +1 × 10 | 11 | **2** |
  | Exp +5 × 10 | 50 | **10** |

- **수정 가이드**:
  - 지갑·인벤토리 = **조건부 원자 UPDATE** (`ExecuteUpdateAsync`). 차감은 `WHERE balance >= @amt`, 영향 행 0 = 실패. 적립은 `SET balance = balance + @amt`. 행이 없을 때의 lazy create 는 `INSERT … ON CONFLICT DO UPDATE` 또는 "UPDATE → 0행이면 INSERT → UNIQUE 위반이면 UPDATE 재시도".
  - 인벤토리 적립은 `LEAST(quantity + @amt, @maxStack)` — 이때 **실제 적립량**을 돌려줘야 G10(초과분 과금)도 같이 풀린다.
  - Exp 는 레벨업 루프가 도메인(`UserProgression.AddExp`)에 있어 SQL 한 줄로 못 바꾼다 → **낙관적 동시성**: Npgsql `xmin` 을 동시성 토큰으로(그림자 속성으로 두면 도메인 엔티티 무변경) + `DbUpdateConcurrencyException` 시 **다시 읽고 재적용** 재시도. 재시도 없으면 테스트의 "합계 50"이 안 나온다.
  - `ExecuteUpdateAsync` 는 변경 추적기를 우회한다 — 이후 캐시 DEL 은 그대로 유지. 마이그레이션이 생기면 diff 가 비었는지 확인(`xmin` 은 시스템 컬럼).

### G2. 퀘스트 보상 중복 수령 — ✔ 실측

- **위치**: `QuestService.cs:121-141` — `row.Status == Claimed` 를 메모리에서 확인 → `UpsertAsync` → 지급
- **시나리오**: 동시 2요청이 둘 다 Accepted 를 읽음 → 둘 다 Claimed 로 덮어씀(값이 같아 에러 없음) → 둘 다 지급.
- **RED**: `QuestClaimConcurrencyTests.cs` — 10개 동시 → 성공 `Expected: 1 / Actual: 10`
- **수정 가이드**: 수령 마킹을 **조건부 UPDATE 로 원자화** — `WHERE user_id=@u AND quest_id=@q AND status=Accepted AND progress >= @required` 로 Claimed 로 바꾸고, 영향 행이 1 일 때만 지급. 저장소에 `TryMarkClaimedAsync` 같은 메서드 하나가 생긴다. "마킹 후 지급 실패 = 재수령 불가"는 기존 설계 결정(유지). 재시도 안전까지 원하면 그다음 단계로 `IRewardLedger.GrantOnceAsync`(보상 종류별 키).
- 단위 테스트(`FakeQuestRepository`)로는 못 잡는다 — await 사이에 끼어들 틈이 없다. 그래서 RED 는 실제 Postgres 통합 테스트다.

### G4. 착용 장비를 팔거나 소비해도 스탯이 남는다 — ✔ 실측

- **위치**: `EquipmentService.cs` — 소유 검증은 `EquipAsync` 때 한 번뿐. `ShopService.SellAsync` · `InventoryGrpcService.ConsumeItem` 은 착용 여부를 안 본다. `GetEquippedStatsAsync` 도 소유 재확인 없음.
- **시나리오**: 검 착용 → 판매 → 골드 받고 공격력 유지 → 다시 사서 반복. 장비를 `ConsumeItem` 으로 "소비"해도 동일.
- **RED**: `EquippedItemOwnershipTests.cs` — 대조군 그린, 판매·소비 2건 RED
- **❓ 선행 결정**: 
  - (가) **조회 시 소유 필터** — `GetEquippedAsync`·`GetEquippedStatsAsync` 가 인벤토리에 없는 장비를 걸러낸다. 수정 1곳으로 판매·소비·미래의 모든 제거 경로를 덮는다. 대신 "팔면 조용히 해제"가 된다. **권장.**
  - (나) **착용 중 판매·소비 거부** — 사용자에게 명확하지만 Shop·Inventory 가 Equipment 를 알아야 하고, 제거 경로마다 검사를 넣어야 한다.
  - 테스트는 두 방식 모두 통과하도록 **불변식**("보유하지 않은 장비의 스탯은 없다")만 고정했다.

### S2. 입장한 연결이 다른 UserId 로 다시 Join 한다 — ✔ 실측

- **위치**: `RoomJoinLeaveHandler.cs:36-52` — Redis 배정(`gamesession:player:{userId}`)만 확인하고 `session.UserId = packet.UserId` 로 덮어쓴다. 이어지는 `JoinRoom` → `LeaveRoom(sessionId)` 가 **바뀐 UserId 기준**으로 정리한다.
- **시나리오**: 파티원 B 가 자기 연결로 `C_PlayerJoin{UserId=A}` → 연결 신원이 A 로 바뀜 → A 의 세션이 방에서 밀려남(재접속 인수 로직) → B 가 A 의 액터를 조종. userId 는 `S_PlayerJoined`·`S_Move` 로 파티원에게 공개된다.
- **RED**: `SocketServer.Tests/Room/PlayerJoinIdentityTests.cs` (Redis 컨테이너) — `Expected: 200 / Actual: 100`
- **수정 가이드**: 핸들러 입구에서 `session.UserId > 0 && session.UserId != packet.UserId` 면 거부. `session.UserId` 대입은 검증이 전부 끝난 뒤로.
- **❓ 남는 구멍 (공개 계약 변경 필요)**: **새 연결**이 피해자 userId 로 Join 하는 경로는 지금 패킷만으론 정상 재접속(`ReconnectTakeoverTests`)과 구분할 수 없다. 해법 = GameServer 가 `gamesession:player:{userId}` 에 1회용 join 토큰을 함께 쓰고 → `GameSessionEvent`(proto 필드 추가)로 본인에게만 전달 → `C_PlayerJoin` 에 토큰(MemoryPack 필드 **끝에** 추가) → SocketServer 가 대조. proto·패킷 둘 다 바뀌므로 승인 후 테스트를 추가한다.

---

## P1 — 정상 플레이 중 고착·유실 (테스트 먼저 작성 필요)

| ID | 근거 | 위치 | 무엇이 깨지나 | 수정 방향 |
|---|---|---|---|---|
| **S4** | 리뷰 | `DungeonProgress.cs:77-78`, `RoomManager.cs:173`, `Room.Leave` | 전원 다운 판정이 미입장·유예 중 멤버를 '생존'으로 셈. 그 멤버가 나중에 정리돼도 재판정 없음 → 2인 방에서 A 크래시 + B 다운 = 방이 영구 정지 | 판정 대상을 "입장 완료 + 유예 아님"으로. 멤버 제거·유예 만료 시점에도 판정 호출 |
| **S5** | 리뷰 | `RoomManager.cs:235,251,268,289` · `LootPickupMessageQueue.cs:18` | `_ = EnqueueAsync(...)` 로 발행 결과를 버림 → Redis 순단 시 줍기 지급·클리어 보상·퇴장 메시지가 **로그도 없이** 소실 | 최소 = 실패를 관찰해 로깅. 본체 = 재시도(줍기는 이미 바닥에서 제거된 뒤라 유실 = 아이템 증발) |
| **G5** | 리뷰 | `RoomLifecycleConsumer.cs:32-37` | `RemovePlayerFromRoomAsync` 가 `Result.Failure` 를 돌려줘도 정상 리턴 → ACK → 재배달 없음 → association 잔존 → 재로그인 시 방 복원·방 생성 막힘 | 일시 오류 실패는 throw 해서 재배달(at-least-once 규칙). 영구 실패만 로그 후 ACK |
| **C1** | ✔ 실측 | `System/Auth/SessionKeepAlive.cs:75-80` | 토큰 갱신 1회 실패(순단 포함) = keep-alive 영구 종료, 재시작 경로 없음 → 60분 후 모든 RPC Unauthenticated. `RpcException` 도 잡지 않음 | `NeedLogin` 만 종료, 그 외(일시 오류)는 백오프 재시도. 재로그인 시 루프 재기동 |
| **C2** | 리뷰 | `System/DungeonLobby/DungeonLobbyService.cs:190-211` | `SubscribeRoom` 스트림이 끊겨도 재구독 안 함(`IsInRoom=true` 유지) → 방장이 시작해도 멤버는 대기실에 갇힘 | `ChatModel` 의 재연결 루프와 같은 패턴 재사용 |
| **C3** | 리뷰 | `Presentation/DungeonLobby/LobbyModel.cs:243-254` | `StartGameAsync` 에 finally 없음 → 예외·이벤트 미수신 시 `_isProcessing=true` 고정 → 나가기 포함 모든 로비 입력 무시 | try/finally. 성공 경로 해제를 GameSessionReady 에만 의존하지 않기 |
| **C4** | ✔ 실측 | `Presentation/Dialogue/DialogueModel.cs:66, 246` | `Start()` 의 `EnterUi()` 짝이 `Dispose()` 에 없음 → 대화 중 던전 시작 시 루트 `InputContext._uiDepth` 가 1 로 남아 던전에서 이동·공격 불가 | `InGameModel._uiCaptured` 와 같은 패턴 — Dispose 에서 진입 상태면 `ExitUi()` |
| **G7** | 리뷰 | `DungeonLobbyService.cs:112-153, 249-297` · `GameSessionReadyConsumer.cs:69-72` · `DungeonRoomRepository.cs:179` | 방 잠금은 `JoinRoom` 만 사용. 나머지는 캐시 객체로 전체 행 덮어쓰기 → Playing 이 Starting 으로 되돌아가 클라가 던전 입장 못함 / 삭제된 방 association 잔존 | 상태 전이를 조건부 UPDATE 로, 또는 같은 방 락 사용. `UpdateRoomSettings` 는 Starting 도 거부 |
| **G10** | 리뷰 | `ShopService.cs:28-43` · `InventoryRepository.cs:61,67` | maxStack 초과분도 전액 차감(99/99 에서 10개 구매 = 골드만 사라짐). 지급이 예외를 던지면 환불 없음 | G3 에서 실제 적립량 반환 → 그만큼만 과금. 지급 예외도 환불 경로로 |

---

## P2 — 남용·정합성·유지보수 (리뷰, 재현 전)

**GameServer**
- **G6** 채팅 `Reconnect(LastMessageId=0)` → `chat_messages` 전량 LIMIT 없이 로드 + 전부 캐싱 (`ChatMessageRepository.cs:42-52`, `ChatGrpcService.cs:86`). 인증 사용자 누구나 반복 가능한 DoS. → 상한 + 커서 페이징.
- **G8** `StartGame` 의 클라 `mapId` 가 방에 저장된 MapId 보다 우선하는데 `SpawnLayoutTable.IsKnown` 검증 없음 (`DungeonLobbyService.cs:320-322`). → 요청 mapId 를 무시하고 방 값만 쓰는 게 가장 단순.
- **G9** 닉네임 UNIQUE 제약 없음 — 동시 변경 시 중복, 이후 `SingleOrDefaultAsync` 가 항상 예외. 버려진 닉네임을 가져간 사람이 이전 주인의 귓속말을 읽을 수 있음(이력 필터가 닉네임 기준).
- **G11** `UserSessionRepository.cs:274-283` `TouchSession` 이 캐시를 다시 씀(Update 후 DEL 규칙 위반) — 로그아웃 직후 덮어쓰면 폐기된 세션이 30분간 통과.
- **G12** 원장 트랜잭션 안의 저장소가 **커밋 전에** 캐시 DEL (`RewardLedger.cs:47-49`) — 그 틈의 읽기가 커밋 전 값을 30분 캐싱.
- **G13** `OutboxPublisherService` 가 역직렬화 실패 메시지를 processed 로 안 바꿈 → 영원히 재시도, 20건 쌓이면 배치 전체 정지.
- **G14** `DungeonRoom.Create/UpdateRoomSettings` 에 MaxPlayers 상한 없음.
- **G15** `ChatService.cs:7` Application 이 `Microsoft.AspNetCore.Mvc.Filters` 를 using(미사용) — 계층 오염, 지우면 끝.

**SocketServer**
- **S6** 수신 속도 제한 없음 — `C_Move` 초당 수천 개 × N-1 브로드캐스트로 상대 송신 큐(1024)를 채워 **남의 연결을 끊을 수 있다**. `PingPongHandler.cs:10` 이 핑마다 Information 로그.
- **S7** 회피·줍기에 상태 게이트 없음 (`DodgeHandler.cs:18-34`, `LootHandler.cs:28-33`) — 다운·스턴 중 회피·줍기 가능. `IsActivationBlocked` 는 Dead 태그만 봐서 HP 0 ~ `MarkDowned` 사이 틈.
- **S8** `PlayerConsumedConsumer.cs:54-57` — 회복이 적용되지 않아도 `S_ApplyEffect` 브로드캐스트 → 다운 클라 HP 표시와 서버 HP 가 갈라짐.
- **S9** 재접속·늦은 입장 로스터에 활성 Effect(스턴·슬로우) 미포함 (`RoomJoinLeaveHandler.cs:97-127`).
- **S10** `PlayerActor.cs:29-32` 회피 마나 검사·차감이 다른 락 → 사이에 리젠이 끼면 회복분 유실. `TrySpendMana` 사용.
- **S11** `RoomTickService.cs:51-55` try/catch 가 방 전체 루프를 감쌈 → 방 하나의 예외가 그 주기 모든 방의 틱·유예 정리를 건너뜀. → 방 단위 catch.
- **S12** ❓ 미입장·끊김 멤버도 클리어 보상 대상 (`RoomManager.cs:267`). 의도인지 결정 필요.

**Unity 클라**
- **C5** `GameSessionConnector.cs:100,147` 재시도 루프 안에서 `ConnectAsync` 예외 미처리 → 첫 실패에서 재시도 전체 종료.
- **C6** `SocketConnector.cs:40` `TcpClient.ConnectAsync` 에 취소·타임아웃 없음(OS 타임아웃 ~21s).
- **C7** 기본값 생성자 인자 13곳(`InGameModel`·`InventoryModel`·`ShopModel`·`QuestModel`·`EquipmentModel`·`DialogueModel`·`ProgressionModel`·`AuthSession`·`PlayerProgressionHolder`·`QuestService`·`NPCDialogueBinder` 등) — 305d21cd 와 같은 함정. 지금은 전부 등록돼 있어 잠재 결함.
- **C8** `ChatGrpcService.cs:34` `WriteAsync(...).GetAwaiter().GetResult()` 메인 스레드 동기 블로킹 (코드젠 출력 → 템플릿 수정).
- **C9** `GUI/GUIRoot.cs` static `Instance` + DDOL 싱글턴(규칙 위반, 참조 19곳). 지금 누수는 없음.
- **C10** `MainLifetimeScope.cs:101,119` `ProgressionModel` 중복 등록.
- **C11** `DungeonLobbyService.cs:93-106` `LeaveRoomAsync` gRPC 예외 시 `StopSubscription`·`ClearRoom` 미실행.
- **C12** 인터페이스 도입 기준 미충족: `IStateFactory` · `IStateMachineBuilder` · `IWalletService`(구현 1, 테스트 대역 없음). 규칙상 제거 대상.
- 경미: `State => _state.ToReadOnlyReactiveProperty()` 가 읽을 때마다 새 구독 · `InGameModel.Dispose` 가 `_toggleQuest`·`_toggleAbility`·`_interactionPrompt` 미해제.

---

## ❓ 선행 결정이 필요한 기존 항목

### A6 + F12. 공유 데이터 계약의 자리 (한 건으로 처리)

- `EquipmentType`·`ItemGrade`·`ShopCategory`·`DropTable` 같은 **데이터 계약**이 `Shared.Gameplay`(결정론 전투 코어)에 얹혀 있다. Unity 가 읽을 수 있는 공유 DLL 이 `Shared.Gameplay`(netstandard2.1) 하나뿐이라 갈 곳이 없었다. 지금은 `Shared.Gameplay/Contracts/` 에 모아뒀다 — **여기에 새 파일을 늘리지 않는다.**
- `Shared.Infrastructure` 는 실제로는 임베디드 JSON 카탈로그 + 메시지 계약인데 이름 때문에 "Application → Infrastructure 위반"으로 읽힌다(`GameServer.Application.csproj:10`).
- 결정: ① `Shared.Contracts`(netstandard2.1·무의존) 신설 여부 ② `DropTable` 은 계약이 아니라 roll 함수 → Gameplay 잔류 여부 ③ `Shared.Infrastructure` → `Shared.GameData` 개명을 같이 할지(참조 그래프·Dockerfile restore 를 한 번만 건드리게).

### B1. `CharacterMotor.Move` 가 `Time.deltaTime` 을 직접 읽는다
- 상태머신은 deltaTime 을 인자로 받는데 Motor 만 전역 시계. `ActionRootTests` 가 프레임레이트 의존이 됐다(테스트는 우회, 원인 잔존). 시그니처를 바꾸면 이동 감각·튜닝값에 영향 → 바꿀지부터.

### F9. `EquipmentType` 오타가 공개 계약에 전파
- `Header = 1`(Helmet 의 오타로 보임) · `Shoose = 3` → `equipment.proto:26,28` `EQUIPMENT_TYPE_HEADER/_SHOOSE` → 클라 DLL. 고치려면 proto 재생성 + 클라 Generated + 저장된 슬롯 값 확인.

### F13 잔여. `DialogueView` 대사 진행 입력을 직접 폴링
- `GUI/Dialogue/DialogueView.cs:51-62` 가 `Keyboard`/`Mouse` 직접 폴링(HUD 4키는 정식 경로로 이관 완료). 모달 중에만 받는 입력이라 `Advance` 액션 신설 vs UI Submit(EventSystem) 중 **먼저 결정**. 그냥 `InputRouter` 로 밀면 대화창이 닫혀도 키가 살아 있다.

---

## 낮음 — 정리 대상 (위험 아님)

- **B3** `Game.Gameplay.Input.InteractionSystem` 휴면 중복 — 실작동 경로는 `Game.Gameplay.Character`(detector + `IInteractable`). 제거 또는 일원화.
- **F14** Redis 스트림 `maxLength` 없음 — 비테스트 생산 지점 3곳(`ChatBroadcastChannel.cs:18` · `DungeonRoomBroadcastChannel.cs:13` · `RedisMessageQueueBase.PublishAsync`). 마지막 하나만 고쳐도 Consumer Group 큐 전부가 유계. `XADD … MAXLEN ~ N`.
- **F7** asmdef 3개(`Game.Presentation` · `Game.System.InGame` · `Game.Tests.EditMode`)가 존재하지 않는 `Game.System.DungeonLobby` 를 참조. 컴파일엔 무해, 인스펙터 경고.
- **F8** SocketServer `SessionId` 가 앰비언트 로그 컨텍스트가 아니다 — 21개 호출부가 템플릿에 직접 넣어 두 번 찍히고, 나머지는 빈칸. 세션 진입점에서 `LogContext.PushProperty` 한 번.
- **F10** 팝업 호출 4단계 보일러플레이트 8곳 반복(`SetAddressableOwner` 호출부). `ShowAlertAsync(title, msg, glow)` 하나로.
- 하드코딩: gRPC 주소 `Network/Https/GameApiClient.cs:19`(M6 배포 전 필요) · Redis 락 설정 `RedisDistributedLock.cs:10`
- 툴팁 미연동 `GUI/Hud/Sub/BattleEffectSlot.cs:17`
- 상시 `Debug.Log` — `CharacterSpawner`(21) · `LobbyModel`(14) · `GameSessionConnector`(13) 등. 릴리스 빌드 전 조건부 컴파일/로그 레벨.

---

## 미실측 ("문제 없음"이 아니라 "확인 안 함")

- 몬스터→플레이어 지연 실측 ms — 트레이스 기본 Off. `Tools/Combat/Combat Trace` Record 후 던전 플레이 필요.
- leviathan 보스 모델·슬램 모션 실플레이 육안 확인(가드 테스트로 배선만 고정).
- `/healthz` 가 실제 200 을 반환하는지, Docker Compose `healthcheck` 연결 여부.
- 아트 팩 실사용률(3.0GB 중 참조 비율).
- P1·P2 의 "리뷰" 항목 전부 — 재현 테스트를 만들 때 실제 여부가 확정된다.

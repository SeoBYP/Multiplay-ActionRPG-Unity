# 챕터 3 학습 로그 — 실시간 던전 로비 시스템

## 처음 알았던 것 vs 피드백으로 수정된 것

### Polling vs Streaming — "둘 다 똑같은 거 아냐?"

**처음 내가 생각한 것:**
Polling이나 Streaming이나 결국 클라이언트가 서버한테 물어보고 받아오는 거라서 똑같다고 생각했음.

**피드백:**
연결 방식 자체가 다름.

| | Polling | Streaming |
|--|--|--|
| 연결 방식 | 요청할 때마다 연결 맺고 끊음 | 한 번 연결 후 계속 유지 |
| 서버 부하 | 요청마다 세션 생성 비용 발생 | 연결 유지 비용만 발생 |
| 실시간성 | 주기 간격만큼 딜레이 | 이벤트 발생 즉시 전달 |
| 불필요한 요청 | 변화 없어도 매번 요청 | 변화 있을 때만 전송 |

Polling은 "5초마다 물어봐도 될까요?" → 방이 안 바뀌어도 계속 요청.
Streaming은 "방에 변화가 생기면 알려줄게요" → 변화가 있을 때만 서버가 push.

**추가로 배운 것:**
- Polling 개선판이 Long Polling (응답 올 때까지 연결 유지)
- Long Polling보다 효율적인 게 Server-Sent Events (단방향)
- 양방향이 필요하면 WebSocket 또는 gRPC Stream
- 이 프로젝트에서는 WebSocket을 쓰지 않은 이유: gRPC로 채팅/로비를 커버 가능해서 기술 부채 최소화

---

### gRPC Server-Side Streaming 선택 이유

**내가 이해한 것:**
방 정보는 서버 → 클라이언트 방향으로만 보내면 되니까 Server-Side Streaming으로 충분.

**피드백으로 추가된 것:**
맞음. Bi-directional Streaming은 양방향이 필요할 때 사용.
로비에서는 클라이언트가 스트림으로 데이터를 보낼 필요 없음 → Server-Side가 적합.

---

### Redis Pub/Sub + Bounded Channel 구조

**내가 설계한 것:**
Pub/Sub으로 알림 처리해서 같은 방에 있는 유저들끼리만 통신되게 했음.

**피드백 — 왜 Bounded Channel을 쓰는가:**

처음엔 "멀티 스레드로 프레임 분산을 위해서"라고 이해했음.

실제 이유는 두 가지:

**1. 스레드 경계 분리**
```
Redis Subscriber 스레드 (메시지 수신)
       ↓  Channel.Writer.TryWrite()
  Bounded Channel
       ↓  Channel.Reader.ReadAllAsync()
gRPC 스트림 스레드 (클라이언트에 전송)
```
Redis 수신과 gRPC 전송을 직접 연결하면 Redis 스레드가 gRPC 처리를 기다려야 함.
채널로 분리하면 각자 독립적으로 동작 → 병렬 처리 가능.

**2. 백프레셔 (Backpressure)**
채널 크기를 제한해서 처리 못하는 메시지가 무한 쌓이는 것을 방지.
`BoundedChannelFullMode.DropOldest`를 선택한 이유:

| 옵션 | 동작 | 문제 |
|------|------|------|
| Wait | 꽉 차면 발행 대기 | Redis 스레드 블로킹 |
| DropNewest | 새 메시지 버림 | 최신 정보 유실 |
| **DropOldest** | 오래된 메시지 버림 | **로비에서 최신 상태가 중요 → 적합** |

로비 상태는 "현재 방 상태"가 중요하지 "5초 전 방 상태"가 중요하지 않음.
밀려 있는 오래된 업데이트를 버리고 최신 것만 전달하면 됨.

---

### Race Condition — 방 입장 동시성 문제

**내가 이해한 것:**
방에 동시에 여러 명이 입장하면 MaxPlayers를 초과할 수 있다.

**피드백:**
Redis 기반 서버에서 Race Condition 해결책: **Lua Script**.

일반 코드 흐름의 문제:
```
[유저A] 인원 조회 (3/4)
[유저B] 인원 조회 (3/4)  ← 동시에
[유저A] 입장 처리 (4/4)
[유저B] 입장 처리 (5/4)  ← MaxPlayers 초과!
```

Lua Script는 Redis에서 **원자적으로 실행**됨.
조회 → 검증 → 저장이 하나의 트랜잭션으로 처리되어 중간에 다른 명령 끼어들 수 없음.

**구현 결과:**
`JoinRoomLua` 스크립트에서 반환 코드로 결과를 구분:
```
-1: RoomNotFound
-2: InvalidStatus
-3: AlreadyInOtherRoom
-4: AlreadyInThisRoom
-5: RoomFull
 1: Success
```

이를 `JoinRoomAtomicResult` enum으로 표현하여 Application 레이어에서 switch로 처리.

---

### Host Succession (방장 위임) — HashSet → List 변경

**발견된 문제:**
`CurrentPlayers`가 `HashSet<long>`이었을 때 방장이 나가면 다음 방장이 누가 되는지 순서가 보장되지 않았음.

`HashSet`은 삽입 순서를 보장하지 않음 → `FirstOrDefault()`로 다음 방장을 뽑아도 매번 다른 사람이 될 수 있음.

**수정:**
`HashSet<long>` → `List<long>`으로 변경.

`List`는 삽입 순서 보장 → 입장한 순서대로 정렬됨 → 방장 퇴장 시 두 번째로 입장한 사람이 방장이 됨.

**트레이드오프:**
- `HashSet`: 중복 방지 O(1), 순서 없음
- `List`: 순서 보장, 중복 방지는 `Contains()` O(n) 또는 별도 로직 필요

로비 규모(최대 수십 명)에서는 `List`의 O(n) 비용이 문제 없음.

---

### N+1 쿼리 문제 — ToRoomInfo

**발견된 문제:**
`ToRoomInfo`에서 방의 플레이어 정보를 가져올 때:

```csharp
// 수정 전 (N+1)
foreach (var userId in room.CurrentPlayers)
{
    var user = await userRepository.GetByIdAsync(userId);  // N번 호출
    info.CurrentPlayers.Add(user.ToUserInfo());
}
```

방에 4명이 있으면 Redis에 4번 왕복 → 방이 많아질수록 기하급수적으로 증가.

**수정:**
```csharp
// 수정 후 (배치 조회)
var users = await userRepository.GetByIdsAsync(room.CurrentPlayers);
```

`GetByIdsAsync`는 Redis Batch를 사용해 한 번에 조회.

**추가로 배운 것 — `IEnumerable` vs `List` 파라미터 타입:**

`GetByIdsAsync(IEnumerable<long> userIds)`로 만들면 내부에서 `Count()`와 `ElementAt(i)` 사용 시 **O(n²)** 문제 발생.

`IEnumerable`은 매번 처음부터 순회하기 때문.
`List<long>`으로 타입을 고정하면 `Count`는 O(1) 속성 접근, `[i]`는 O(1) 인덱서 접근.

---

## 코드 리뷰에서 발견된 버그 수정 이력

### TryJoinRoomAsync 파라미터 순서 버그

**문제:**
```csharp
// 수정 전 (버그)
var joinResult = await dungeonRoomRepository.TryJoinRoomAsync(roomId, userId, ct);

// 인터페이스 시그니처
Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, ...);
```

호출 순서와 인터페이스 순서가 반대였음 → userId 자리에 roomId가, roomId 자리에 userId가 들어가는 버그.

**수정:**
```csharp
var joinResult = await dungeonRoomRepository.TryJoinRoomAsync(userId, roomId, ct);
```

**배운 것:**
같은 타입(`long`) 파라미터가 여러 개일 때 순서 버그는 컴파일 에러가 나지 않음.
런타임에서만 발견됨 → 인터페이스 설계 시 파라미터 순서 컨벤션을 통일해야 함.

---

### SubscribeRoom — sessionId null 체크 누락

**문제:**
다른 gRPC 메서드들은 sessionId null 체크 후 Unauthorized 반환하는데, `SubscribeRoom`만 null 체크 없이 `SubscribeAsync(null, ...)`을 호출했음.

**수정:**
```csharp
if (sessionId is null)
    throw new RpcException(new Status(StatusCode.Unauthenticated, "..."));
```

스트리밍 메서드는 Response 객체를 반환하는 게 아니라 `throw new RpcException`으로 에러를 전달해야 함.

---

## 현재 코드에서 아직 미완성인 것 (TODO)

| 항목 | 내용 | 우선순위 |
|------|------|----------|
| `Console.WriteLine` → `ILogger` | `DungeonRoomRepository`, `DungeonLobbyGrpcService` 전체 | 중간 |
| Game session endpoint 설정 | `Host/Port` 관리 전략을 `appsettings.json` 또는 별도 parser로 정리 | 높음 |
| 연결 해제 처리 | 스트림 중간에 클라이언트가 끊어졌을 때 정리 로직 완성도 | 높음 |
| 에러 복구 전략 | gRPC 스트림 에러 발생 시 클라이언트 재연결 흐름 | 중간 |
| GameServer ↔ SocketServer 운영 보강 | Redis Stream 기반 메시지 흐름은 연결됨. 재시도/관측성/consumer 운영 전략 보강 필요 | 높음 |

---

## 최근 구조 변경으로 추가로 배운 것

### GameSession 책임 분리

예전에는 `DungeonLobbyService.StartGameAsync()`가 게임 시작 요청부터 세션 생성까지 함께 처리하는 구조였음.

최근 리팩터링으로 아래처럼 경계가 분리됨:

- `DungeonLobbyService`
  - 게임 시작 요청 수락
  - room 상태를 `Starting`으로 전이
- `SocketServer`
  - `GameStartRequestedMessage` 소비
  - 준비 완료 후 `GameSessionReadyMessage` 발행
- `GameSessionReadyConsumer`
  - 준비 완료 메시지 수신
  - `GameSessionService.CreateGameSessionAsync(...)` 호출
  - room 상태를 `Playing`으로 반영
  - 구독 스트림 publish

배운 점:

- 로비 서비스와 게임 세션 생성 책임은 분리하는 편이 맞음
- 요청-응답 안에서 외부 준비 완료까지 기다리면 경계가 섞이고 실패 지점이 늘어남
- 비동기 메시지 흐름으로 바꾸면 확장성과 장애 대응 포인트가 더 명확해짐

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Server-Side Streaming | 서버 → 클라이언트 단방향 지속 스트림 |
| Redis Pub/Sub | 채널에 메시지 발행 → 구독자 전체에게 즉시 전달 |
| Bounded Channel | 크기 제한 있는 비동기 큐, 스레드 경계 분리 |
| DropOldest | 채널이 꽉 찼을 때 오래된 메시지부터 버림 |
| 백프레셔 (Backpressure) | 소비 속도보다 생산이 빠를 때 흐름 제어하는 메커니즘 |
| Lua Script 원자성 | Redis에서 여러 명령을 하나의 트랜잭션으로 실행 |
| Race Condition | 여러 요청이 동시에 같은 자원을 수정할 때 발생하는 데이터 불일치 |
| Host Succession | 방장 퇴장 시 다음 멤버에게 방장 위임, List로 순서 보장 |
| N+1 쿼리 | 목록 조회 후 각 항목마다 추가 쿼리 발생하는 성능 문제 |
| 배치 조회 | 여러 ID를 한 번에 조회해 N+1 해결 |

# 03. 실시간 로비 — 밀어주는 서버, 그리고 같은 자리를 노리는 두 사람

> **한 줄** — 방 목록을 폴링으로 새로 고치는 대신 **gRPC 서버 스트리밍으로 밀어주고**, 스트림이 나르는 것은 방 상태가 아니라 **"다시 읽어라"는 RoomId 하나**로 고정했다. 그 대가로 만난 것이 동시 입장 경쟁이다.
>
> **범위** 스트리밍 선택 · Bounded Channel 백프레셔 · 동시성 · 방장 승계 · N+1
> **검증** `DungeonLobbyE2ETests`(Docker 대상) · `DungeonLobbySubscriptionServiceTests`

---

## 1. 폴링을 버린 기준

"결국 클라가 물어보고 받는 건 똑같지 않나"라고 생각했었다. 다른 건 **비용이 어디서 발생하느냐**다.

| | Polling | Server Streaming |
|---|---|---|
| 연결 | 요청마다 맺고 끊음 | 한 번 맺고 유지 |
| 변화가 없을 때 | **그래도 요청** (전부 낭비) | 아무것도 흐르지 않음 |
| 지연 | 주기만큼 (최대 N초) | 이벤트 발생 즉시 |
| 서버 비용 | 요청 수 × 세션 생성 | 연결 수 × 유지 비용 |

로비는 **대부분의 시간 동안 아무 일도 일어나지 않는다.** 방이 하나도 안 바뀌어도 폴링은 초당 N회 요청을 만든다. 이 지점에서 두 방식의 비용 곡선이 갈린다.

선택지는 스펙트럼이었다 — `Polling → Long Polling → SSE → WebSocket / gRPC Stream`. 로비는 **서버 → 클라 단방향**이면 충분하므로(클라가 스트림으로 올려 보낼 게 없다) 양방향 스트리밍은 과했고, WebSocket은 gRPC로 이미 되는 일을 두 기술로 나눠 갖는 셈이라 기각했다([01](./chapter-01-architecture.md)의 프로토콜 중복 회피와 같은 판단).

## 2. 스트림의 뼈대 — 채널이 나르는 것은 "RoomId 하나"

```
 [방 변화 발생]                                   [gRPC 스트림]
      │                                                ▲
      ▼                                                │
 Redis Stream  ─────▶  구독 스레드  ──TryWrite──▶  Bounded Channel  ──ReadAllAsync──▶ SendLoop
 stream:room:{id}      (수신 전담)      (용량 제한)     Channel<long>              (전송 전담)
                                                          │
                                                    담는 것 = RoomId 뿐
                                                          │
                                                          ▼
                                              SendLoop이 DB에서 최신 방 상태를
                                              **다시 읽어서** 클라에 보낸다
```

### 결정 ① 채널로 스레드 경계를 끊는다

Redis 수신과 gRPC 전송을 직접 연결하면 **수신 스레드가 전송이 끝날 때까지 묶인다**. 클라 하나가 느리면 그 방의 이벤트 수신 전체가 밀린다. 채널을 사이에 두면 양쪽이 각자의 속도로 돈다.

### 결정 ② 꽉 차면 **오래된 것부터 버린다**

```csharp
// UserRoomContext.cs:12
Channel.CreateBounded<long>(new BoundedChannelOptions(capacity) {
    FullMode = BoundedChannelFullMode.DropOldest
})
```

| 옵션 | 동작 | 이 상황에서 |
|---|---|---|
| `Wait` | 꽉 차면 발행 측 대기 | ❌ Redis 수신 스레드가 블로킹된다 |
| `DropNewest` | 새 메시지 버림 | ❌ 최신 상태를 버리는 건 정반대 |
| **`DropOldest`** | 오래된 것부터 버림 | ✅ 로비에 필요한 건 **지금 상태**, 5초 전 상태가 아니다 |

### 결정 ③ 그리고 이게 핵심 — 이벤트는 **트리거일 뿐**이다

채널 타입이 `Channel<long>`이다. 방 상태 스냅샷이 아니라 **RoomId만** 흐른다. 수신 측은 ID를 받고 나서 **진실원(DB)에서 다시 읽는다.**

이유는 두 가지다.
- **채널이 DropOldest로 메시지를 버려도 안전하다** — 버려진 게 상태였다면 그 변경은 영영 유실되지만, 버려진 게 "다시 읽어라" 신호라면 뒤따르는 신호 하나가 최신 상태를 통째로 실어 나른다.
- **모든 구독자가 같은 진실을 본다** — 각자 스냅샷을 들고 있으면 순서가 엇갈리는 순간 화면이 갈라진다.

> 이 원칙("이벤트는 ID + 다시 읽어라, 최신 상태는 항상 DB에서")은 나중에 프로젝트 전역 규칙이 됐다. 실제로 이걸 어겨서 생긴 버그가 [07](./chapter-07-db-cache.md)의 `AsNoTracking` 사건이다 — 다시 읽긴 했는데 EF 추적 캐시가 옛 엔티티를 돌려줘서, SendLoop이 방 상태를 영원히 `Starting`으로 읽었다.

## 3. 동시성 — 같은 문제가 두 번 모습을 바꿨다

### 문제

```
[A] 인원 조회 (3/4)
[B] 인원 조회 (3/4)   ← 동시
[A] 입장 처리 (4/4)
[B] 입장 처리 (5/4)   ← 정원 초과
```

전형적인 **check-then-act** 경쟁이다. 조회와 갱신 사이에 다른 요청이 끼어들 수 있다.

### v1 — Redis Lua 스크립트 (당시 해법)

방 인원이 `DungeonRoom` 엔티티 안의 리스트였을 때는, 조회·검증·저장을 **Lua 스크립트 하나로 묶어** Redis에서 원자 실행했다. 반환 코드로 실패 사유를 구분했다.

```
-1 RoomNotFound  -2 InvalidStatus  -3 AlreadyInOtherRoom
-4 AlreadyInThisRoom  -5 RoomFull   1 Success
```

→ `JoinRoomAtomicResult` enum으로 받아 Application에서 분기. **Redis에서 원자성을 사는 대신, 검증 로직 일부가 Lua 문자열 안으로 들어간다**는 대가가 있었다.

### v2 — 멤버십을 별도 테이블로 (현재)

이후 방 멤버십은 엔티티 내부 리스트에서 **`dungeon_room_players` 연관 테이블**(PK: `RoomId + UserId`, `JoinedAt` 보유)로 분리됐다. 이유는 이 챕터 밖에 있다 — 던전 퇴장·재접속·호스트 이양을 **플레이어 단위 이벤트**로 다루려면 멤버십이 방 엔티티에 묶여 있으면 안 됐다.

얻은 것은 명확하다. 복합 PK가 **같은 방 중복 입장을 DB 제약으로** 막고, `JoinedAt`이 입장 순서를 영속적으로 보존한다.

### ⚠️ 그런데 원자성은 같이 옮겨오지 못했다 (현재 결함)

현재 `DungeonLobbyService.JoinRoomAsync`는 잠금 없는 check-then-act다.

```csharp
// DungeonLobbyService.cs:170-174
var currentPlayers = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);
if (currentPlayers.Count >= room.MaxPlayers)      // ← 조회
    return ...RoomFull;
await dungeonRoomPlayerRepository.CreateAsync(roomId, userSession.UserId, ct);  // ← 갱신 (사이에 방어 없음)
```

- **정원 초과**를 막는 것이 아무것도 없다 — 개수 제약은 DB로 표현할 수 없고, 락도 걸려 있지 않다.
- **동시 다중 방 입장**도 막히지 않는다 — `AlreadyInRoom` 역시 check-then-act이고, `UserId` 인덱스가 **unique가 아니다**(`DungeonRoomPlayerConfiguration.cs:22`).
- v1의 원자 API(`TryJoinRoomAsync`)는 **아직 인터페이스에 남아 있지만 아무도 호출하지 않고**, 구현도 상태 확인만 하는 껍데기로 축소됐다.

고칠 재료는 이미 코드베이스에 있다 — `IUserLock`(Redis `SET NX EX` + 소유자 토큰 검증 Lua 해제)이 존재하고 `ChatService`가 쓰고 있다. 로비 입장에는 걸려 있지 않을 뿐이다.

> **교훈** — 저장 구조를 바꾸면 **그 구조가 떠받치던 불변식도 같이 이사해야 한다.** 기능 테스트는 전부 통과한다. 경쟁 조건은 혼자 테스트할 때 재현되지 않기 때문이다.

## 4. 방장 승계 — 같은 요구, 세 번 진화한 해법

방장이 나가면 **다음으로 오래 있던 사람**이 방장이 돼야 한다. 요구는 처음부터 같았는데 해법이 바뀌었다.

```
v0  HashSet<long>  →  순서 없음. FirstOrDefault()가 매번 다른 사람을 뽑는다 (버그)
v1  List<long>     →  삽입 순서 보장. 대가 = 중복 방지가 O(n) Contains
v2  JoinedAt 컬럼  →  OrderBy(p => p.JoinedAt)로 명시적 정렬 + 복합 PK가 중복 방지
```

v1은 **순서를 자료구조에 암묵적으로 의존**한 것이고, v2는 **순서를 데이터로 명시**한 것이다. 후자는 저장소가 Redis든 Postgres든 정렬 결과가 같다. "순서가 중요하면 순서를 저장하라"가 여기서 얻은 규칙이다.

> 같은 함정이 나중에 다시 나온다 — 무순서 Set 위에서 페이징을 하면 페이지 경계가 매번 달라진다([27](./chapter-27-silent-failure.md)).

## 5. N+1 — 고쳤는데 다른 층에서 다시 나타났다

**1차(플레이어 층)**: `ToRoomInfo`가 방 인원마다 유저를 한 명씩 조회했다. 4명이면 4왕복. → `GetByIdsAsync`로 배치 조회.

**2차(방 목록 층)**: 멤버십이 별도 테이블로 나가자, 방 목록 조회에서 **방마다 2왕복**(플레이어 조회 + 유저 조회)이 됐다. 방이 20개면 40왕복. → 전체 방의 플레이어를 한 번에(`GetPlayersByRoomIdsAsync`), 거기서 나온 유저를 또 한 번에 조회한 뒤 메모리에서 조립.

```
[N+1]   방 20개  →  20 × (players + users) = 40 쿼리
[배치]  방 20개  →  rooms 1 + players 1 + users 1 = 3 쿼리 (방 수와 무관)
```

**타입 하나가 성능을 바꾼 사례도 있었다** — `GetByIdsAsync(IEnumerable<long>)`로 두면 내부의 `Count()`·`ElementAt(i)`가 매번 처음부터 순회해 **O(n²)** 이 된다. `List<long>`으로 고정하면 `Count`는 속성 접근, `[i]`는 인덱서로 각각 O(1)이다. 시그니처는 지금도 `List<long>`이다.

## 6. 컴파일러가 잡아주지 않는 버그 둘

**같은 타입 파라미터의 순서** — `TryJoinRoomAsync(long userId, long roomId)`를 `(roomId, userId)`로 호출하고 있었다. 둘 다 `long`이라 **컴파일도 되고 테스트도 통과하고 런타임에만 틀린다.** 이후 ID 파라미터는 순서 컨벤션을 통일했다.

**스트리밍 메서드의 인증 누락** — 다른 RPC는 전부 `sessionId` null 검사 후 `Unauthorized`를 반환하는데 `SubscribeRoom`만 빠져 있었다. 스트리밍은 응답 객체를 반환하는 형태가 아니라서(반환형이 `Task`) **응답에 에러를 담는 습관이 통하지 않고**, `throw new RpcException(...)`으로 전달해야 한다. 형태가 다르면 규칙이 새어 나간다.

## 7. 책임 경계 — StartGame은 어디까지 하는가

초기엔 `StartGameAsync` 하나가 시작 요청부터 게임 세션 생성까지 다 했다. 지금은 잘려 있다.

```
DungeonLobbyService   방 상태를 Starting으로 전이 + 이벤트 기록      ← 여기까지만
        │  (Redis Stream)
SocketServer          방 준비 → GameSessionReadyMessage 발행
        │  (Redis Stream)
GameSessionReadyConsumer  세션 생성 → 방 Playing 반영 → 구독자에게 publish
```

요청-응답 안에서 **다른 서버의 준비 완료를 기다리면** 그 RPC의 응답 시간이 상대 서버의 상태에 묶이고, 실패 지점이 하나 늘어난다. 비동기 메시지로 끊으면 각 단계가 독립적으로 재시도·관측 가능해진다. (전체 흐름 = [05](./chapter-05-game-start-e2e.md))

이 분리 덕에 나중에 **호스트 재접속** 같은 것도 스트림 진입 지점에서 처리할 수 있게 됐다 — `SubscribeRoom`은 호스트가 `Starting` 상태 방에 다시 붙으면 시작을 자동 재트리거한다(`DungeonLobbyGrpcService.cs:278`).

## 8. 남은 것

- **입장 원자성 미복구** (3절) — 현재 알려진 가장 실질적인 결함.
- 스트림 중간 연결 해제 정리와 클라 재연결 전략은 이후 챕터에서 다뤘다([11](./chapter-11-socket-session-entry.md)·[21](./chapter-21-connection-liveness-hp-authority.md)).

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 이벤트 = ID + "다시 읽어라" | 전 도메인 공통 규칙. 위반 사례가 `AsNoTracking` 버그([07](./chapter-07-db-cache.md)) |
| DropOldest 백프레셔 | 채팅 스트림도 동일 정책(`UserChatContext`) |
| 순서가 중요하면 순서를 저장한다 | `JoinedAt` 기반 호스트 이양 · 페이징 정렬([27](./chapter-27-silent-failure.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-03-dungeon-lobby.md](../learning-log/chapter-03-dungeon-lobby.md)

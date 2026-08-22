# Chapter 05 — 게임 시작 E2E 흐름 (GameServer → SocketServer → Client)

## 설계 배경 (Why)

던전 게임 시작은 두 서버가 협력해야 하는 복잡한 흐름이다.

- **GameServer**: 방 상태 관리, 클라이언트 인증, gRPC 스트림 브로드캐스트
- **SocketServer**: 실시간 인게임 TCP 통신

두 서버는 **직접 호출하지 않는다.** 대신 Redis Streams 메시지 큐를 통해 비동기로 협력한다.

### 왜 직접 RPC 호출 안 하나?

```
GameServer → [HTTP/gRPC] → SocketServer  ← 이렇게 하면 안 되나?
```

하면 된다. 하지만 문제가 생긴다:
- SocketServer가 죽어있으면 GameServer도 실패 → 장애 전파
- SocketServer 다중 인스턴스 시 어디로 보낼지 라우팅 필요
- 두 서버 간 강결합 → 배포/스케일링 복잡

Redis Streams를 쓰면:
- GameServer는 "게임 시작 요청을 발행"하고 끝
- SocketServer는 자기 페이스대로 소비
- 서로 모르는 상태에서 통신 가능 (느슨한 결합)

---

## 전체 E2E 흐름

```
[클라이언트]                [GameServer]              [SocketServer]
    |                           |                           |
    |── SubscribeRoom ─────────>|                           |
    |   (gRPC 스트림 연결)       |                           |
    |                           |                           |
    |── StartRoom ─────────────>|                           |
    |                           |── XADD stream:game:start >|
    |                           |   {roomId, playerIds}     |
    |                           |                           |── CreateRoom()
    |                           |   <── SET socket:room     |
    |                           |       :{id}:ready ────────|
    |                           |   (127.0.0.1:7777)        |
    |                           |                           |
    |                           |── room.SetSocketInfo()    |
    |                           |── UpdateAsync()           |
    |                           |── PublishAsync(roomId)    |
    |                           |                           |
    |<── GameStartedEvent ──────|                           |
    |   {ip, port, roomInfo}    |                           |
    |                           |                           |
    |── [TCP Connect 7777] ─────────────────────────────── >|
```

---

## 핵심 구현 포인트

### 1. DungeonRoom 엔티티에 SocketInfo 포함

```csharp
public string SocketIp { get; private set; } = string.Empty;
public int SocketPort { get; private set; } = 0;

public void SetSocketInfo(string socketIp, int socketPort)
{
    SocketIp = socketIp;
    SocketPort = socketPort;
}
```

**왜 Room 엔티티에?**
Room 상태가 PLAYING이 될 때 어느 SocketServer에 접속해야 하는지가 Room의 속성이다.
구독 중인 모든 클라이언트가 `GetDungeonRoomAsync`를 호출할 때 ip:port를 한 번에 받을 수 있다.

### 2. StartGameAsync 순서

```csharp
// 1. MQ 발행 (SocketServer에 게임 시작 알림)
await gameStartPublisher.PublishAsync(new GameStartMessage { RoomId = roomId, PlayerIds = ... }, ct);

// 2. SocketServer가 준비될 때까지 폴링 (최대 10초)
var socketInfo = await socketReadyChecker.WaitAsync(roomId, ct);
if (socketInfo is null)
    return Result.Failure("SocketServer 응답 없음");

// 3. Room 상태 갱신 (ip:port 포함)
room.StartGame(userId);
room.SetSocketInfo(parts[0], int.Parse(parts[1]));
await dungeonRoomRepository.UpdateAsync(room, ct);

// 4. 구독자들에게 브로드캐스트
await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
```

순서가 중요하다. MQ 발행 → SocketServer 준비 대기 → Room 갱신 → 구독자 알림.
SocketServer가 준비되기 전에 브로드캐스트하면 클라이언트가 ip:port 없는 GameStartedEvent를 받는다.

### 3. Redis Streams Consumer Group

SocketServer는 Consumer Group 방식으로 메시지를 소비한다:

```csharp
await Database.StreamCreateConsumerGroupAsync(
    QueueKey, GroupName,
    StreamPosition.Beginning,  // "0"
    createStream: true);
```

**`StreamPosition.Beginning` vs `StreamPosition.NewMessages`**

| 옵션 | 의미 | 주의 |
|------|------|------|
| `Beginning` ("0") | 그룹 생성 이전 메시지도 읽음 | 재시작 시 중복 처리 가능 |
| `NewMessages` ("$") | 그룹 생성 이후 새 메시지만 읽음 | 이미 발행된 메시지 놓칠 수 있음 |

SocketServer 재시작 시 처리 못한 메시지를 다시 읽으려면 `Beginning`이 맞다.
단, ACK된 메시지는 제외되므로 중복 처리 걱정은 없다.

### 4. NOGROUP 에러 복구

```csharp
catch (RedisException ex) when (ex.Message.Contains("NOGROUP"))
{
    Console.WriteLine("RedisException: {0}", ex.Message);
    await EnsureConsumerGroupAsync();  // 스트림/그룹 재생성
    continue;
}
```

**발생 시나리오:**
- 개발 중 `DEL stream:game:start`로 스트림 정리
- SocketServer 재시작 없이 재테스트
- GameServer가 새 스트림 생성 → Consumer Group 없음 → NOGROUP

**이 에러를 방치하면:**
`_ = Task.Run(...)` 패턴에서 예외가 조용히 삼켜진다.
콘솔에 아무것도 안 나오고, `[GameStart]` 로그도 없고, SocketServer 프로세스는 살아있다.
이런 Silent Failure가 가장 디버깅하기 어렵다.

### 5. StartRoomResponse vs SubscribeRoom

```
StartRoomResponse: Result + RoomInfo (ip/port 불필요)
GameStartedEvent:  RoomInfo + ip + port  ← 여기서 받는다
```

`StartRoom`을 호출한 방장도 `SubscribeRoom`으로 구독 중이므로
`StartRoomResponse`에서 ip:port를 추가로 받을 필요가 없다.
클라이언트는 구독 스트림 한 곳에서 처리하면 된다.

---

## 발생한 버그들

### Bug 1: `await null` NullReferenceException

```csharp
// 버그
RoomInfo = await result.Value?.ToRoomInfo(userRepository),

// result.Value가 null이면 → null Task → await null → NullReferenceException
```

```csharp
// 수정
RoomInfo = result.Value is null ? null : await result.Value.ToRoomInfo(userRepository),
```

`?.`는 메서드를 호출하지 않아 `Task<T>` 대신 `null`을 반환한다.
`await null`은 NullReferenceException이다.

### Bug 2: Clone()에서 SocketInfo 유실

```csharp
// 버그: SocketIp, SocketPort 미전달
public DungeonRoom Clone() => FromRedis(RoomId, RoomName, ...);

// 수정
public DungeonRoom Clone() => FromRedis(RoomId, RoomName, ..., SocketIp, SocketPort);
```

새 필드를 추가할 때 `Clone()`, `FromRedis()`, `ParseFromRedis()`, `ToHashEntry()` 4곳을 함께 수정해야 한다.

### Bug 3: Repository에서 SocketInfo 미저장

Redis Hash에 새 필드를 추가할 때 `UpdateAsync`와 `ParseDungeonRoomFromRedis` 양쪽에 추가해야 한다.
한쪽만 하면 저장은 안 되고 읽기만 되거나, 반대가 된다.

---

## 시니어 리뷰

### 현재 구현의 문제점

**폴링 방식의 한계:**
```csharp
while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
{
    var val = await db.StringGetAsync($"socket:room:{roomId}:ready");
    if (val.HasValue) return val.ToString();
    await Task.Delay(100, ct);  // 100ms 폴링
}
```
10초 동안 최대 100번 Redis를 찌른다. SocketServer가 느리면 타임아웃.
더 나은 방법: Redis Pub/Sub 또는 Redis Streams를 양방향으로 사용.

**Silent Failure:**
`_ = Task.Run(...)` 패턴은 예외를 삼킨다.
운영환경에서는 `ILogger`로 에러를 기록하고, 재시도 또는 알림 처리가 필요하다.

**하드코딩된 SocketServer 주소:**
```csharp
"127.0.0.1:7777"  // Program.cs에 하드코딩
```
appsettings.json에서 읽어야 한다. 다중 인스턴스 시 어떤 주소를 반환할지도 고민 필요.

**Pending Message 처리:**
Consumer Group에서 ACK 전에 SocketServer가 죽으면 메시지가 PEL(Pending Entry List)에 남는다.
`XAUTOCLAIM`으로 일정 시간 후 미처리 메시지를 재처리하는 로직이 필요하다.

---

## 다음 단계

- [ ] E2E 테스트 자동화 (PostMan으로는 Subscribe + RPC 병행 테스트 불가)
  - gRPC 클라이언트 테스트 코드 작성 (xUnit + Grpc.Net.Client)
  - 또는 Unity 클라이언트로 실제 플로우 검증
- [ ] SocketServer appsettings.json으로 IP:Port 분리
- [ ] Console.WriteLine → ILogger 교체
- [ ] TCP Ping/Pong 구현 (연결 상태 확인)

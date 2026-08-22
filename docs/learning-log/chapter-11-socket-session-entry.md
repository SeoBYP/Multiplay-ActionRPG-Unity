# 챕터 11 — 소켓 세션 진입 흐름 (C_Auth 제거 · Redis 기반 검증 · 버그 수정)

## 이번 챕터에서 한 일

챕터 9~10에서 Unity 클라이언트 구조와 소켓 이동 동기화까지 완성했다.  
이번 챕터에서는 실제로 플레이어가 던전에 진입하는 전체 흐름을 가동 상태로 만들었다.

구체적으로:

1. SocketServer TCP 인증 방식 재설계 — `C_Auth`/`S_Auth` 제거, Redis 기반 검증으로 전환
2. `GameSessionReadyConsumer`에서 Redis player key 선 기입 구조 추가
3. 클라이언트 상태 머신 버그 수정 (`S_PlayerJoined` 실패 시 `Failed` 전환)
4. LobbyViewController Addressable 경쟁 조건 수정
5. `SubscribeRoom` Playing 방 재접속 시 Redis key 복구 로직 추가
6. `GameSessionConnector` 중복 이벤트 가드 추가

---

## 문제 1 — 왜 C_Auth가 없어도 되는가

### 처음 설계

SocketServer에 TCP로 붙으면 클라이언트가 먼저 `C_Auth { UserId }` 패킷을 보냈다.  
SocketServer는 이것을 받아서 `_userRoomIndex`라는 인메모리 딕셔너리로 `UserId → RoomId`를 확인했다.  
이 딕셔너리는 `GameStartRequestedMessage`를 받아 Room을 생성할 때 채워졌다.

```
GameStartRequestedMessage 수신
    → Room 생성
    → _userRoomIndex[userId] = roomId  ← 메모리에 저장
    → GameSessionReadyEvent 발행

C_Auth { UserId } 수신
    → _userRoomIndex[userId] 조회  ← 메모리에서 검증
    → S_Auth { Success = true }
```

### 문제가 드러난 시점

Docker를 재시작했더니 SocketServer가 다시 뜨면서 `_userRoomIndex`가 비었다.  
이 상태에서 클라이언트가 방에 입장하려 하면:

- `C_Auth` 패킷 → SocketServer 인메모리 조회 → miss → `S_Auth { Success = false }`
- 클라이언트는 연결을 끊고 재시도 → 같은 결과 → 무한 반복

헬스체크(HeartBeat)는 30초마다 동작해서, 클라이언트가 붙었다가 끊기를 반복하는 게 서버 로그에 남았다.

```
# 서버 로그에 반복적으로 찍히던 내용
ConnectionReset (10054) from 127.0.0.1
ConnectionReset (10054) from 127.0.0.1
ConnectionReset (10054) from 127.0.0.1
```

### 왜 인메모리가 문제인가

인메모리 딕셔너리는 프로세스 재시작에 취약하다.  
SocketServer는 Stateless를 목표로 설계했는데, 정작 인증 검증이 Stateful했다.

또 다른 관점: **GameServer가 이미 인증을 다 하고 있다.**

클라이언트가 GameServer gRPC API를 호출할 때마다 JWT를 검사한다.  
방을 만들고, 입장하고, 시작하는 모든 흐름이 GameServer에서 이미 검증된 상태다.  
그 결과로 `GameSessionReadyEvent(ip, port)`를 받아야만 SocketServer 주소를 알 수 있다.  
즉 SocketServer 주소를 아는 클라이언트는 이미 GameServer에서 검증된 클라이언트다.

그렇다면 SocketServer에서 다시 `C_Auth`로 같은 인증을 반복할 이유가 없다.

---

## 설계 전환 — Redis 기반 입장 검증

### B안 채택: Redis player key 선 기입

인증 제거가 불안하면 **Redis를 공유 상태 저장소로** 쓰는 방식이 있다.

`GameServer`가 게임 세션을 만드는 시점에 각 플레이어 정보를 Redis에 미리 써둔다.  
SocketServer는 TCP 연결 후 `C_PlayerJoin` 패킷을 받으면 Redis를 조회해 검증한다.

```
GameSessionReadyConsumer (GameServer)
    → GameSession 생성
    → Redis HSET gamesession:player:{userId} roomId / gameSessionId / nickname / spawnIndex
    → PublishAsync  ← 이 시점에 Redis에 데이터가 이미 있음

C_PlayerJoin { RoomId, UserId } 수신 (SocketServer)
    → Redis HGETALL gamesession:player:{userId}
    → roomId 일치 확인
    → 통과 시 session.UserId / session.Nickname 세팅
    → room.JoinRoom
    → S_PlayerJoined { Success = true, ... }
```

이 방식의 장점은:

| 항목 | 인메모리 | Redis |
|------|----------|-------|
| 서버 재시작 | 데이터 소실 | TTL 2시간 유지 |
| 수평 확장 | SocketServer 인스턴스 공유 불가 | 모든 인스턴스에서 조회 가능 |
| 인증 책임 분리 | SocketServer가 독립 인증 보유 | GameServer 검증 결과를 그대로 활용 |

### C_Auth 제거 내역

`C_Auth`/`S_Auth` 패킷을 완전히 제거했다.

```csharp
// 제거됨
[MemoryPackUnion(1300, typeof(C_Auth))]
[MemoryPackUnion(1301, typeof(S_Auth))]

// C_PlayerJoin에 UserId 추가
[MemoryPackable]
public partial class C_PlayerJoin : Packet
{
    public long RoomId { get; set; }
    public long UserId { get; set; }  // ← 추가
}
```

Union ID 체계는 클라이언트/서버가 동일해야 하므로, Shared.Packet 프로젝트와 Unity 클라이언트 양쪽을 동시에 수정했다.

```
수정 파일:
  ServerAll/Shared/Shared.Packet/Packets/Packet.cs             ← Union 등록 제거
  ServerAll/Shared/Shared.Packet/Packets/Domains/AuthPackets.cs ← 파일 제거
  ServerAll/Shared/Shared.Packet/Packets/Domains/RoomPackets.cs ← C_PlayerJoin에 UserId 추가
  ServerAll/SocketServer/PacketHandler/Handler/AuthHandler.cs   ← 파일 제거
  ServerAll/SocketServer/PacketHandler/Handler/RoomJoinLeaveHandler.cs ← 전면 재작성

  Client/.../Socket/Packets/Packet.cs                          ← Union 등록 제거
  Client/.../Socket/Packets/AuthPackets.cs                     ← 내용 제거
  Client/.../Socket/Packets/RoomPackets.cs                     ← C_PlayerJoin에 UserId 추가
  Client/.../Socket/Handler/Contents/AuthPacketHandler.cs      ← 내용 제거
  Client/.../Socket/SocketApiClient.cs                         ← AuthPacketHandler 등록 제거
  Client/.../Socket/Session/SocketSession.cs                   ← AuthenticateAsync 제거
  Client/.../Socket/Session/SocketSessionState.cs              ← Authenticating/Authenticated 제거
  Client/.../System/InGame/GameSessionConnector.cs             ← AuthenticateAsync 호출 제거
```

클라이언트 소켓 상태 머신도 단순해졌다.

```
이전: Idle → Connecting → Connected → Authenticating → Authenticated → Joining → Joined
이후: Idle → Connecting → Connected → Joining → Joined
```

---

## 문제 2 — S_PlayerJoined 실패 시 상태 머신이 멈추는 버그

### 증상

`C_PlayerJoin`을 보냈는데 서버가 `S_PlayerJoined { Success = false }`를 돌려보내면,  
클라이언트는 `WaitUntil(Joined || Failed)` 루프에서 **영원히 빠져나오지 못했다.**

```csharp
// GameSessionConnector
await session.JoinRoomAsync(ct);
await UniTask.WaitUntil(
    () => session.State == SocketSessionState.Joined
       || session.State == SocketSessionState.Failed);  // ← 이게 트리거되지 않음
```

### 원인

`SocketSession.UpdateStateFromPacket`에서 실패 시 상태를 `Failed`가 아니라 `Connected`로 설정했다.

```csharp
// 버그 코드
private void UpdateStateFromPacket(Packet packet)
{
    if (packet is S_PlayerJoined joined)
    {
        if (joined.Success)
            State = SocketSessionState.Joined;
        else
            State = SocketSessionState.Connected;  // WaitUntil 조건을 절대 충족 못함
    }
}
```

`Connected` 상태는 WaitUntil 조건에 없었다.  
그래서 루프는 계속 돌았고, 30초 후 HeartBeat가 연결을 강제로 끊는 흐름이 반복됐다.

### 수정

```csharp
// 수정 후
private void UpdateStateFromPacket(Packet packet)
{
    if (packet is S_PlayerJoined joined)
    {
        State = joined.Success
            ? SocketSessionState.Joined
            : SocketSessionState.Failed;  // WaitUntil 조건 충족
    }
}
```

추가로 `ConnectAsync`의 사전 조건도 수정했다.

```csharp
// 수정 전: Idle 또는 Disconnected에서만 재연결 허용
if (State != SocketSessionState.Idle &&
    State != SocketSessionState.Disconnected)
    throw ...;

// 수정 후: Failed 상태에서도 재시도 허용
if (State != SocketSessionState.Idle &&
    State != SocketSessionState.Disconnected &&
    State != SocketSessionState.Failed)
    throw ...;
```

첫 번째 시도가 실패한 뒤 두 번째 `GameSessionReady` 이벤트가 왔을 때 재연결이 가능하다.

---

## 문제 3 — Addressable 핸들 해제 경쟁 조건

### 증상

로비에서 방 대기실(RoomDetail)을 열고 있는 도중에 `GameSessionReady` 이벤트가 오면:

```
[LobbyViewController] RoomDetail 로드 실패: Attempting to use an invalid operation handle
```

에러가 발생했다.

### 원인

`LobbyViewController`에는 두 개의 이벤트 구독이 있었다.

```csharp
// 방 입장 성공 → RoomDetail 열기 (비동기)
_model.NavigateToRoom.Subscribe(roomId =>
{
    CloseLobby();
    OpenRoomDetailAsync().Forget();  // ← 비동기, 아직 로딩 중
});

// 게임 세션 준비 완료 → RoomDetail 닫기
_model.NavigateToGame.Subscribe(args =>
{
    CloseRoomDetail();  // ← 로딩 중인 핸들을 해제해버림
    ...
});
```

`OpenRoomDetailAsync()`가 Addressable을 로딩하는 도중에  
`NavigateToGame`이 발생해 `CloseRoomDetail()`이 핸들을 해제하면,  
아직 `await _detailHandle.Task.AsUniTask()`를 기다리는 `OpenRoomDetailAsync()`가 invalid handle 예외를 던졌다.

### 원인이 있었던 이유

방 생성 → 게스트 입장 → 방 시작이 빠르게 진행되는 경우, Addressable 로딩 완료 전에 `GameSessionReady`가 올 수 있다.

특히 서버가 로컬 Docker에 떠있는 경우 gRPC 응답이 빠르기 때문에 이 타이밍이 쉽게 재현됐다.

### 수정

`NavigateToGame` 핸들러에서 `CloseRoomDetail()` 호출을 제거했다.

```csharp
// 수정 후
_model.NavigateToGame.Subscribe(args =>
{
    // CloseRoomDetail() 제거 — 씬 전환이 자연스럽게 정리한다.
    // 로딩 중에 핸들을 해제하면 invalid operation handle 예외 발생.
    Debug.Log($"[LobbyViewController] 게임 세션 준비 완료 — {args.Ip}:{args.Port}");
});
```

던전 씬이 로드되면 이전 씬이 언로드되면서 VContainer scope가 dispose되고,  
`LobbyViewController.Dispose()`가 `CloseRoomDetail()`을 호출하므로 별도 처리가 불필요하다.

**결과**: DungeonDetail UI가 던전 씬 로드 완료 후 씬 전환으로 닫히게 됨.  
사용자 관점에서는 "대기실이 보이다가 던전 씬이 열리는" 자연스러운 흐름이 된다.

---

## 문제 4 — Docker 재시작 후 Playing 방에 재접속 불가

### 증상

Docker를 재시작하고 기존 Playing 상태의 방에 클라이언트가 재접속하면 연결이 실패했다.  
`redis-cli KEYS "gamesession:player:*"`를 확인하면 아무 키도 없었다.

### 원인 체인

```
Docker 재시작
    → Redis 메모리 초기화 (TTL이 남았더라도 휘발)
    → gamesession:player:{userId} 키 소실
    
클라이언트 재접속
    → DungeonLobbyService.RestoreRoomAsync → StartSubscription
    → SubscribeRoom (서버) → Playing 방 → 즉시 GameSessionReadyEvent 전송
    → 클라이언트: ConnectAndLoadDungeonAsync → C_PlayerJoin 전송
    → SocketServer: Redis HGETALL gamesession:player:{userId} → miss
    → S_PlayerJoined { Success = false }
    → (수정 전) 상태 = Connected → WaitUntil 무한 루프
    → (수정 후) 상태 = Failed → GameSessionConnector 조기 반환
```

문제의 본질은 **`GameSessionReady` 이벤트를 받는 시점에 Redis key가 이미 없다**는 것이었다.

### 두 단계 수정

**단계 1**: `SubscribeRoom` auto-retrigger를 Playing 방에도 확장

호스트가 Playing 방에 재구독하면 `StartGameAsync()`를 다시 호출한다.  
`StartGameAsync()`는 Playing 방에 대해 idempotent하게 처리된다 — 같은 `GameStartRequestedMessage`를 Outbox에 추가하고 상태를 바꾸지 않는다.

```csharp
// 서버: DungeonLobbyService.StartGameAsync (기존 구현)
if (room.Status == RoomStatus.Playing)
{
    // 재시작 상황: Redis key 복구가 필요한 경우를 위해 메시지를 다시 흘린다
    var retryOutbox = OutboxMessage.Create(...);
    await outboxRepository.AddWithRoomUpdateAsync(room, retryOutbox, ct);
    await dungeonLobbySubscriptionService.PublishAsync(roomId, ct);
    return Result<DungeonRoom>.Success(room);
}
```

```csharp
// 서버: SubscribeRoom — Starting → Playing으로 조건 확장
if ((currentRoom.Value?.Status == RoomStatus.Starting ||
     currentRoom.Value?.Status == RoomStatus.Playing) &&
    currentRoom.Value.HostUserId == validation.Value)
{
    await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId, ...);
}
```

**단계 2**: `SendLoopAsync`에서 Playing 방 처리 시 Redis key 직접 복구

Playing 방에 대한 `GameSessionReadyEvent`를 전송하기 **직전에** key가 없으면 DB에서 조회해 Redis에 재기입한다.  
이 방식은 호스트뿐 아니라 게스트에게도 적용된다.

```csharp
case RoomStatus.Playing:
    var gameSession = await gameSessionRepository.GetByRoomIdAsync(room.Value.RoomId, ct);
    
    // 이벤트 전송 전 Redis key 보장 — 서버 재시작 후 Playing 방 재접속 시 key가 소실되어 있을 수 있음
    await EnsurePlayerDataInRedisAsync(room.Value.RoomId, gameSession.GameSessionId, ct);
    
    serverMsg.GameSessionEvent = new GameSessionReadyEvent
    {
        Ip = gameSession.SocketIp,
        Port = gameSession.SocketPort
    };
    break;
```

```csharp
private async Task EnsurePlayerDataInRedisAsync(long roomId, long gameSessionId, CancellationToken ct)
{
    var redis = connectionMultiplexer.GetDatabase();
    var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(roomId, ct);

    for (var i = 0; i < players.Count; i++)
    {
        var player = players[i];
        var key = $"gamesession:player:{player.UserId}";

        if (await redis.KeyExistsAsync(key)) continue;  // 이미 있으면 건너뜀

        var profile = await userProfileRepository.GetByIdAsync(player.UserId, ct);
        var nickname = profile?.NickName ?? $"Player_{player.UserId}";

        await redis.HashSetAsync(key, new HashEntry[]
        {
            new("roomId", roomId),
            new("gameSessionId", gameSessionId),
            new("nickname", nickname),
            new("spawnIndex", i)
        });
        await redis.KeyExpireAsync(key, TimeSpan.FromHours(2));
    }
}
```

**핵심**: `GameSessionReadyEvent`를 클라이언트에 보내는 시점에는 Redis key가 이미 있다는 것이 보장된다.  
타이밍 경쟁 조건이 없다.

---

## 문제 5 — GameSessionReady 이벤트 중복 수신

### 증상

서버 로그에 `GameSessionReady 수신`이 두 번 찍혔다.

```
[GameSessionConnector] GameSessionReady 수신 — ip=127.0.0.1 port=7777 roomId=30
[GameSessionConnector] GameSessionReady 수신 — ip=127.0.0.1 port=7777 roomId=30  ← 중복
[GameSessionConnector] TCP 연결 시도 — ...
[GameSessionConnector] TCP 연결 시도 — ...  ← 중복 연결 시도
```

### 원인

`SubscribeRoom` auto-retrigger가 `PublishAsync`를 호출하면, 이미 구독 중인 모든 클라이언트에 이벤트를 전달한다.  
초기 kick(즉시 전송)과 `StartGameAsync` → `PublishAsync` 경로에서 두 번 올 수 있었다.

또한 이전 `GameSessionConnector` 구현은 이미 `Joining` 상태인데도 새 이벤트가 오면 `ConnectAsync`를 다시 호출했다.

### 수정

```csharp
private void HandleGameSessionReady(string ip, int port, long roomId)
{
    Debug.Log($"[GameSessionConnector] GameSessionReady 수신 — ip={ip} port={port} roomId={roomId}");

    var state = _socketSession.State;
    if (state != SocketSessionState.Idle &&
        state != SocketSessionState.Disconnected &&
        state != SocketSessionState.Failed)
    {
        Debug.Log($"[GameSessionConnector] 이미 연결 중 (state={state}) — 중복 이벤트 무시");
        return;
    }

    ConnectAndLoadDungeonAsync(ip, port, roomId).Forget();
}
```

`Idle`, `Disconnected`, `Failed` 상태에서만 실제 연결을 시작한다.  
이미 `Connecting`, `Joining`, `Joined` 상태이면 이벤트를 무시한다.  
`Failed` 상태는 허용한다 — 첫 번째 시도가 실패한 뒤 두 번째 이벤트로 재시도하는 경로를 막으면 안 된다.

---

## 전체 흐름 다이어그램 (수정 후)

```
[GameServer gRPC]
  StartRoom
    → Outbox: GameStartRequestedMessage
    → OutboxPublisher → stream:game:start (Redis)

[GameServer BackgroundService]
  GameSessionReadyConsumer
    → GameSession 생성 (DB)
    → Redis HSET gamesession:player:{userId} ← 선 기입
    → PublishAsync → SubscribeRoom 구독자들에게 이벤트 전송
    → S_GameStatus 이벤트 → 클라이언트 GameSessionReadyEvent 수신

[Unity Client]
  DungeonLobbyService
    → OnGameSessionReady(ip, port, roomId)
  
  LobbyViewController
    → NavigateToGame → RoomDetail 유지 (씬 전환 후 정리)
  
  GameSessionConnector
    → 중복 가드 통과 확인
    → SocketSession.ConnectAsync
    → SocketSession.JoinRoomAsync
      → C_PlayerJoin { RoomId, UserId }

[SocketServer TCP]
  RoomJoinLeaveHandler
    → Redis HGETALL gamesession:player:{userId}
    → roomId 일치 확인
    → session.UserId / session.Nickname 세팅
    → room.JoinRoom
    → S_PlayerJoined { Success = true, PosX, PosY, PosZ, RotY, ... }

[Unity Client]
  SocketSession
    → S_PlayerJoined { Success = true } → State = Joined
    → WaitUntil(Joined) 완료
  
  GameSessionConnector
    → SceneManager.LoadSceneAsync("Dungeon")
```

---

## 배운 점

### 1. 인메모리 상태는 무조건 재시작에 취약하다

단일 서버라면 재시작 시 클라이언트에게 재연결하라고 알릴 수 있다.  
멀티 인스턴스 환경에서는 A 인스턴스에 저장된 메모리를 B 인스턴스가 모른다.

SocketServer가 "인게임 상태"만 인메모리로 유지하도록 설계 목표를 잡고,  
**검증에 필요한 입장 데이터는 공유 저장소(Redis)에 두는 것이 올바른 방향**이었다.

### 2. 두 서버의 경계에서는 "누가 먼저 쓰느냐"가 중요하다

`GameSessionReadyConsumer`가 Redis에 쓴 뒤 `PublishAsync`로 이벤트를 내보낸다.  
이 순서가 반대였다면 클라이언트가 이벤트를 받고 TCP 연결을 시도하는 시점에 Redis key가 없다.

분산 시스템에서는 이벤트 발행 전에 의존 데이터를 먼저 기입하는 순서가 매우 중요하다.

### 3. 상태 머신의 모든 경로를 명시적으로 다뤄야 한다

`S_PlayerJoined { Success = false }` 케이스를 `Connected`로 처리한 것은 단순히 "틀린 상태값"이 아니었다.  
`WaitUntil`이 영원히 대기하다가 HeartBeat에 의해 끊기고, 다시 연결하고, 다시 끊기는 사이클로 이어졌다.

상태 머신을 설계할 때는 "성공 경로"뿐 아니라 **모든 실패 경로에서 상태가 명확히 전이**되어야 한다.

### 4. Addressable 핸들의 수명주기는 비동기 문맥에서 조심해야 한다

Unity Addressables는 핸들 기반이다.  
`Addressables.LoadAssetAsync()`를 호출한 뒤 `await`가 끝나기 전에 `Addressables.Release(handle)`을 호출하면 예외가 난다.  
이런 경쟁 조건은 직접 만들지 않더라도, **다른 이벤트 구독자가 핸들을 해제할 수 있다**.

리소스 해제는 로딩을 시작한 컨텍스트 안에서 해결하거나,  
씬 전환처럼 자연스러운 정리 타이밍에 맡기는 것이 안전하다.

### 5. "이벤트가 여러 번 올 수 있다"고 가정하고 핸들러를 짜야 한다

`GameSessionReady`처럼 외부에서 발행되는 이벤트는 재시도, 재구독, 네트워크 재연결 등으로 중복 수신될 수 있다.  
핸들러에 멱등성 가드가 없으면 같은 연결을 두 번 시도해서 에러가 난다.

이벤트 핸들러는 항상 "이미 처리 중인가"를 확인하고, 처리 중이면 무시하는 패턴을 기본으로 가져가야 한다.

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Redis 선 기입 | 이벤트 발행 전 의존 데이터를 공유 저장소에 먼저 저장하는 패턴 |
| EnsurePlayerDataInRedisAsync | Playing 방에 재접속 시 Redis key 유무를 확인하고 DB에서 복구하는 메서드 |
| Addressable 경쟁 조건 | 비동기 로딩 중 핸들 해제 시 invalid operation handle 예외 발생 |
| 상태 머신 실패 경로 | 모든 실패 케이스가 명시적 상태 전이를 가져야 WaitUntil 등이 막히지 않음 |
| 이벤트 중복 가드 | 이미 연결/입장 중인 상태에서는 같은 이벤트를 다시 처리하지 않는 패턴 |
| 인메모리 → Redis | 재시작 취약한 인메모리 검증 로직을 공유 저장소 기반으로 전환 |
| SubscribeRoom auto-retrigger | 방 상태(Starting/Playing)로 재구독 시 서버가 StartGame 흐름을 자동 재실행 |

---

## 수정된 파일 목록

| 파일 | 변경 내용 |
|------|-----------|
| `ServerAll/Shared/Shared.Packet/Packets/Packet.cs` | C_Auth/S_Auth Union 등록 제거 |
| `ServerAll/Shared/Shared.Packet/Packets/Domains/AuthPackets.cs` | 파일 제거 |
| `ServerAll/Shared/Shared.Packet/Packets/Domains/RoomPackets.cs` | C_PlayerJoin에 UserId 추가 |
| `ServerAll/SocketServer/PacketHandler/Handler/AuthHandler.cs` | 파일 제거 |
| `ServerAll/SocketServer/PacketHandler/Handler/RoomJoinLeaveHandler.cs` | Redis 기반 검증으로 전면 재작성 |
| `ServerAll/SocketServer/Session/Session.cs` | IDatabase 주입 추가 |
| `ServerAll/SocketServer/Session/SessionManager.cs` | IDatabase 전달 |
| `ServerAll/SocketServer/Program.cs` | IDatabase Singleton 등록 |
| `ServerAll/GameServer/Infrastructure/Common/Consumer/GameSessionReadyConsumer.cs` | Redis player key 선 기입 |
| `ServerAll/GameServer/API/Services/DungeonLobbyGrpcService.cs` | EnsurePlayerDataInRedisAsync 추가, Playing 방 auto-retrigger 확장 |
| `Client/.../Socket/Packets/Packet.cs` | C_Auth/S_Auth Union 등록 제거 |
| `Client/.../Socket/Packets/AuthPackets.cs` | 내용 제거 |
| `Client/.../Socket/Packets/RoomPackets.cs` | C_PlayerJoin에 UserId 추가 |
| `Client/.../Socket/Session/SocketSession.cs` | UpdateStateFromPacket Failed 수정, ConnectAsync Failed 허용, AuthenticateAsync 제거 |
| `Client/.../Socket/Session/SocketSessionState.cs` | Authenticating/Authenticated 제거 |
| `Client/.../Socket/Session/ISocketSession.cs` | AuthenticateAsync 제거 |
| `Client/.../Socket/SocketApiClient.cs` | AuthPacketHandler 등록 제거 |
| `Client/.../System/InGame/GameSessionConnector.cs` | 중복 이벤트 가드 추가, AuthenticateAsync 호출 제거 |
| `Client/.../GUI/OutGame/LobbyViewController.cs` | NavigateToGame에서 CloseRoomDetail 제거 |
| `Client/.../Tests/PlayMode/E2E/.../SocketE2ETests.cs` | C_Auth 제거, C_PlayerJoin에 UserId 전달 |
| `Client/.../Tests/EditMode/.../SocketApiClientTest.cs` | AuthPacket 관련 테스트 제거 |

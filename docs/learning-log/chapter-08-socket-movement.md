# 챕터 8 학습 로그 — SocketServer 이동 동기화

## 처음 알았던 것 vs 피드백으로 수정된 것

### Session vs PlayerState — 어디에 상태를 둘 것인가

**내가 처음 설계한 것:**
Room에 `Dictionary<long, PlayerState>` 딕셔너리를 추가해서 위치 데이터를 관리하면 된다.

**피드백:**
Room이 네트워크 멤버십 관리 + 게임 상태 관리 두 가지 책임을 갖게 되어 비대해진다.

**올바른 설계:**
Session에 `PlayerState?`를 Composition으로 붙이는 방식.

| | Room에 딕셔너리 추가 | Session Composition |
|--|--|--|
| Room 책임 | 브로드캐스트 + 상태 관리 | 브로드캐스트만 |
| 접근 속도 | O(1) (딕셔너리) | O(1) (직접 참조) |
| 코드 복잡도 | 별도 딕셔너리 + 잠금 필요 | session.PlayerState 바로 접근 |

```
Session
  ├── (네트워크) Socket, Connected, LastRecvAt
  ├── (인증)    UserId, Nickname
  └── PlayerState?  ← 인증 성공 후 초기화
        ├── PosX, PosY, PosZ
        ├── RotY
        └── LastMovedAt
```

---

### C_Auth에 RoomId가 들어있던 문제

**내가 처음 만든 것:**
```csharp
C_Auth { UserId, RoomId }  // 인증 + 방 입장을 한 패킷에
```

**피드백:**
gRPC는 로비, Socket은 인게임 — 인증과 방 입장은 다른 책임이다.

**올바른 설계:**
```
C_Auth  { UserId }         → "나 이 사람이야" (TCP 신원 확인만)
C_PlayerJoin { RoomId }    → "이 방 들어갈게" (인증 후 별도 요청)
```

gRPC 로비와 Socket 인게임의 경계를 명확히 하는 것이 핵심.

---

### 이동 동기화 좌표 타입 — int vs float

**내가 처음 만든 것:**
```csharp
public int PosX { get; set; }  // int
public int PosY { get; set; }
public int PosZ { get; set; }
```

**수정:**
```csharp
public float PosX { get; set; }  // float
public float PosY { get; set; }
public float PosZ { get; set; }
public float RotY  { get; set; }  // 추가
```

이유: Unity의 `Vector3`가 float 기반. int로 받으면 0.5 같은 소수점 위치가 소실됨.

---

### MoveHandler에서 Room 탐색 방법

**내가 처음 만든 것:**
```csharp
var room = session.RoomManager.GetAssignedRoom(session.UserId);  // O(N) 전체 방 탐색
```

**피드백:**
이동 패킷은 초당 수십 번 들어온다. 매번 모든 방을 순회하면 낭비.

**수정:**
```csharp
var room = session.Room;  // O(1) 직접 참조
```

`session.Room`은 `C_PlayerJoin` 성공 시점에 이미 세팅됨. 탐색 불필요.

---

### PlayerState 초기화 시점

**내가 처음 설계한 것:**
`C_Auth` 수신 시 PlayerState 초기화.

**피드백:**
MQ 수신 시점에 미리 초기화하는 게 맞다.

**올바른 흐름:**
```
GameStartRequestedMessage 수신
    → Room 생성
    → PlayerInfos 순회하며 InitPlayerState() 즉시 세팅  ← 이 시점
    → GameSessionReadyMessage 발행

C_PlayerJoin 수신
    → 이미 초기화된 PlayerState 조회만  (InitPlayerState 불필요)
```

이유: Room이 생성될 때 이미 누가 들어올지 알고 있음 (GameStartRequestedMessage에 PlayerInfos 포함).
늦게 초기화하면 Race Condition 가능성 있음.

---

### S_Move TimeStamp 정책

**내 처음 생각:**
서버 시간으로 덮어쓰면 일관성이 높아지지 않을까?

**피드백:**
클라이언트 TimeStamp를 그대로 릴레이해야 한다.

이유: 다른 클라이언트가 보간(interpolation)할 때 **패킷이 언제 발생했는지** 원본 시점이 필요.
서버 시간으로 덮으면 네트워크 지연이 빠진 시간이 되어 보간이 틀어짐.

```csharp
// 서버가 하는 일: 상태 저장 + 릴레이만
room.UpdatePlayerState(session.UserId, packet.PosX, packet.PosY, packet.PosZ, packet.RotY, packet.TimeStamp);
room.Broadcast(new S_Move
{
    UserId    = session.UserId,
    PosX      = packet.PosX,
    PosY      = packet.PosY,
    PosZ      = packet.PosZ,
    RotY      = packet.RotY,
    TimeStamp = packet.TimeStamp  // 클라이언트 시간 그대로
}, session.SessionId);
```

---

### IHost / BackgroundService 패턴

**이전 구조:**
`Program.cs Main()`에서 수동으로 모든 객체 생성 + `Task.Run`으로 루프 관리.

```csharp
// 이전 — 수동 관리
var consumer = new GameStartRequestedConsumer(...);
_ = Task.Run(() => consumer.RunAsync(cts.Token), cts.Token);

// Ctrl+C 처리도 수동
Console.CancelKeyPress += (s, e) => { cts.Cancel(); };
await Task.Delay(Timeout.Infinite, cts.Token);
```

**피드백:**
GameServer가 `WebApplication.CreateBuilder` 쓰는 것처럼, SocketServer도 `IHost` 패턴이 맞다.

**수정 후:**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<RoomManager>();
        services.AddHostedService<TcpListenerService>();
        services.AddHostedService<GameStartRequestedConsumer>();
        services.AddHostedService<HeartBeatService>();
    })
    .Build();

await host.RunAsync();  // Ctrl+C / SIGTERM 자동 처리
```

분리된 서비스:

| 클래스 | 책임 |
|---|---|
| `GameStartRequestedConsumer` | MQ 소비 + Room 생성 + GameSessionReady 발행 |
| `TcpListenerService` | TCP 소켓 생명주기 (Start/Stop) |
| `HeartBeatService` | 세션 타임아웃 감지 + 강제 종료 |
| `TestRoomService` | 콘솔 커맨드 기반 테스트 룸 생성 (개발 전용) |

---

## 트러블슈팅

### Redis XREADGROUP "unknown command" 에러

**증상:**
```
StackExchange.Redis.RedisServerException: ERR unknown command 'XREADGROUP'
```

**초기 의심:** Redis 버전이 5.0 미만 (XREADGROUP 지원 시점)

**디버깅 과정:**
1. `docker ps` → `redis:7-alpine` 정상 실행 확인
2. `netstat -ano | findstr :6379` → `com.docker.backend.exe` 하나만 존재 (로컬 Redis 없음)
3. `docker exec redis-cli XREADGROUP GROUP g c COUNT 10 STREAMS key` → `ERR Unbalanced` 응답
   - `unknown command`가 아님 → Redis는 명령어를 인식하고 있음
   - PowerShell에서 `>`가 파일 리다이렉션으로 해석된 것이 문제

**실제 원인:**
진단용으로 추가한 `server.Info()` 코드가 `allowAdmin=true` 없이 실행되어 서버 크래시.

**해결:** 진단 코드 제거 → 정상 동작.

**배운 것:**
- StackExchange.Redis에서 `server.Info()`는 admin 모드 필요
- 단순 연결 확인은 `mux.GetDatabase().Ping()` 사용
- PowerShell에서 `>`는 따옴표로 감싸야 redis-cli에 전달됨: `">"​`

---

## 아직 미완성인 것 (TODO)

```
GameLoop (60Hz tick 기반 브로드캐스트)
    → 현재: 클라이언트가 C_Move 보낼 때만 브로드캐스트 (이벤트 기반)
    → 목표: 서버가 주기적으로 전체 PlayerState 스냅샷 브로드캐스트 (tick 기반)

전투 시스템
    → C_Attack → 히트 판정 → S_Attack 브로드캐스트

Room.Leave 시 PlayerState 미정리
    → 플레이어가 나가도 _playerStates 딕셔너리에 잔존

LeaveRoom O(N) 개선 여지
    → GetAssignedRoom: _rooms.Values.FirstOrDefault → userId→roomId 역방향 매핑으로 O(1)
```

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Session Composition | 네트워크 레이어에 게임 상태를 포함시키는 패턴 |
| PlayerState | 인게임 위치/회전/타임스탬프를 담는 게임 상태 객체 |
| BackgroundService | .NET IHost 기반 장기 실행 서비스 추상화 |
| HeartBeat | LastRecvAt 기반 타임아웃으로 좀비 세션 제거 |
| TimeStamp 릴레이 | 서버 덮어쓰기 없이 클라이언트 시간 그대로 전달 (보간 정확도 유지) |
| MemoryPack Union | ID 기반 다형성 직렬화, 4바이트 length prefix |
| IDbContextFactory | Singleton 서비스에서 thread-safe DbContext 생성용 팩토리 |

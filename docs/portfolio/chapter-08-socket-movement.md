# 08. 이동 동기화 — 게임 상태를 누가 소유하는가

> **한 줄** — 이동 패킷 자체는 단순하다. 어려운 것은 **플레이어 상태를 세션에 붙일 것인가 방에 둘 것인가**였고, 이 결정은 한 번 뒤집혔다. 세션에 붙이면 **연결이 끊기는 순간 상태도 함께 죽기** 때문이다.
>
> **범위** 상태 소유권 · 좌표/타임스탬프 정책 · O(1) 조회 · IHost 전환
> **검증** `SocketServer.Tests` · `SocketE2ETests`(두 클라 입장·이동 브로드캐스트·퇴장·재접속)

---

## 1. 이 시점의 서버 역할 — 판정이 아니라 릴레이

```
클라A ── C_Move{x,y,z,rotY,timestamp} ──▶ SocketServer ── S_Move ──▶ 클라B, C, D
                                              │
                                         상태 저장 + 그대로 중계 (검증하지 않는다)
```

이동은 **지금도 의도적으로 클라 즉발 + 서버 릴레이**다([authority-model](../wiki/authority-model.md) 축 ③ = 반응성). 전투·HP·몬스터가 나중에 전부 서버 권위로 승격됐는데도 이동만 남긴 이유는 하나다 — **입력에서 화면까지 왕복 지연이 끼면 손맛이 죽는다.**

대가는 분명하다. 위치는 검증되지 않으므로 좌표 조작(스피드핵)이 가능하다. **"막지 못한다"가 아니라 "이 축에서는 반응성을 샀다"** 이고, 그래서 전투 판정은 이동을 신뢰하지 않고 서버가 히트박스를 재계산한다.

## 2. 이 챕터에서 뒤집힌 결정 — 상태의 소유자

### 당시 판단: Session Composition

처음엔 `Room`에 `Dictionary<long, PlayerState>`를 두려 했다가, **Room이 네트워크 멤버십과 게임 상태 두 책임을 다 갖는다**는 이유로 접었다. 대신 세션에 상태를 매달았다.

```
Session
 ├ (네트워크) Socket, Connected, LastRecvAt
 ├ (인증)     UserId, Nickname
 └ PlayerState?   ← 여기에 위치/회전
```

책임 분리 논리로는 깔끔했다. 그런데 **같은 문서 안에 이 결정과 양립할 수 없는 결론이 하나 더 있었다.**

> "`PlayerState`는 `GameStartRequestedMessage`를 받는 시점에 미리 초기화한다. 늦게 하면 경쟁 조건이 생긴다."

**그 시점에는 세션이 존재하지 않는다.** 방은 로비 이벤트로 만들어지고, 클라의 TCP 접속은 그 뒤에 온다. 상태를 세션에 매달아 두면 **초기화할 곳이 없다.** 문서 안에서 이미 모순이 드러나 있었는데 당시엔 보지 못했다.

### 현재: Room이 소유한다

```csharp
// Room.cs:25 — 처음에 기각했던 바로 그 구조
private readonly Dictionary<long, PlayerState> _playerStates = new();

// RoomManager.cs:54 — 방 생성 시점(세션 이전)에 전원 초기화
room.InitPlayerState(userId, nickname, spawnIndex, x, y, z, rotY, ...);
```

되돌아온 이유는 셋이고, **전부 나중에 생긴 요구에서 나왔다.**

| 이유 | 내용 |
|---|---|
| **초기화 시점** | 방은 세션보다 먼저 존재한다. 스폰 위치는 입장 전에 정해져 있어야 한다 |
| **재접속 유예** | 연결이 끊겨도 상태는 살아 있어야 한다 — 세션에 매달려 있으면 같이 죽는다 |
| **틱 순회** | `RoomTickService`가 방 단위로 몬스터·플레이어를 함께 순회한다. 상태가 세션에 흩어져 있으면 방 단위 시뮬레이션이 불가능하다 |

두 번째가 결정적이다. 원본 문서는 이걸 **결함으로 적어두기까지 했다.**

> TODO: "Room.Leave 시 PlayerState 미정리 — 플레이어가 나가도 `_playerStates`에 잔존"

지금 이건 **의도된 동작**이다. `Leave(sessionId, graceful: true)`는 세션 목록에서만 지우고 상태는 남긴다. 남겨야 재접속했을 때 그 자리로 돌아올 수 있다. 유예가 끝난 뒤의 정리는 `RoomTickService`가 따로 한다.

```
연결 끊김 ──▶ 세션 제거 (PlayerState 보존)  ──재접속──▶ 같은 상태로 복귀
                     │
                     └──유예 만료──▶ RoomTickService가 상태 정리 + 영구 퇴장 확정
```

> **교훈** — "책임 분리"는 옳은 원칙이지만 **어떤 축으로 자를지**는 미래 요구가 정한다. 여기서 진짜 축은 "네트워크 vs 게임"이 아니라 **"수명이 다른가"** 였다. 세션은 끊기고 다시 붙지만 플레이어 상태는 한 판 내내 살아야 한다. **수명이 다른 것을 한 객체에 묶으면 짧은 쪽이 긴 쪽을 죽인다.**

## 3. 타임스탬프를 서버가 덮어쓰지 않는 이유

"서버 시간으로 통일하면 일관성이 높아지지 않나?" — 반대다.

```
클라A: t=100 에 이동            → 네트워크 지연 40ms
서버:  t=140 에 수신
        서버 시간으로 덮어쓰면 → S_Move{timestamp=140}
클라B: "이 위치는 140 시점" 으로 보간 → 실제로는 100 시점의 위치
                                        → 지연이 통째로 보간 오차가 된다
```

보간에 필요한 것은 **패킷이 언제 발생했는가**이지 언제 도착했는가가 아니다. 서버는 클라 원본 타임스탬프를 **손대지 않고 그대로 중계**한다.

```csharp
// MovementHandler.BuildBroadcast — 서버는 UserId만 채우고 나머지는 원본 그대로
TimeStamp = packet.TimeStamp,
AnimState = packet.AnimState,   // 연출도 해석 없이 중계 (클라 권위)
```

같은 논리가 나중에 애니메이션 동기화에도 그대로 적용됐다 — 서버는 `AnimState` 1바이트를 **해석하지 않고 옮기기만** 한다([29](./chapter-29-multiplayer-sync-invisible-failures.md)).

## 4. 좌표는 `float`여야 한다

`int`로 시작했다가 바꿨다. Unity의 `Vector3`가 float이므로 `int`로 받으면 소수점이 잘린다 — 정수 격자 위로 스냅되면서 **미세하게 떨리는 이동**이 된다. 네트워크 대역을 아끼려면 정밀도를 줄이는 양자화가 정답이지, 타입을 바꾸는 건 정답이 아니다.

## 5. 초당 수십 번 도는 경로에서 O(N)을 없앤다

```csharp
// ❌ 이동 패킷마다 전체 방을 순회
var room = session.RoomManager.GetAssignedRoom(session.UserId);

// ✅ 입장 시점에 이미 연결해 둔 직접 참조
var room = session.Room;
```

**빈도가 높은 경로에서는 탐색 자체가 비용**이다. 방 참조는 `C_PlayerJoin` 성공 시 세션에 박아두므로 다시 찾을 이유가 없다.

같은 문제가 반대 방향(퇴장 시 `userId`로 방 찾기)에도 있었고, 이건 나중에 **역방향 인덱스**로 닫혔다.

```csharp
// RoomManager.cs:14
private readonly ConcurrentDictionary<long, long> _userRoomIndex = new();   // userId → roomId, O(1)
```

## 6. 인증과 입장은 다른 사건이다

```
C_Auth       { UserId }    "나 이 사람이야"    (TCP 신원)
C_PlayerJoin { RoomId }    "이 방 들어갈게"    (인게임 입장)
```

처음엔 `C_Auth`에 `RoomId`를 같이 넣었다가 분리했다. **gRPC는 로비, 소켓은 인게임**이라는 경계에서, 인증은 연결 수준이고 입장은 게임 수준이다. 하나로 합치면 "인증은 됐지만 방이 없는" 상태나 "방을 바꾸는" 상황을 표현할 수 없다.

여기서 한 걸음 더 나아가면 **`C_Auth` 자체가 필요 없어진다** — 신원은 이미 gRPC 로그인 때 서버가 알고 있고, 그걸 Redis 세션으로 확인하면 되기 때문이다([11](./chapter-11-socket-session-entry.md)).

## 7. `IHost` 전환 — 수명 관리를 프레임워크에 넘긴다

```csharp
// 이전: 수동 관리
var consumer = new GameStartRequestedConsumer(...);
_ = Task.Run(() => consumer.RunAsync(cts.Token));       // 예외가 조용히 사라진다
Console.CancelKeyPress += (s, e) => cts.Cancel();       // 종료 처리도 직접
await Task.Delay(Timeout.Infinite, cts.Token);

// 이후: IHost + BackgroundService
services.AddHostedService<TcpListenerService>();
services.AddHostedService<GameStartRequestedConsumer>();
services.AddHostedService<HeartbeatService>();
await host.RunAsync();     // Ctrl+C / SIGTERM / 순차 종료 자동
```

GameServer가 `WebApplication`을 쓰는데 SocketServer만 수동 루프를 돌 이유가 없었다. 전환하면서 책임도 갈렸다 — TCP 수명(`TcpListenerService`), 메시지 소비(`Consumer`), 좀비 세션 청소(`HeartbeatService`).

> 다만 `BackgroundService`에는 함정이 있다 — **`ExecuteAsync`가 리턴하면 다시 시작되지 않는다.** 이 문제는 나중에 `ResilientStreamConsumer`로 중앙에서 해결했다([05](./chapter-05-game-start-e2e.md) 7절).

## 8. 진단이 엉뚱한 곳을 가리킨 사례

```
StackExchange.Redis.RedisServerException: ERR unknown command 'XREADGROUP'
```

Redis 버전 문제로 보였다. 확인해 보니 컨테이너는 `redis:7-alpine`이었고, `docker exec`로 직접 때리면 명령을 **인식하고 있었다**(다른 에러가 났다). 즉 "unknown command"는 거짓 단서였다.

진짜 원인은 진단하려고 넣은 코드였다 — **`server.Info()`가 `allowAdmin=true` 없이 호출돼 연결을 망가뜨리고 있었다.** 진단 코드를 지우자 정상 동작했다.

> **교훈** — 에러 메시지가 지목하는 대상이 범인이 아닐 수 있다. 특히 **진단을 위해 추가한 코드가 증상을 만들고 있는 경우**는 원인 후보에서 빠지기 쉽다. 단순 연결 확인은 `GetDatabase().Ping()`으로 충분하다.

## 9. 이 챕터의 TODO는 어떻게 닫혔나

| 당시 TODO | 결말 |
|---|---|
| tick 기반 브로드캐스트 | ✅ `RoomTickService`(10Hz)가 방 단위로 도입 — 몬스터 AI와 함께([13](./chapter-13-monster-server-authority.md)) |
| 전투 시스템(C_Attack → 판정) | ✅ 서버 권위로 구현. 클라는 트리거만 |
| `Room.Leave` 시 상태 미정리 | 🔄 **결함이 아니라 기능이 됨** — 재접속 유예(2절) |
| `LeaveRoom` O(N) | ✅ `_userRoomIndex` 역방향 매핑 |

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 원본 타임스탬프 릴레이 | 원격 플레이어 보간 · 애니메이션 상태 중계([29](./chapter-29-multiplayer-sync-invisible-failures.md)) |
| 방이 게임 상태를 소유 | 몬스터·전투·부활이 전부 방 단위 시뮬레이션으로([13](./chapter-13-monster-server-authority.md)·[24](./chapter-24-coop-revive.md)) |
| 인증 ≠ 입장 | 세션 기반 입장 검증으로 `C_Auth` 제거([11](./chapter-11-socket-session-entry.md)) |
| 고빈도 경로엔 직접 참조 | 이동·전투 핸들러의 공통 원칙 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-08-socket-movement.md](../learning-log/chapter-08-socket-movement.md)

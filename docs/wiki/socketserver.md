# SocketServer 패턴

## Session Composition 구조

```
Session
  ├── (네트워크) Socket, Connected, LastRecvAt
  ├── (인증)    UserId, Nickname
  ├── Room?       ← C_PlayerJoin 성공 시 직접 참조 세팅
  └── PlayerState? ← GameStartRequestedMessage 수신 시 미리 초기화
```

이동 패킷에서 `session.Room`으로 O(1) 직접 접근.  
`RoomManager.GetRoom()` 탐색은 입장/퇴장 시에만 사용.

## PlayerState 초기화 타이밍

```
GameStartRequestedMessage 수신 → Room.InitPlayerState() 즉시 호출
C_PlayerJoin 수신 → 이미 초기화된 상태 조회만 (재초기화 금지)
```

누가 들어올지 이미 알고 있으므로 미리 세팅. 늦게 초기화하면 Race Condition 가능성 있음.

## 이동 동기화 정책

서버가 클라이언트 `TimeStamp`를 **그대로 릴레이**. 덮어쓰지 않음.  
이유: 다른 클라이언트 보간(interpolation)이 원본 발생 시점 기준으로 계산해야 정확함.

```csharp
room.Broadcast(new S_Move { TimeStamp = packet.TimeStamp }, excludeSessionId: session.SessionId);
```

## Room lock 규칙

`_playerSessions`, `_playerStates` 접근 시 반드시 `lock`. 기존 Room.cs 패턴 유지.

## 서비스 구조 (BackgroundService)

| 클래스 | 책임 |
|--------|------|
| `TcpListenerService` | TCP 소켓 생명주기 |
| `GameStartRequestedConsumer` | MQ 소비 → Room 생성 → PlayerState 초기화 → GameSessionReady 발행 |
| `HeartBeatService` | 30초 타임아웃, 15초 주기 체크 |
| `TestRoomService` | 콘솔 커맨드 기반 테스트 (개발 전용) |

## 미완성 항목

- Room.Leave 시 `_playerStates`에 PlayerState 잔존 (정리 로직 없음)
- `RoomManager.GetAssignedRoom`: 현재 O(N) → userId→roomId 역방향 매핑으로 O(1) 개선 여지

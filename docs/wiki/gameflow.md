# 게임 시작 E2E 흐름

## 전체 흐름 (구현 완료)

```
[클라이언트]              [GameServer]               [SocketServer]
     │                        │                            │
     ├─ StartRoom RPC ────────>│                            │
     │                        ├─ room → Starting           │
     │                        ├─ Outbox INSERT (같은 트랜잭션)
     │                        │                            │
     │                    Outbox Publisher                 │
     │                        ├─ XADD stream:game:start ──>│
     │                        │   {roomId, players, traceId}
     │                        │                            ├─ Room.Create()
     │                        │                            ├─ InitPlayerState()
     │                        │   <── SET socket:ready ────│
     │                        │                            │
     │                        ├─ room.SetSocketInfo()      │
     │                        ├─ room → Playing            │
     │                        ├─ PublishAsync(roomId) ─────>── 구독자에게 푸시
     │                        │                            │
     │<─ GameSessionReadyEvent─│                            │
     │   {ip, port}            │                            │
     │                        │                            │
     ├─ [TCP Connect] ──────────────────────────────────── >│
     ├─ C_Auth ────────────────────────────────────────── >│
     ├─ C_PlayerJoin ─────────────────────────────────── >│
     │<─ S_PlayerJoined ───────────────────────────────── │
```

## 서버 간 메시지 (`Shared.Infrastructure/Messages/`)

### GameStartRequestedMessage (GameServer → SocketServer)
```csharp
long RoomId
IReadOnlyList<PlayerInfo> PlayerInfos  // UserId, Nickname, SpawnIndex
string TraceId
```
**현재 DungeonId 없음** — 던전 연동 시 추가 필요.

### GameSessionReadyMessage (SocketServer → GameServer)
```csharp
long RoomId
string SocketIp  // "127.0.0.1" (현재 하드코딩 → appsettings 이동 예정)
int SocketPort   // 7777
string TraceId
```

## 서버가 직접 RPC 호출하지 않는 이유

- SocketServer 다운 시 GameServer도 실패 → 장애 전파 방지
- 다중 인스턴스 시 라우팅 복잡도 제거
- Redis Streams로 느슨한 결합 유지

## Consumer Group 주의사항

- `StreamPosition.Beginning("0")`: 재시작 시 미처리 메시지 재처리 가능 (ACK된 것 제외)
- `StreamPosition.NewMessages("$")`: 이미 발행된 메시지 놓침 → **사용 금지**
- `NOGROUP` 에러: 스트림 재생성 시 Consumer Group도 재생성 필요 (복구 로직 구현 완료)

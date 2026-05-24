# 네트워킹 규칙

## proto 수정 시 필수 작업

`.proto` 파일을 수정하면 **즉시** 클라이언트 `Generated/` 파일을 재생성한다. 재생성 없이는 클라이언트가 새 필드를 인식하지 못한다.

재생성 명령은 `CLAUDE.md` → "proto 수정 후 클라이언트 재생성" 섹션 참조.

## 패킷 추가 3단계 (모두 필수)

Union 등록 누락이 런타임 역직렬화 오류의 1순위 원인.

**1. 패킷 클래스** — `Shared.Packet/Packets/Domains/`
```csharp
[MemoryPackable]
public partial class C_Attack : Packet { }  // C_ = 클라→서버

[MemoryPackable]
public partial class S_Attack : Packet { }  // S_ = 서버→클라
```

**2. Union 등록** — `Shared.Packet/Packets/Packet.cs`
```csharp
[MemoryPackUnion(1600, typeof(C_Attack))]
[MemoryPackUnion(1601, typeof(S_Attack))]
```

**3. 핸들러** — `SocketServer/PacketHandler/Handler/`
```csharp
[PacketHandler(typeof(C_Attack))]
public static async ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct) { }
```

## Union ID 범위

```
1300~1399: 인증
1310~1319: 입장/퇴장
1400~1499: 유틸 (Ping/Pong)
1500~1599: 이동
1600~1699: 전투
1700~1799: 게임 라이프사이클
1800~1899: 던전 이벤트  ← 다음 추가 영역
```

## Redis 키 네임스페이스

```
user:{userId}                    → Hash
credential:{userId}              → Hash
session:{sessionId}              → Hash
session:active                   → Sorted Set (score = 만료 Unix timestamp)
room:{roomId}                    → Hash
room:active                      → Set

stream:room:{roomId}             → Stream (던전 로비 이벤트)
stream:game:start                → Stream (GameServer → SocketServer)
stream:chat:global               → Stream
stream:chat:room:{roomId}        → Stream
stream:chat:user:{nickname}      → Stream
```

`stream:` 접두사 필수. 데이터 키와 스트림 키에 같은 이름 사용 시 `WRONGTYPE` 에러 발생.

## Redis 캐시 패턴

Cache-Aside + Delete 패턴 사용. Update(덮어쓰기) 금지.
```
읽기: Redis 먼저 → MISS → PostgreSQL → Redis SET(TTL)
쓰기: PostgreSQL SaveChanges → Redis DEL (다음 읽기 때 재캐싱)
```

## Redis 트랜잭션

트랜잭션 내부 `await` 금지 — 데드락 발생.
```csharp
var tx = _database.CreateTransaction();
_ = tx.HashSetAsync(key, entries);   // await 금지, _ = 로 Task 버림
_ = tx.KeyExpireAsync(key, ttl);
await tx.ExecuteAsync();             // 여기서 한 번에 실행
```

## IBroadcastChannel vs IMessageQueue

| | IBroadcastChannel | IMessageQueue |
|--|--|--|
| 소비 방식 | 각자 독립 XREAD (Fan-out) | Consumer Group (경쟁 소비) |
| 수신자 | 1 메시지 → N명 모두 | 1 메시지 → 1명만 |
| 용도 | 채팅, 로비 이벤트 | 서버 간 작업 분배 |

## Consumer Group

`StreamPosition.Beginning("0")` 사용 — 재시작 시 미처리 메시지 재처리 가능.  
`StreamPosition.NewMessages("$")` 사용 금지 — 이미 발행된 메시지 누락.

## SocketServer 세션 패턴

```
Session
  ├── Socket, Connected, LastRecvAt   (네트워크)
  ├── UserId, Nickname                (인증)
  ├── Room?     ← C_PlayerJoin 성공 시 직접 참조 세팅
  └── PlayerState? ← GameStartRequestedMessage 수신 시 미리 초기화
```

- 이동 패킷에서 `session.Room`으로 O(1) 직접 접근. `RoomManager.GetRoom()` 탐색은 입장/퇴장에서만.
- `GameStartRequestedMessage` 수신 → `Room.InitPlayerState()` 즉시 호출. `C_PlayerJoin`에서 재초기화 금지.
- `_playerSessions`, `_playerStates` 접근 시 반드시 `lock`.
- 이동 패킷의 `TimeStamp`는 클라이언트 원본 그대로 릴레이. 서버에서 덮어쓰지 않는다.

## TCP 연결 순서

`C_Auth` → `C_PlayerJoin` 순서 필수. Auth 전 Join 요청은 서버가 거부.

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
room:ready:{roomId}              → Set (대기실 준비 완료 userId — DB 아닌 Redis 전용 휘발성)

dungeon:result:done:{roomId}     → String (보상 지급 멱등 claim, SET NX EX 24h)
loot:pickup:done:{pickupId}      → String (아이템 지급 멱등 claim, SET NX EX 24h)

stream:room:{roomId}             → Stream (던전 로비 이벤트)
stream:game:start                → Stream (GameServer → SocketServer)
stream:chat:global               → Stream
stream:chat:room:{roomId}        → Stream
stream:chat:user:{nickname}      → Stream
```

`stream:` 접두사 필수. 데이터 키와 스트림 키에 같은 이름 사용 시 `WRONGTYPE` 에러 발생.

## 멱등 처리 기록은 항목별 키로 (F3 재발 방지)

비멱등 지급(AddExp·AddQuantity·지갑 적립)을 스트림에서 소비할 때 "처리했음" 기록을
**집합 1키 + 컬렉션 EXPIRE** 로 두지 않는다. 추가마다 TTL 이 통째로 갱신되고, 무이벤트 구간이
TTL 을 넘기면 **모든 기록이 동시에 소멸**해 늦은 재배달이 이중 지급이 된다.

```csharp
// ❌ 집합 1키 — 만료되면 기록 전멸
await redis.SetAddAsync(Key(), id);
await redis.KeyExpireAsync(Key(), ttl);

// ✅ 항목별 키 — 각 기록이 자기 처리 시각 기준 수명. claim 도 1왕복 원자.
bool claimed = await redis.StringSetAsync(Key(id), "1", ttl, When.NotExists);
if (!claimed) return; // 이미 처리됨
```

SortedSet 인덱스처럼 항목별 TTL 이 불가능한 자료구조는, 대신 **캐시를 못 믿는 조건을 명시하고
DB(진실원)로 폴백**한다. "1건이라도 나왔으니 캐시 히트" 는 부분 만료를 정상 응답으로 위장시킨다.

## Redis 캐시 패턴 (Repository 읽기/쓰기 규칙 — 필수)

Cache-Aside + Delete 패턴. 모든 도메인 Repository는 이 3원칙을 동일하게 따른다.

```
Get    : Redis 먼저 → HIT 즉시 반환 / MISS → PostgreSQL 읽기 → Redis SET(TTL) → 반환
Update : PostgreSQL SaveChanges → Redis DEL  (다음 Get이 DB에서 재캐싱)
Delete : PostgreSQL 삭제 → Redis DEL
```

**1. Get은 항상 캐시 우선.** Redis HIT이면 DB를 보지 않는다 (성능).
**2. Update는 절대 캐시를 덮어쓰지 않는다.** DB 갱신 후 캐시 DEL만 한다. (`HashSet`으로 캐시 직접 갱신 금지 — stale·부분갱신 위험)
**3. 캐시 MISS 시 DB 읽기는 반드시 `AsNoTracking()`.**

### 예외 1건 — Main 위치(`UserPositionRepository`)

**이 도메인만 Redis 가 1차 저장소다.** 주기 보고라 쓰기가 매우 잦고 유실이 허용되는 유일한 데이터라,
주기 쓰기는 Redis 로만 가고 **이탈 시점(로그아웃·던전 입장)에 한 번 DB 로 확정**한다.
유실 폭이 "마지막 확정 이후"로 한정되고, 그때는 마지막 정상 이탈 위치로 되돌아간다.

**다른 도메인에 복사하지 말 것.** 이 예외가 성립하는 조건은 셋이다 —
① 쓰기 빈도가 매우 높다 ② 유실돼도 게임이 깨지지 않는다 ③ 폴백(저작 스폰)이 존재한다.
셋 중 하나라도 아니면 위의 Cache-Aside + Delete 를 그대로 쓴다.

### `AsNoTracking()`이 필수인 이유 (실제 버그 사례)

DB 폴백 읽기를 추적 쿼리로 하면 **오래 유지되는(long-lived) DbContext에서 stale 엔티티를 반환**한다.

- `GameServerDbContext`는 Scoped. **스트리밍 RPC(`SubscribeRoom` 등)는 한 스코프 = 한 DbContext를 수십 초 유지**한다.
- 추적 쿼리는 EF identity map에 먼저 적재된 엔티티를 그대로 돌려주고 **DB 최신값으로 덮어쓰지 않는다.**
- 결과: 다른 스코프(Consumer 등)가 DB에 쓴 변경을 **그 스트림이 끝날 때까지 영원히 못 읽는다.**
  - 실제로 SendLoop이 `Starting`을 계속 읽어 `GameSessionEvent` 대신 `UpdateEvent`만 전송 → 클라가 던전 입장 못 함.

```csharp
// ❌ 추적 쿼리 — stale 엔티티 반환 위험
var room = await context.DungeonRooms.SingleOrDefaultAsync(r => r.RoomId == id, ct);

// ✅ cache-aside 읽기 전용이므로 추적 불필요. 항상 DB 최신값.
var room = await context.DungeonRooms.AsNoTracking().SingleOrDefaultAsync(r => r.RoomId == id, ct);
```

**원칙: 이벤트는 "ID + 다시 읽어라"는 트리거일 뿐. 최신 상태는 항상 DB(진실의 원천)에서 읽는다.**
모든 컴포넌트가 같은 DB를 단일 진실로 바라봐야 일관성이 보장된다. 캐시·EF 추적 메모리를 진실로 삼지 않는다.

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

**소비 루프를 새로 쓰지 않는다.** `RedisMessageQueueBase<T>.ConsumeGroupAsync(group, consumer, logger, ct)` 를 쓴다 —
EnsureGroup · 내 PEL 재읽기 · `XREADGROUP` · **`XAUTOCLAIM` 회수** · ACK 가 전부 그 안에 있다.
파생 큐는 스트림 키 · 그룹 이름 · 발행 여부만 정한다.

```csharp
public override IAsyncEnumerable<T> DequeueAllAsync(CancellationToken ct = default)
    => ConsumeGroupAsync(GroupName, ConsumerName, logger, ct);
```

- **컨슈머 이름은 안정적이어야 한다** — `StableConsumerName(prefix)` (= `{prefix}-{MachineName}`).
  매 기동 `Guid.NewGuid()` 를 붙이면 재시작 때 **빈 새 PEL** 을 읽게 돼 이전 생의 미ACK 메시지를 영영 못 본다.
- **역직렬화에 실패한 엔트리도 ACK 한다.** PEL 에 남기면 회수 로직이 매 스윕마다 같은 독을 다시 집는다.

## 전달 의미는 at-least-once — 핸들러는 멱등해야 한다

ACK 는 **핸들러가 성공한 뒤**에 한다(`StreamMessage<T>` 봉투). 던지면 ACK 하지 않아 재배달된다.

```
XREADGROUP → 봉투 → 핸들러 ─성공→ AcknowledgeAsync()
                     └실패→ ACK 안 함 → PEL 잔류 → XAUTOCLAIM 재배달
```

**새 컨슈머를 만들면 핸들러 멱등성을 먼저 확인한다.** 재실행이 상태를 두 번 바꾸면 안 된다.
- 상태 전이는 **가드로** 멱등화한다(`if (room.Status == Starting)`, `GetByRoomIdAsync` 재사용).
- 비멱등 누적(`+=`, `+heal`)은 **멱등키**가 필요하다. 메시지에 고유 id 가 없으면 발행부에 추가한다.

## 비멱등 지급은 DB 원장으로 (Redis 키로 잠그지 않는다)

Exp 적립·인벤토리 증가·지갑 적립처럼 **되돌릴 수 없는 누적**은 `IRewardLedger.GrantOnceAsync` 를 쓴다.

```csharp
await ledger.GrantOnceAsync(
    new RewardGrantRequest($"dungeon:{roomId}:{userId}", userId, "exp", "", amount),
    token => progressionService.AddExpAsync(userId, amount, token),
    ct);
```

- 원장 INSERT(`GrantKey` UNIQUE)와 지급이 **같은 트랜잭션**이라 exactly-once 가 성립한다.
  Redis 키로 잠그면 키와 지급이 다른 저장소라 "지급됐는데 기록이 없다 / 그 반대" 창이 항상 남는다.
- **멱등 단위를 최소 지급 단위로 내린다.** 참가자별로 키를 갈라야 부분 실패 후 나머지만 마저 줄 수 있다.
- **지급 1건 = 스코프 1개.** 실패한 DbContext 의 변경 추적기를 재사용하면 롤백된 변경이 다음 저장에 새어 나간다.

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

## TCP 연결 순서 — 소켓 전용 인증 패킷은 없다

**`C_Auth`/`S_Auth`는 제거됐다.** 소켓 인증은 `C_PlayerJoin`의 **Redis 검증**이 대신한다.

```
TCP 연결 → C_PlayerJoin { RoomId, UserId }
             → SocketServer: HGETALL gamesession:player:{userId}
             → roomId 일치 확인 (불일치·키 없음 = 거부)
             → S_PlayerJoined { Success }
```

- 검증 데이터는 **GameServer 가 이벤트 발행 *전에* 선기입**한다(`GameSessionReadyConsumer`).
- 인메모리 `_userRoomIndex` 를 인증 근거로 삼지 않는다(프로세스 재시작에 소실).
- 상세 = [docs/portfolio/chapter-11-socket-session-entry.md](../../docs/portfolio/chapter-11-socket-session-entry.md)

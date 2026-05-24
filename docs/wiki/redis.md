# Redis 사용 규칙

## 키 네임스페이스

```
user:{userId}                    → Hash  (유저 캐시)
credential:{userId}              → Hash  (인증 정보 캐시)
session:{sessionId}              → Hash  (세션 캐시)
session:active                   → Sorted Set (score = 만료 Unix timestamp)
room:{roomId}                    → Hash  (방 캐시)
room:active                      → Set   (활성 방 목록)

stream:room:{roomId}             → Stream (던전 로비 이벤트)
stream:chat:global               → Stream (전역 채팅)
stream:chat:room:{roomId}        → Stream (방 채팅)
stream:chat:user:{nickname}      → Stream (귓속말)
```

**`stream:` 접두사는 필수.** 데이터 키와 스트림 키가 같은 이름이면 `WRONGTYPE` 에러 발생.  
(Redis는 하나의 키에 하나의 타입만 허용)

## Cache Aside + Delete 패턴

```
읽기: Redis 먼저 → MISS → PostgreSQL → Redis SET(TTL)
쓰기: PostgreSQL SaveChanges → Redis DEL (다음 읽기 때 재캐싱)
```

Update 패턴(덮어쓰기)을 쓰지 않는 이유: DB 저장과 Redis 저장 사이 타이밍 문제로 구버전이 남을 수 있음.

## IBroadcastChannel vs IMessageQueue

| | IBroadcastChannel | IMessageQueue |
|--|--|--|
| 소비 방식 | 각자 독립 XREAD (Fan-out) | Consumer Group (경쟁 소비) |
| 수신자 | 1 메시지 → N명 모두 | 1 메시지 → 1명만 |
| 용도 | 채팅, 로비 이벤트 | 서버 간 작업 분배 |

## Redis Transaction — fire-and-forget 패턴

```csharp
var tx = _database.CreateTransaction();
_ = tx.HashSetAsync(key, entries);      // await 금지 (데드락 발생)
_ = tx.KeyExpireAsync(key, ttl);
await tx.ExecuteAsync();                // 여기서 한 번에 실행
```

트랜잭션 내부에서 `await` 사용 시 데드락. `_ =` 로 Task를 버리고 `ExecuteAsync`에서 일괄 실행.

## TTL 규칙

| 키 | TTL |
|----|-----|
| `session:{id}` | JWT AccessToken 만료 시간 (15분) |
| `user:*`, `credential:*` 등 캐시 키 | `RedisSettings.RedisCacheTtl` (30분) |

## Sorted Set을 이용한 만료 세션 관리

```csharp
// score = 만료 Unix timestamp
SortedSetAddAsync("session:active", sessionId, expiresAt.ToUnixTimeSeconds());

// 만료된 것 정리 (한 줄)
SortedSetRemoveRangeByScoreAsync("session:active", 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
```

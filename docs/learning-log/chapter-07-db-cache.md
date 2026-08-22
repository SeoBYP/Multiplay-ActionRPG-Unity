# Chapter 07 — DB + Redis 캐시 레이어 (Cache Aside + 통합 테스트)

## 설계 배경 (Why)

서버에서 DB를 직접 쓰면 단순하다. 근데 문제가 생긴다.

```
클라이언트 → GameServer → PostgreSQL
```

- 인증마다 `UserCredential` 조회
- 로비 입장마다 `DungeonRoom` 조회
- 채팅 메시지마다 `ChatMessage` 삽입

요청이 늘어나면 DB가 병목이 된다. 게임 서버는 특히 동시 접속자가 많을 때 짧은 시간에 동일 데이터를 반복 조회한다 (같은 방에 있는 플레이어 4명이 방 정보를 동시에 요청).

**해결**: DB 앞에 Redis 캐시 레이어를 둔다.

그런데 캐시 전략에도 여러 가지가 있다:

| 전략 | 설명 | 단점 |
|---|---|---|
| Cache Aside + Update | 쓰기 시 DB + Redis 동시 업데이트 | 동시성 문제, Race Condition |
| TTL 중심 | 일정 시간 후 자동 만료 | Stale 데이터 허용 |
| 이벤트 기반 무효화 | MQ로 캐시 무효화 이벤트 발행 | 인프라 복잡도 증가 |
| **Cache Aside + Delete** | 쓰기 시 DB 저장 후 캐시 삭제 | 다음 읽기에 캐시 미스 1회 |

**Cache Aside + Delete를 선택한 이유:**
- User 같은 엔티티는 생성 후 거의 변경되지 않음 → 쓰기 빈도 낮음
- 캐시와 DB 간 일관성이 최우선 (게임 세션, 인증 데이터)
- 구현이 단순 → Repository 레이어에서 일관되게 적용 가능

---

## 아키텍처

```
Repository 계층 (Cache Aside + Delete)

읽기:
  ┌──────────┐    HIT     ┌───────┐
  │ GetAsync │──────────→│ Redis │ → 반환
  │          │   MISS     └───────┘
  │          │──────────→ PostgreSQL → Redis SET(TTL) → 반환
  └──────────┘

쓰기:
  ┌────────────┐
  │ UpdateAsync│──→ PostgreSQL SaveChanges
  │            │──→ Redis DEL (캐시 무효화)
  └────────────┘
  ← 다음 읽기 시 MISS → DB 조회 → 재캐싱
```

Redis 키 구조:
```
user:{userId}                    → Hash (UserId, PublicId, ...)
user:publicid:{publicId}         → String (userId 역방향 매핑)
credential:{userId}              → Hash (Email, PasswordHash, RefreshToken, ...)
session:{sessionId}              → Hash (UserId, CreatedAt, ExpiresAt, ...)
session:user:{userId}            → String (sessionId 역방향 매핑)
session:active                   → Sorted Set (score = 만료 Unix timestamp)
room:{roomId}                    → Hash (RoomName, HostId, Status, ...)
room:active                      → Set (활성 방 목록)
chat:message:{messageId}         → Hash (SenderName, Message, ChatType, ...)
chat:all                         → Sorted Set (score = messageId)
```

---

## 핵심 구현

### 1. Cache Aside 읽기 패턴

```csharp
public async Task<User?> GetByIdAsync(long userId)
{
    // 1. Redis 먼저 확인 (HIT)
    var cached = await GetUserCacheAsync(userId);
    if (cached is not null)
        return cached;

    // 2. MISS → DB 조회
    var user = await _context.Users.AsNoTracking()
        .FirstOrDefaultAsync(u => u.UserId == userId);
    if (user is null)
        throw new KeyNotFoundException($"User {userId} not found");

    // 3. Redis에 캐싱 (TTL 포함)
    await SetUserCacheAsync(user);
    return user;
}
```

### 2. Cache Aside Delete 쓰기 패턴

```csharp
public async Task UpdateAsync(User user)
{
    // 1. DB 저장
    _context.Users.Update(user);
    await _context.SaveChangesAsync();

    // 2. 캐시 삭제 (다음 읽기에 DB에서 재캐싱)
    await DeleteUserCacheAsync(user.UserId, user.PublicId);
}
```

**왜 Update가 아니라 Delete인가?**
Update 패턴은 DB 저장과 Redis 저장 사이에 타이밍 문제가 생긴다.

```
Thread A: DB 저장
Thread B: Redis 조회 (아직 UPDATE 전 → 구버전 반환)
Thread A: Redis UPDATE
```

Delete하면 Thread B가 MISS를 받고 DB에서 최신 값을 가져온다.

### 3. Redis Transaction — fire-and-forget 패턴

```csharp
private async Task SetUserCacheAsync(User user)
{
    var transaction = _database.CreateTransaction();

    // ⚠️ 트랜잭션 내부에서는 await 금지
    // _ = 로 Task를 버려야 함 (실제 실행은 ExecuteAsync에서)
    _ = transaction.HashSetAsync(userKey, hashEntries);
    _ = transaction.KeyExpireAsync(userKey, RedisSettings.RedisCacheTtl);
    _ = transaction.StringSetAsync(mappingKey, user.UserId.ToString(), RedisSettings.RedisCacheTtl);

    await transaction.ExecuteAsync();  // 여기서 한 번에 실행
}
```

트랜잭션 내부에서 `await`를 사용하면 데드락이 발생한다.
Redis 트랜잭션은 MULTI/EXEC 블록으로 명령을 모아서 한 번에 보내야 한다.

### 4. TTL 분리 — SessionTtl vs RedisCacheTtl

```csharp
// ❌ 잘못된 이해: 모든 Redis 키에 같은 TTL
_ = transaction.KeyExpireAsync(sessionKey, RedisSettings.RedisCacheTtl);  // 30분

// ✅ 올바른 이해: 용도에 따라 TTL 분리
_ = transaction.KeyExpireAsync(sessionKey, sessionTtl);         // JWT 만료 시간 (15분)
_ = transaction.KeyExpireAsync(userCacheKey, RedisCacheTtl);    // 캐시 TTL (30분)
```

| 키 종류 | TTL | 이유 |
|---|---|---|
| `session:{id}` | `JwtOptions.AccessTokenMinutes` | JWT 만료와 같아야 함 |
| `session:user:{id}` | `JwtOptions.AccessTokenMinutes` | 세션과 생명주기 동일 |
| `user:{id}`, `credential:{id}` 등 | `RedisSettings.RedisCacheTtl` (30분) | 순수 캐시, 만료 후 DB에서 재캐싱 |

### 5. ActiveSessions — Set에서 Sorted Set으로

처음엔 Redis Set을 사용했다:

```csharp
// ❌ Set 방식 — 멤버 개별 TTL 없음
await _database.SetAddAsync("session:active", sessionId);

// 만료된 세션을 어떻게 제거하나?
// → Background Service에서 주기적으로 모든 세션 조회 후 DB 비교 필요
// → 비효율적
```

Sorted Set으로 변경:

```csharp
// ✅ Sorted Set — score = 만료 Unix timestamp
_ = transaction.SortedSetAddAsync(
    "session:active",
    session.SessionId,
    DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds()  // score = 만료 시각
);

// 활성 세션 수 조회 (만료 안 된 것만)
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
return await _database.SortedSetLengthAsync("session:active", now, double.PositiveInfinity);

// 만료된 세션 정리 (한 줄)
await _database.SortedSetRemoveRangeByScoreAsync("session:active", 0, now);
```

score를 만료 타임스탬프로 쓰면 범위 쿼리 하나로 활성/만료 세션을 구분할 수 있다.
Background Service 없이 정리 가능.

### 6. 복수 엔티티 병렬 캐싱

```csharp
// ❌ 순차 처리
foreach (var user in dbUsers)
    await SetUserCacheAsync(user);

// ✅ 병렬 처리
await Task.WhenAll(dbUsers.Select(SetUserCacheAsync));
```

---

## 발생한 버그들

### Bug 1: RefreshToken null → empty → null 왕복

```csharp
// 저장: null을 빈 문자열로 저장 (Redis Hash는 null 저장 불가)
dict["RefreshToken"] = credential.RefreshToken ?? string.Empty;

// 읽기: 빈 문자열을 null로 복원 안 함
dict.TryGetValue("RefreshToken", out var refreshToken);
return new UserCredential { RefreshToken = refreshToken };  // ← "" 반환
```

인증 로직에서 `credential.RefreshToken == null`로 판단해야 하는데 `""`가 오면 다르게 동작한다.

```csharp
// 수정: 빈 문자열 → null 복원
dict.TryGetValue("RefreshToken", out var refreshToken);
var normalizedToken = string.IsNullOrEmpty(refreshToken) ? null : refreshToken;
```

### Bug 2: 트랜잭션 내 await 데드락

```csharp
// ❌ 데드락 발생
var transaction = _database.CreateTransaction();
await transaction.KeyDeleteAsync(profileKey);      // ← await 사용
await transaction.ExecuteAsync();
```

```csharp
// ✅ fire-and-forget
var transaction = _database.CreateTransaction();
_ = transaction.KeyDeleteAsync(profileKey);         // ← _ = 로 버림
await transaction.ExecuteAsync();
```

`await`를 트랜잭션 내부에서 쓰면 `ExecuteAsync()` 전에 실행을 기다리려고 해서 데드락이 생긴다.

### Bug 3: DungeonRoomPlayerRepository — 삭제 후 빈 목록으로 캐시 정리

```csharp
// ❌ DB 삭제 후 DB에서 조회 → 이미 없어서 빈 배열
await context.DeleteRangeAsync(players);
await context.SaveChangesAsync();

var remainingPlayers = await GetPlayersByRoomIdAsync(roomId);  // []
foreach (var p in remainingPlayers)
    await DeleteCacheAsync(roomId, p.UserId);  // 아무것도 삭제 안 됨
```

```csharp
// ✅ DB 삭제 전에 목록을 먼저 꺼냄
var dbPlayers = await context.DungeonRoomPlayers
    .Where(p => p.RoomId == roomId).ToListAsync();

await context.DeleteRangeAsync(dbPlayers);
await context.SaveChangesAsync();

// 삭제 전 목록으로 캐시 정리
var deleteTasks = dbPlayers.Select(p => DeleteCacheAsync(roomId, p.UserId));
await Task.WhenAll(deleteTasks);
```

### Bug 4: Sorted Set 마이그레이션 후 이전 API 잔존

```csharp
// ActiveSessionsKey를 Sorted Set으로 변경했는데
// 이전 Set API가 그대로 남아 있어서 WRONGTYPE Redis 에러

// ❌ Set API (이전 코드)
var sessionIds = await _database.SetMembersAsync(ActiveSessionsKey);

// ✅ Sorted Set API
var sessionIds = await _database.SortedSetRangeByScoreAsync(
    ActiveSessionsKey, now, double.PositiveInfinity);
```

Redis 자료구조를 바꾸면 **읽기/쓰기 API를 전부** 함께 변경해야 한다.
바꾼 곳이 하나라도 남아있으면 런타임에 `WRONGTYPE` 에러가 발생한다.

---

## 통합 테스트 — Testcontainers

Repository 단위는 Fake로 충분하지만 캐시 전략 자체는 실제 DB + Redis로 검증해야 한다.

```
테스트 피라미드

  E2E           GameStartE2ETest (실제 gRPC 서버 + Fake)
  Integration   RepositoryIntegrationTests ← 이번에 추가
  Application   AuthServiceTests, ChatServiceTests (Fake 레포)
  Domain        UserTests, DungeonRoomTests (순수 엔티티)
```

### Testcontainers Fixture

```csharp
public class RepositoryTestFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("gamedb").WithUsername("gameuser").WithPassword("gamepass123")
            .Build();
        RedisContainer = new RedisBuilder().Build();

        // 병렬 시작
        await Task.WhenAll(PostgreSqlContainer.StartAsync(), RedisContainer.StartAsync());

        _redisConnection = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);

        using var context = CreateDbContext();
        await context.Database.EnsureCreatedAsync();  // 스키마 생성
    }
}
```

### Cache HIT 검증 패턴

```csharp
[Fact]
public async Task Read_Hit_ShouldReturnFromCacheWithoutDbAccess()
{
    var user = await repository.CreateAsync();

    // DB에서 직접 삭제 (캐시는 유지)
    context.Users.Remove(user);
    await context.SaveChangesAsync();

    // 캐시에서 반환되어야 함 (DB에 없으므로 DB 조회 시 실패)
    var found = await repository.GetByIdAsync(user.UserId);

    Assert.NotNull(found);  // 캐시 HIT 증명
}
```

### Update assertion — 새 context 사용

```csharp
// ❌ EF Change Tracker가 메모리 캐시 반환 (DB 실제 상태 미검증)
var dbUser = await context.Users.FindAsync(user.UserId);
Assert.Equal(newValue, dbUser?.Field);

// ✅ 새 context → EF 캐시 우회 → 실제 DB 조회
using var assertContext = _fixture.CreateDbContext();
var dbUser = await assertContext.Users.FindAsync(user.UserId);
Assert.Equal(newValue, dbUser?.Field);
```

EF Core는 `FindAsync`에서 동일 context의 Change Tracker에 이미 추적 중인 엔티티가 있으면 DB 조회 없이 반환한다. Update 이후 검증은 항상 새 context 인스턴스를 사용해야 한다.

---

## 시니어 리뷰

### Cache Aside의 한계

**Write-Heavy 엔티티에는 비효율적:**
쓸 때마다 캐시를 지우고, 읽을 때마다 다시 올린다.
`ChatMessage`처럼 쓰기가 빈번한 엔티티는 캐시 미스가 계속 발생한다.
채팅은 Read-Through + TTL 전략이 더 적합할 수 있다.

**Cache Stampede 가능성:**
동시에 많은 요청이 같은 캐시 키를 MISS하면 모두 DB로 직행한다.
해결: Redis `SET NX` (Mutex) 또는 `Probabilistic Early Expiration` 기법.
현재 구현에서는 쓰기 빈도가 낮아 실질적 문제는 없다.

### Testcontainers 주의점

**EF `EnsureCreatedAsync` vs `MigrateAsync`:**
- `EnsureCreatedAsync`: 마이그레이션 히스토리 없이 스키마 생성. 빠르고 간단.
- `MigrateAsync`: 실제 마이그레이션 파일 순서대로 적용. 프로덕션 스키마와 동일 보장.

포트폴리오 단계에서는 `EnsureCreatedAsync`로 충분하지만, 실제 서비스에서는 `MigrateAsync` 권장.

**공유 Fixture vs 테스트별 독립 DB:**
현재 모든 통합 테스트가 `[Collection("RepositoryIntegrationTests")]`로 같은 컨테이너를 공유한다.
- 장점: 컨테이너 시작 비용 1회
- 단점: 테스트 간 데이터 잔존 가능

각 테스트가 `UserRepository.CreateAsync()`로 새 User를 만들어 ID가 겹치지 않으므로 현재는 문제없다.

---

## 다음 단계

- [ ] `DungeonRoom.Update` 테스트에서 수동 DB 저장 + `repository.UpdateAsync()` 중복 제거
- [ ] `UserRepository.Update` 테스트 — Reflection 대신 도메인 메서드 사용 검토
- [ ] Cache Stampede 방어 — 고트래픽 시나리오 시 `SET NX` Mutex 패턴 적용
- [ ] `EnsureCreatedAsync` → `MigrateAsync` 전환 (실제 마이그레이션 파일 작성 후)

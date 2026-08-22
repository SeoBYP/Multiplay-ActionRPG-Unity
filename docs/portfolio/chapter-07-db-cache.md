# 07. DB + Redis 캐시 — 무효화는 지우는 것이 아니라 "믿지 않는 것"이다

> **한 줄** — Cache-Aside + **Delete**로 캐시 일관성을 잡았는데, 그러고도 낡은 값이 반환됐다. 원인은 **캐시가 한 층이 아니라 두 층**이었기 때문이다 — Redis를 지워도 EF Core의 추적 메모리가 옛 엔티티를 계속 돌려주고 있었다.
>
> **범위** 캐시 전략 선택 · 무효화 · Redis 자료구조 · 트랜잭션 · Testcontainers 통합 테스트
> **검증** `RepositoryIntegrationTests`(Testcontainers: PostgreSQL + Redis 실제 컨테이너)

---

## 1. 왜 캐시인가 — 같은 데이터를 동시에 반복해서 묻는다

게임 서버의 읽기 패턴은 웹과 다르다. **같은 방의 4명이 같은 순간에 같은 방 정보를 요청**한다. 인증은 요청마다 크리덴셜을 보고, 로비는 갱신마다 방을 본다.

이 데이터들의 공통점은 **읽기는 폭발적이고 쓰기는 드물다**는 것이다. 캐시가 가장 잘 듣는 형태다.

## 2. 전략 선택 — 네 가지 중 왜 Delete인가

| 전략 | 쓰기 시 동작 | 문제 |
|---|---|---|
| Cache-Aside + **Update** | DB 저장 + 캐시도 갱신 | 두 저장소 갱신 사이에 경쟁 창이 생긴다 |
| TTL 중심 | 시간 지나면 만료 | 만료 전까지 stale을 **의도적으로 허용** |
| 이벤트 기반 무효화 | MQ로 무효화 이벤트 발행 | 인프라 한 겹 추가 |
| **Cache-Aside + Delete** | DB 저장 후 캐시 **삭제** | 다음 읽기에 미스 1회 |

인증·세션·방 상태는 **stale이 곧 버그**다(만료된 세션으로 입장, 이미 시작된 방에 입장). TTL 중심은 그래서 탈락. 이벤트 기반은 이 규모에서 과했다.

### 왜 갱신이 아니라 삭제인가

```
[Update 방식의 경쟁 창]
 Thread A: DB 저장 완료 ─────────────┐
 Thread B:      Redis 조회 → 옛 값 반환 💥   ← 아직 A가 캐시를 갱신하기 전
 Thread A: Redis 갱신 ───────────────┘

[Delete 방식]
 Thread A: DB 저장 → Redis DEL
 Thread B:      Redis 조회 → MISS → DB 조회 → 항상 최신 ✅
```

**캐시를 갱신하려 들면 "무엇이 최신인가"를 캐시가 판단해야 한다.** 삭제하면 그 판단이 필요 없어진다 — 모르면 진실원에 물어보게 만든다. 대가는 미스 한 번뿐이다.

이 규칙은 세 줄로 정리돼 프로젝트 전역 규칙이 됐다.

```
Get    : Redis → HIT 즉시 반환 / MISS → DB 읽기 → Redis SET(TTL) → 반환
Update : DB SaveChanges → Redis DEL          (절대 덮어쓰지 않는다)
Delete : DB 삭제 → Redis DEL
```

## 3. 그런데도 낡은 값이 왔다 — 이 프로젝트에서 가장 비싼 버그

증상은 캐시 문제로 보이지 않았다. **던전 입장이 안 됐다.**

```
방 상태: Waiting → Starting → (SocketServer 준비) → Playing
                                                      │
로비 구독 스트림의 SendLoop이 방을 다시 읽는다 ────────┘
   기대: Playing 을 읽고 GameSessionEvent(접속 정보) 전송
   실제: 몇 번을 읽어도 Starting → UpdateEvent 만 계속 전송 → 클라는 영원히 대기
```

Redis 캐시는 정상적으로 지워지고 있었다. DB에는 `Playing`이 들어 있었다. 그런데 **읽으면 `Starting`이 나왔다.**

### 원인 — 캐시가 두 층이었다

```
        [내가 인지한 구조]              [실제 구조]

         Repository                     Repository
             │                              │
        ┌────┴────┐                    ┌────┴────┐
        │  Redis  │ ← DEL 함           │  Redis  │ ← DEL 함 (정상 동작)
        └────┬────┘                    └────┬────┘
             │                              │
        ┌────┴────┐                  ┌──────┴───────┐
        │PostgreSQL│                 │ EF ChangeTracker │ ← ★ 아무도 안 지움
        └─────────┘                  │  (identity map)  │
                                     └──────┬───────┘
                                            │
                                       PostgreSQL
```

EF Core의 **추적 쿼리는 identity map에 이미 있는 엔티티를 그대로 돌려준다.** DB에서 새 값을 읽어와도 **기존 인스턴스를 덮어쓰지 않는다.** 이게 정상 동작이다 — 추적 중인 엔티티를 마음대로 갈아치우면 사용자의 수정 사항이 날아가기 때문이다.

문제는 **DbContext의 수명**이었다.

```
일반 RPC        요청 시작 → DbContext 생성 → 처리 → 폐기   (수십 ms, 문제 없음)
스트리밍 RPC    구독 시작 → DbContext 생성 → ... 수십 초 유지 ... → 종료
                             └ 처음 읽은 Starting 이 이 스코프가 끝날 때까지 살아 있다
```

`GameServerDbContext`는 Scoped다. `SubscribeRoom` 같은 **서버 스트리밍은 한 스코프가 수십 초 유지**된다. 그 안에서 처음 읽은 엔티티가 identity map에 자리를 잡으면, **다른 프로세스(Consumer)가 DB에 무엇을 쓰든 그 스트림은 끝날 때까지 옛 값만 본다.**

### 수정

```csharp
// ❌ 추적 쿼리 — identity map의 옛 엔티티를 반환할 수 있다
var room = await context.DungeonRooms.SingleOrDefaultAsync(r => r.RoomId == id, ct);

// ✅ cache-aside 폴백은 읽기 전용이다. 추적할 이유가 없다.
var room = await context.DungeonRooms.AsNoTracking().SingleOrDefaultAsync(r => r.RoomId == id, ct);
```

현재 Infrastructure 전반에 `AsNoTracking`이 **53곳** 적용돼 있다. 남아 있는 추적 쿼리는 전부 **읽고 수정/삭제할 엔티티**를 가져오는 자리다(`DeleteAsync`가 지울 대상을 조회하는 등) — 그 경우엔 추적이 목적이므로 맞다.

> **교훈 세 가지**
> 1. **캐시는 내가 만든 것만이 아니다.** ORM·HTTP 클라이언트·CDN 모두 캐시를 갖는다. 무효화 설계는 그 전부를 세어야 한다.
> 2. **수명이 길어지면 성질이 바뀐다.** 요청 단위에서 안전한 코드가 스트리밍에서 깨졌다. 스코프 수명은 성능 문제가 아니라 **정확성 문제**다.
> 3. **읽기 전용이라고 선언하면 문제가 사라진다.** `AsNoTracking`은 성능 최적화로 알려져 있지만 여기서는 **정확성 장치**로 쓰였다. 의도를 코드에 적으면 엔진이 알아서 맞춰준다.

이 사건 이후 규칙이 하나 추가됐다 — **"이벤트는 ID + 다시 읽어라는 트리거일 뿐, 최신 상태는 항상 DB에서 읽는다. 캐시도 EF 추적 메모리도 진실원이 아니다."**([03](./chapter-03-dungeon-lobby.md)의 채널이 `RoomId`만 나르는 이유가 이것이다.)

## 4. 자료구조를 바꿔 백그라운드 작업을 없앤 사례

활성 세션 목록을 Redis **Set**으로 관리했었다. 그런데 **Set의 멤버에는 개별 TTL을 걸 수 없다.** 만료된 세션을 걷어내려면 주기적으로 전체를 훑어 DB와 대조하는 백그라운드 서비스가 필요했다.

**Sorted Set**으로 바꾸면서 `score = 만료 Unix timestamp`로 뒀다.

```csharp
SortedSetAdd("session:active", sessionId, 만료시각.ToUnixTimeSeconds());

// 활성 세션 수  = 지금 이후 만료되는 것만 센다
SortedSetLength("session:active", now, double.PositiveInfinity);

// 만료분 정리   = 한 줄
SortedSetRemoveRangeByScore("session:active", 0, now);
```

**"시간"을 score로 표현하니 만료 판정이 범위 질의가 됐고, 청소 작업이 사라졌다.** 자료구조를 바꾸는 것이 로직을 추가하는 것보다 나은 전형적인 경우다.

대가도 있었다 — 자료구조를 바꾸면 **읽기·쓰기 API를 전부 같이 바꿔야 한다.** `SetMembersAsync` 호출이 한 곳 남아 있어서 런타임에 `WRONGTYPE`이 났다. Redis는 키 하나에 타입 하나만 허용하므로, 마이그레이션 누락은 컴파일이 아니라 운영 중에 드러난다.

## 5. Redis 트랜잭션 안에서 `await` 금지

```csharp
var tx = _database.CreateTransaction();
_ = tx.HashSetAsync(key, entries);        // ★ _ = 로 Task를 버린다
_ = tx.KeyExpireAsync(key, ttl);
await tx.ExecuteAsync();                  // 여기서 MULTI/EXEC로 한 번에 실행
```

StackExchange.Redis의 트랜잭션은 **명령을 모았다가 `ExecuteAsync`에서 한꺼번에 보낸다.** 내부에서 `await`하면 아직 보내지도 않은 명령의 결과를 기다리게 되어 멈춘다. 이건 라이브러리의 함정이 아니라 **MULTI/EXEC 모델 그 자체**다 — Redis는 EXEC 전까지 명령을 큐에 쌓기만 한다.

## 6. TTL은 용도마다 다르다

```
session:{id}         → JWT 만료와 동일    ← 토큰이 살아 있는 동안만 유효해야 한다
session:user:{id}    → 세션과 동일        ← 생명주기를 공유
user:{id} 등 순수 캐시 → 30분             ← 만료돼도 DB에서 다시 채우면 그만
```

**"세션 만료"와 "캐시 만료"는 이름만 만료지 의미가 다르다.** 전자는 **정책**(만료되면 권한이 없어짐)이고 후자는 **최적화**(만료돼도 동작은 같음)다. 이걸 하나의 상수로 묶으면 캐시 TTL을 조정하는 순간 보안 정책이 바뀐다.

## 7. 경계에서 값이 변형된다 — `null → "" → null`

Redis Hash는 null을 저장할 수 없다. 그래서 저장할 때 `?? string.Empty`로 바꿨는데, **읽을 때 되돌리지 않았다.**

```
도메인:  RefreshToken = null   ("로그아웃 상태")
   ↓ 저장
Redis:   RefreshToken = ""
   ↓ 읽기
도메인:  RefreshToken = ""     ← null 이 아니다!
```

인증 코드는 `RefreshToken is null`로 로그아웃 여부를 판단한다. `""`는 null이 아니므로 **로그아웃한 사용자가 로그인 상태로 판정**된다. 직렬화 경계는 **양방향 변환이 짝을 이뤄야** 한다 — 한쪽만 있으면 값이 조용히 다른 의미가 된다.

## 8. 통합 테스트 — 캐시 전략은 Fake로 검증할 수 없다

Repository 위 레이어는 Fake로 충분하지만, **캐시 전략 자체는 진짜 Redis와 진짜 DB가 있어야 검증된다.** Testcontainers로 두 컨테이너를 띄웠다.

```
E2E           Docker 서버 대상 (PlayMode)
Integration   RepositoryIntegrationTests  ← 실제 PostgreSQL + Redis
Application   AuthServiceTests 등 (Fake 리포지토리)
Domain        엔티티 단위 (순수)
```

**캐시 HIT을 어떻게 증명하나** — "빠른가"로는 증명이 안 된다. 그래서 **DB에서 행을 지우고 조회**했다.

```csharp
var user = await repository.CreateAsync();
context.Users.Remove(user);              // DB에서 제거 (캐시는 그대로)
await context.SaveChangesAsync();

var found = await repository.GetByIdAsync(user.UserId);
Assert.NotNull(found);   // DB에 없는데 반환됐다 = 캐시에서 왔다는 증명
```

**검증에도 3절의 함정이 있다** — Update 이후 검증은 반드시 **새 DbContext**로 해야 한다. 같은 context로 조회하면 EF가 추적 중인 엔티티를 그대로 돌려주기 때문에 **DB에 실제로 저장됐는지 검증하지 못한다.** 프로덕션 버그와 테스트 위양성이 완전히 같은 원인에서 나왔다.

## 9. 남은 것

- **Cache Stampede 미방어** — 인기 키가 동시에 만료되면 모든 요청이 DB로 직행한다. `SET NX` 뮤텍스로 한 명만 재적재하게 하는 것이 정석이고, 재료(`RedisUserLock`)는 이미 있지만 캐시 재적재에는 걸려 있지 않다. 현재 쓰기·트래픽 규모에서 실측된 문제는 아니다(**미실측**).
- **write-heavy 엔티티에는 이 전략이 안 맞는다** — 채팅처럼 쓰기가 잦으면 지우고-다시-올리기가 반복된다. 실제로 채팅 이력은 Sorted Set 인덱스 + TTL이라는 다른 형태로 갈라졌다([04](./chapter-04-chat.md)).
- 통합 테스트 스키마는 `EnsureCreatedAsync`(빠름), 프로덕션은 `MigrateAsync`. 테스트가 마이그레이션 파일 자체를 검증하지는 않는다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| Get/Update/Delete 3원칙 | 전 도메인 Repository의 공통 규칙 |
| `AsNoTracking` = 정확성 장치 | 스트리밍 스코프 전반의 stale 방지 · 진실원 교리([03](./chapter-03-dungeon-lobby.md)) |
| 실제 컨테이너로 인프라 검증 | Docker 대상 E2E를 기본으로 삼는 테스트 전략([09](./chapter-09-unity-client.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-07-db-cache.md](../learning-log/chapter-07-db-cache.md)

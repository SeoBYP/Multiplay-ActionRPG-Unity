# 프로젝트 현황 & 다음 작업 목록

> 마지막 업데이트: 2026-04-07 (1-2, 2-2 완료)
> 기준: 실제 코드 파일 직접 확인

---

## 전체 아키텍처 한 눈에 보기

```
Unity Client
    │ gRPC (인증/로비/채팅)
    ▼
GameServer (.NET 8 / ASP.NET Core)
├── API        : gRPC 서비스 (Auth, Chat, DungeonLobby, User)
├── Application: 서비스 레이어 (비즈니스 로직)
├── Domain     : 엔티티 (User, DungeonRoom, GameSession, Chat)
└── Infrastructure
    ├── DB     : PostgreSQL (EF Core, source of truth)
    ├── Cache  : Redis (Cache Aside + Delete, Sorted Set, Hash)
    ├── MQ     : Redis Streams (GameStart 이벤트)
    └── Log    : Serilog → Graylog (TraceId 전파)
    │ Redis Streams (GameStartRequestedMessage)
    ▼
SocketServer (.NET / TCP)
├── Session    : TCP 세션 관리
├── Room       : 인게임 방 관리
└── PacketHandler: Auth, PingPong

인프라 (Docker Compose)
├── PostgreSQL 16
├── Redis 7
├── Graylog 5.2 + OpenSearch + MongoDB
└── pgAdmin
```

---

## 챕터별 구현 완료 현황

| 챕터 | 주제 | 완성도 | 비고 |
|------|------|--------|------|
| 1 | Clean Architecture + 이중 서버 구조 | ✅ 완료 | Application → Infrastructure 의존성 정리 완료 |
| 2 | JWT 인증 + DeviceId Binding + Token Rotation | ✅ 완료 | 단일 기기 세션 정책 구현 완료 |
| 3 | gRPC Streaming + Redis Pub-Sub (던전 로비) | ✅ 완료 | Race Condition 처리 포함 |
| 4 | 채팅 (Redis Streams + BroadcastChannel) | ✅ 완료 | ReadLoop 설계 포함 |
| 5 | 게임 시작 E2E (GameServer → MQ → SocketServer → TCP) | ✅ 완료 | 폴링 방식으로 SocketServer 준비 대기 |
| 6 | 분산 로그 (Serilog + Graylog + TraceId 전파) | ✅ 완료 | TraceId가 Redis MQ 경계 통과 |
| 7 | Repository 캐시 레이어 (Cache Aside + Delete) | ✅ 완료 | Sorted Set 세션 관리, 통합 테스트 포함 |

---

## 레이어별 구현 상태

### GameServer.Domain

| 엔티티 | 상태 | 비고 |
|--------|------|------|
| User | ✅ | |
| UserCredential | ✅ | RefreshToken, DeviceId |
| UserProfile | ✅ | NickName, Level |
| UserSession | ✅ | SessionId, ExpiresAt |
| DungeonRoom | ✅ | Status(Waiting/Starting/Playing/Closed), SocketInfo |
| DungeonRoomPlayer | ✅ | |
| GameSession | ✅ | Status(Active/Ended) |
| GameSessionPlayer | ✅ | |
| ChatMessage | ✅ | ChatType(Global/Room/Direct), SenderName |

### GameServer.Application (서비스)

| 서비스 | 상태 | 미완성 항목 |
|--------|------|------------|
| AuthService | ✅ | RefreshToken reuse detection 미완 |
| AccountService | ✅ | |
| DungeonLobbyService | ⚠️ 동작함 | StartGameAsync — MQ 직접 발행 (Outbox 미적용), 인코딩 깨진 에러 메시지 |
| ChatService | ✅ | |
| ChatSubscriptionService | ⚠️ 동작함 | SwitchRoomAsync TODO 주석 잔존, 방 전환 재구독 정책 불명확 |
| DungeonLobbySubscriptionService | ⚠️ 동작함 | Room repository 직접 조회 (책임 분리 필요) |
| GameSessionService | ✅ | |
| UserProfileService | ✅ | |

### GameServer.Infrastructure (Repository)

| Repository | DB | Redis | 통합테스트 | 비고 |
|---|---|---|---|---|
| UserRepository | ✅ | ✅ | ✅ | PublicId 역방향 매핑 |
| UserCredentialRepository | ✅ | ✅ | ✅ | Email 역방향 매핑 |
| UserProfileRepository | ✅ | ✅ | ✅ | |
| UserSessionRepository | ✅ | ✅ | ✅ | Sorted Set 세션 관리 |
| DungeonRoomRepository | ✅ | ✅ | ✅ | Active Set 포함 |
| DungeonRoomPlayerRepository | ✅ | ✅ | ✅ | RemoveByRoomId 포함 |
| GameSessionRepository | ✅ | ✅ | ✅ | RoomId 역방향 매핑 |
| GameSessionPlayerRepository | ✅ | ✅ | ✅ | |
| ChatMessageRepository | ✅ | ✅ | ✅ | Sorted Set 다중 인덱스 |

### SocketServer

| 기능 | 상태 | 비고 |
|------|------|------|
| TCP 리스너 | ✅ | |
| 세션 관리 | ✅ | |
| 방 관리 | ✅ | lock 기반 thread-safe |
| 패킷 핸들러 (Auth, PingPong) | ✅ | |
| Redis MQ 소비 (GameStartRequested) | ✅ | NOGROUP 복구 포함 |
| SocketReady 발행 (GameSessionReady) | ✅ | |
| ILogger 적용 | ✅ | |
| 인게임 게임 로직 (실제 전투 등) | ❌ 미구현 | |

### 테스트

| 테스트 종류 | 상태 | 비고 |
|---|---|---|
| Domain 단위 테스트 | ✅ | User, DungeonRoom, ChatMessage |
| Application 서비스 테스트 (Fake) | ✅ | Auth, Account, DungeonLobby, Chat, UserProfile 등 |
| Infrastructure 통합 테스트 (Testcontainers) | ✅ | 9개 Repository 전부 |
| E2E 테스트 (실제 gRPC 서버) | ✅ | GameStartE2ETest |
| PostgresConnectionTests | ✅ | `[Trait("Category","Manual")]` — 실제 DB 필요 |

---

## 다음 작업 목록 (우선순위 순)

### 🔴 Priority 1 — 안정성 (지금 동작하지만 버그 가능성)

#### 1-1. `StartGameAsync` Outbox 패턴 적용

**파일:** `GameServer.Application/Domains/DungeonLobby/DungeonLobbyService.cs`

**현재 문제:**
```csharp
// room 상태 변경
var updated = await dungeonRoomRepository.UpdateAsync(room, ct);

// ← 여기서 서버가 죽으면 room은 Starting이지만 MQ 발행 안 됨
await gameStartRequestedMessageQueue.EnqueueAsync(...);
```
DB 트랜잭션 없이 room 상태 변경 + MQ 발행이 분리되어 있음.
SocketServer가 메시지를 못 받으면 room이 `Starting` 상태로 영구 고착됨.

**설계 방향 (합의된 내용):**

```
StartRoom 요청 흐름:
1. 세션/방장 검증
2. room 상태 → Starting
3. Outbox에 GameStartRequested INSERT  ← DB 트랜잭션으로 2+3 묶기
4. 즉시 응답 반환 (IP/Port 없음)

Outbox Publisher (Background):
5. Outbox 테이블 polling
6. Redis Stream으로 GameStartRequested 발행
7. PublishedAt, Status, RetryCount 갱신

GameSessionService (MQ 소비):
8. SocketServer 준비 대기
9. GameSession + GameSessionPlayer 생성
10. GameSessionReady 이벤트 발행

DungeonLobbyService (GameSessionReady 소비):
11. room 상태 → Playing
12. SubscribeRoom 스트림으로 브로드캐스트 (IP/Port 포함)
```

**클라이언트 응답 분리 원칙:**
- `StartRoom` 응답 → "요청 접수됨", room 상태 `Starting`
- `IP/Port` 수신 → `SubscribeRoom` 스트림의 `GameSessionReadyEvent`

**Outbox 엔티티 필드:**
- `RoomId`, `MessageType`, `Payload` (JSON)
- `Status` (Pending / Published / Failed)
- `RetryCount`, `CreatedAt`, `PublishedAt`

---

#### ~~1-2. 에러 메시지 인코딩 수정~~ ✅ 완료

**파일:** `DungeonLobbyService.cs`

**현재 문제:**
```csharp
return Result.Failure(ErrorCodes.InternalServerError, "諛??낅뜲?댄듃 ?ㅽ뙣");
return Result.Failure(ErrorCodes.InternalServerError, "諛???젣 ?ㅽ뙣");
```
파일 인코딩이 깨져서 한국어가 모두 깨짐. `ErrorMessages.` 상수로 교체 필요.

---

#### 1-3. RefreshToken Reuse Detection 완성

**파일:** `GameServer.Application/Domains/Auth/AuthService.cs`

**현재 상태:** Binding 실패 시 세션 만료만 처리. 탈취 감지(Reuse Detection) 없음.

**목표:**
- 이미 사용된 RefreshToken으로 재시도 → 전체 세션 강제 만료 + 로그 기록
- `Generation` 카운터 또는 `IsRevoked` 필드 기반 구현

---

### 🟡 Priority 2 — 설계 개선

#### 2-1. `DungeonLobbySubscriptionService` 책임 분리

**파일:** `GameServer.Application/Domains/DungeonLobby/DungeonLobbySubscriptionService.cs`

**현재 문제:** 구독 서비스가 Room repository 직접 조회 + 도메인 멤버십 검증 처리.

**목표:** 구독 서비스는 "연결 유지 + 이벤트 전달"만 담당. 멤버십 검증은 별도 `AccessPolicy`로 분리.

---

#### ~~2-2. `ChatSubscriptionService.SwitchRoomAsync` 정리~~ ✅ 완료

**파일:** `GameServer.Application/Domains/Chat/ChatSubscriptionService.cs`

**현재 상태:** `// TODO : 구독` 주석 잔존. 방 전환 시 재구독 정책 불명확.

**남은 Chat 작업:**
- Streaming cancellation / disconnect 처리 정리
- reconnect 시 누락 메시지 복구 정책 문서화
- 방 전환 시 구독 책임 명확화 (TODO 주석 → 실제 구현 또는 정책 주석)
- Chat E2E 테스트 보강

---

#### 2-3. `GetRooms` count/페이징 정책 결정

**파일:** `GameServer.API/Services/DungeonLobbyGrpcService.cs`

**현재 상태:** `// TODO : ROOM Count 방안 고민` 주석 잔존.

**목표:** 총 개수 필드 필요 여부 결정 + 페이징 도입 여부 결정 + proto 수정.

---

#### 2-4. `RedisUserLock` 설정화

**파일:** `GameServer.Infrastructure/Common/RedisUserLock.cs`

**현재 상태:** `LockExpiry`, `RetryInterval`, `DeadLine`, 키 prefix 이중 콜론(`lock:user::...`) 하드코딩.

**목표:** `appsettings.json`으로 이동, 키 prefix 정리.

---

#### 2-5. `ISocketEndpointParser` 추상화

**현재 상태:** `socketInfo.Split(':')` 직접 파싱. IPv6 취약, DNS 기반 endpoint 취약.

**목표:**
```csharp
public interface ISocketEndpointParser
{
    (string Host, int Port) Parse(string rawValue);
}
```
`Ip` 대신 `Host` 네이밍으로 통일.

---

#### 2-6. 통합 테스트 코드 정리

- `DungeonRoomRepositoryIntegrationTests.Update`: 수동 DB 저장 + `UpdateAsync()` 이중 저장 제거
- `UserRepositoryIntegrationTests.Update`: Reflection → 도메인 메서드 사용 or 시나리오 재검토

---

### 🟢 Priority 3 — 운영 안정성

#### 3-1. Redis Stream 운영 정책

**파일:** `SocketServer/Room/GameStartRequestedMessageQueue.cs`

**현재 상태:**
- Consumer name `socket-1` 고정
- PEL(Pending Entry List) 재처리 없음

**목표:**
- Consumer name을 hostname 또는 설정 기반 동적 생성
- `XAUTOCLAIM` 기반 미처리 메시지 재처리
- Stream lag / backlog / consumer health 로깅 추가

---

#### 3-2. TCP 세션까지 TraceId 전파

**현재 상태:** TraceId가 SocketServer Room 로그까지만 존재. C_Auth, PingPong 이후 로그에는 없음.

**목표:** C_Auth 패킷에 TraceId 포함 → SocketServer 세션에 저장 → 이후 패킷 처리 로그에 자동 첨부.

---

#### 3-3. SocketServer appsettings 정리

SocketServer IP:Port 하드코딩 잔존 여부 확인 및 `appsettings.json` 기반으로 통일.

---

### ❌ Priority 4 — 미구현 기능

#### 4-1. Unity 클라이언트 ↔ SocketServer 인게임 로직

현재 SocketServer는 Auth + PingPong 패킷만 처리.

**결정 필요:**
- 게임 타입 (PVP? Co-op? 어떤 장르?)
- 동기화 방식 (서버 권위형 vs 클라이언트 예측)
- 필요 패킷 정의 (이동, 공격, 피격, 아이템 등)

---

#### 4-2. Unity 클라이언트 완성

TCP 소켓 연결, MemoryPack 직렬화 구조는 있음. 실제 게임 UI/UX 및 인게임 플로우 미완.

---

## 작업 권장 순서

```
1. 에러 메시지 인코딩 수정 (1-2)                ← 30분, 즉시 가능
2. ChatSubscriptionService TODO 정리 (2-2)      ← 빠른 정리
3. RefreshToken Reuse Detection 완성 (1-3)      ← 인증 보안 완결
4. Outbox 패턴 설계 + 구현 (1-1)               ← 핵심 안정성 작업
5. DungeonLobbySubscriptionService 분리 (2-1)
6. ISocketEndpointParser 추상화 (2-5)
7. RedisUserLock 설정화 + GetRooms 정책 (2-3, 2-4)
8. 통합 테스트 정리 (2-6)
9. Redis Stream 운영 정책 (3-1)
10. TCP TraceId 전파 (3-2)
11. 인게임 로직 설계 (4-1)                      ← 가장 큰 작업
```

---

## 포트폴리오 완성도 체크리스트

| 항목 | 완료 |
|------|------|
| 아키텍처 설계 이유 문서화 (Clean Architecture, 이중 서버) | ✅ |
| 인증 시스템 (JWT, Refresh, DeviceId) | ✅ |
| 실시간 로비 (gRPC Streaming, Redis Pub-Sub) | ✅ |
| 채팅 (Redis Streams) | ✅ |
| 게임 시작 E2E (MQ, 폴링, SocketServer) | ✅ |
| 분산 로깅 (TraceId 전파, Graylog) | ✅ |
| DB + Redis 캐시 레이어 | ✅ |
| 통합 테스트 (Testcontainers) | ✅ |
| Outbox 패턴 (메시지 신뢰성) | ❌ |
| 인게임 실제 게임플레이 | ❌ |

---

## 개발 규칙

### 브랜치 전략

- `main`: 배포/안정 브랜치
- `develop`: 통합 개발 브랜치
- `feature/*`: 기능 개발
- `hotfix/*`: 긴급 버그 수정

### Commit Convention

| prefix | 용도 |
|--------|------|
| `feat:` | 기능 추가 |
| `fix:` | 버그 수정 |
| `refactor:` | 리팩토링 |
| `test:` | 테스트 |
| `docs:` | 문서 |
| `chore:` | 기타 |

### PR Rule

1. 이슈 링크 필수
2. 테스트 결과 첨부 필수
3. 리뷰 승인 후 머지

### Priority Labels

- `priority:P0` 즉시 처리
- `priority:P1` 이번 스프린트
- `priority:P2` 차주/후순위

---

## 아키텍처 원칙 (합의된 내용)

### 저장소 전략

- **PostgreSQL**: source of truth — room, user, game session, outbox 영속 데이터
- **Redis**: 캐시 / 분산락 / 실시간 메시지 스트림 / pub-sub

### 캐시 패턴 (Cache Aside + Delete)

- **읽기**: Redis 우선 → miss 시 PostgreSQL → Redis 채움
- **쓰기**: PostgreSQL commit → Redis 캐시 삭제

### 메시지 전달 원칙

- **서비스 간 이벤트**: `IMessageQueue<T>` (Redis Streams)
- **클라이언트 실시간 알림**: `IBroadcastChannel<T>` (Pub-Sub)
- **Outbox**: room 상태 변경과 메시지 INSERT는 같은 DB 트랜잭션

### 서비스 책임 경계

| 서비스 | 책임 |
|--------|------|
| DungeonLobbyService | 방 CRUD, 게임 시작 요청 접수 (Outbox 기록까지만) |
| GameSessionService | GameSession/Player 생성, Socket endpoint 파싱 |
| DungeonLobbySubscriptionService | 연결 유지 + 이벤트 전달 (검증 없음) |
| GameSessionReadyConsumer | MQ 소비 → room 상태 갱신 → 브로드캐스트 |

### 네이밍 원칙

- 서비스 인터페이스는 이벤트 핸들러 이름 사용 금지
- `HandleSocketReadyAsync` ❌ → `CreateGameSessionAsync` ✅
- Socket endpoint 변수명: `Ip` ❌ → `Host` ✅

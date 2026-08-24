# 서버 아키텍처 규칙

## Clean Architecture 방향

```
API Layer → Application Layer ← Infrastructure Layer
                    ↓
               Domain Layer
```

- 인터페이스: `Application/Domains/{Name}/I{Name}Service.cs`, `I{Name}Repository.cs`
- 구현체: `Infrastructure/Domains/{Name}/{Name}Repository.cs`
- Application이 Infrastructure를 직접 참조하면 위반.
- Infrastructure 구현체가 Application 인터페이스를 구현하는 방향만 허용.

## 새 도메인 추가

```
GameServer.Application/Domains/{Name}/
  I{Name}Service.cs
  {Name}Service.cs
  I{Name}Repository.cs   ← 인터페이스는 여기

GameServer.Infrastructure/Domains/{Name}/
  {Name}Repository.cs    ← DB + Redis 구현체는 여기
```

## 기존 도메인 경계

| 도메인 | 책임 범위 |
|--------|-----------|
| Auth | JWT 발급/갱신, DeviceId Binding, Token Rotation, Reuse Detection |
| Account | 회원가입, 이메일/비밀번호 검증 |
| DungeonLobby | 방 CRUD, 게임 시작 요청 → **Outbox 기록까지만** |
| GameSession | SocketServer IP:Port 관리, 게임 세션/플레이어 생성 |
| Chat | Global/Room/Whisper, Redis Streams + BroadcastChannel |
| User | 프로필, 세션, 닉네임 관리 |
| Reward | 보상 지급 원장(`reward_grants`) — 지급과 "지급했음" 기록을 한 트랜잭션으로 묶는 exactly-once |

`DungeonLobbyService.StartGameAsync`는 Outbox 기록까지만.  
세션 생성은 `GameSessionService` 책임. 두 책임을 DungeonLobbyService에 합치지 않는다.

서버 간 직접 RPC 호출 금지. GameServer ↔ SocketServer는 Redis Streams로 통신.

## DungeonRoom 엔티티

필드 추가 시 반드시 4곳 동시 수정:
```
Clone() / FromRedis() / ParseFromRedis() / ToHashEntry()
```

현재 상태 필드:
```csharp
RoomStatus Status  // Waiting / Starting / Playing / Closed
string SocketIp, int SocketPort  // SetSocketInfo()로만 설정
```

## 네이밍

- `Ip` → `Host` (IP 주소 필드명)
- 이벤트 핸들러 이름: `HandleXxxAsync` 금지 → 의도를 표현하는 동사 사용
- 서버 포트: GameServer HTTP `5131` / gRPC `5132`, SocketServer TCP `7777`

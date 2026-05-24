# GameServer 아키텍처

## 레이어 구조 (Clean Architecture)

```
API → Application ← Infrastructure
          ↓
        Domain
```

- **인터페이스**는 Application에 정의
- **구현체**는 Infrastructure에 위치
- Application이 Infrastructure를 직접 참조하면 위반

## 새 도메인 추가 시 파일 구조

```
GameServer.Application/Domains/{Name}/
├── I{Name}Service.cs
├── {Name}Service.cs
└── I{Name}Repository.cs    ← 인터페이스는 여기

GameServer.Infrastructure/Domains/{Name}/
└── {Name}Repository.cs     ← DB + Redis 구현체는 여기
```

## 기존 도메인 목록

| 도메인 | 역할 |
|--------|------|
| Auth | JWT 발급/갱신, DeviceId Binding, Token Rotation, Reuse Detection |
| Account | 회원가입, 이메일/비밀번호 검증 |
| DungeonLobby | 방 CRUD, 게임 시작 요청(Outbox 기록까지만), gRPC 구독 스트림 |
| GameSession | SocketServer IP:Port 관리, 게임 세션/플레이어 생성 |
| Chat | Global/Room/Whisper 채팅, Redis Streams + BroadcastChannel |
| User | 프로필, 세션, 닉네임 관리 |

## DungeonRoom 엔티티 주의사항

새 필드 추가 시 **반드시 4곳 모두** 수정:
```
Clone() / FromRedis() / ParseFromRedis() / ToHashEntry()
```

현재 주요 필드:
```csharp
RoomStatus Status  // Waiting / Starting / Playing / Closed
string SocketIp, int SocketPort  // SetSocketInfo()로 설정
```

## 네이밍 원칙

- `Ip` ❌ → `Host` ✅
- `HandleXxxAsync` (이벤트 핸들러 이름) ❌ → 의도 표현 동사 ✅
- 서비스 책임: DungeonLobbyService는 Outbox 기록까지만 / GameSessionService가 세션 생성

# Unity 클라이언트 구조

## gRPC 채널 설정

Unity 기본 `Grpc.Net.Client`는 HTTP/2(h2c) 미지원 → `YetAnotherHttpHandler` 필수.

```
GrpcChannelProvider
  ├── YetAnotherHttpHandler (HTTP/2 h2c 강제)
  ├── Authorization 헤더 자동 주입 (AccessToken)
  └── 채널 공유 — 서비스마다 별도 채널 생성 금지
```

**파일**: `Client/Assets/Script/Network/Https/Core/GrpcChannelProvider.cs`

## 서비스 목록

| 서비스 | 경로 |
|--------|------|
| AuthGrpcService | Https/Services/AuthGrpcService.cs |
| UserGrpcService | Https/Services/UserGrpcService.cs |
| DungeonLobbyGrpcService | Https/Services/DungeonLobbyGrpcService.cs |
| ChatGrpcService | Https/Services/ChatGrpcService.cs |

## VContainer DI

서비스 생성 시점 명시적 관리. MonoBehaviour 싱글톤 대신 사용.  
네트워크 레이어가 씬 오브젝트 생명주기에 묶이지 않음 → 테스트 가능.

## Socket 클라이언트

```
SocketConnector    → TCP 연결 관리
SocketSession      → 패킷 송수신 (MemoryPack 직렬화)
```

`C_Auth` → `C_PlayerJoin` 순서 필수. Auth 전에 Join 요청 시 거부됨.

## PlayMode E2E 테스트 (Docker 서버 대상)

| 파일 | 검증 범위 |
|------|-----------|
| AuthE2ETests | 회원가입/로그인/Refresh/Logout 전체 흐름 |
| UserE2ETests | 닉네임 설정/중복/금지어 |
| DungeonLobbyE2ETests | 방 생성/입장/시작, SubscribeRoom 스트림 |
| ChatE2ETests | Global/Room/Whisper 수신 |
| SocketE2ETests | gRPC 로비 → TCP 인증/입장 → 이동 브로드캐스트 |

**베이스**: `Tests/PlayMode/E2E/E2ETestBase.cs` (채널/서비스 생성, RegisterAndLogin 공통 헬퍼)

## 스트림 취소 처리 주의

gRPC 스트림 취소 시 `OperationCanceledException`과 `RpcException(StatusCode.Cancelled)` 둘 다 처리 필요.  
한쪽만 처리하면 정상 종료가 에러로 찍힘.

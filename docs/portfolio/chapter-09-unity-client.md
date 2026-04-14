# Chapter 09 — Unity Client (gRPC + VContainer + Docker E2E)

## 현재 상태

챕터 8까지에서 서버 쪽 `GameServer(gRPC)` / `SocketServer(TCP)` 경계는 정리되었다.  
이 챕터에서는 Unity 클라이언트를 실제 서버와 붙이는 작업에 집중했다.

핵심 목표는 세 가지였다.

1. Unity가 `GameServer`와 gRPC로 직접 통신할 수 있어야 한다.
2. 클라이언트 네트워크 계층을 VContainer 기반으로 정리해야 한다.
3. Docker로 띄운 실제 서버를 대상으로 PlayMode E2E 테스트를 지속적으로 돌릴 수 있어야 한다.

---

## 왜 gRPC 기준으로 갔는가

이 프로젝트의 서버 경계는 이미 명확했다.

- `GameServer`: 인증, 유저, 던전 로비, 채팅
- `SocketServer`: 인게임 실시간 이동/동기화

즉 Unity 클라이언트는 로비 단계에서 `HTTP + JSON`이 아니라 서버와 동일한 계약을 공유하는 통신 계층이 필요했다.

여기서 gRPC를 선택한 이유는 다음과 같다.

- `proto` 기반 계약으로 클라이언트/서버 타입 불일치를 줄일 수 있다.
- 인증, 유저, 로비, 채팅 API를 하나의 방식으로 통일할 수 있다.
- 로비 구독과 채팅 스트리밍을 같은 스택에서 처리할 수 있다.
- 서버가 이미 gRPC를 중심으로 설계되어 있어서 중복 API 계층을 만들 필요가 없다.

결론적으로:

- **로비 이전과 로비 중 상태 관리**는 gRPC
- **인게임 이동 동기화**는 TCP Socket

으로 경계를 유지하는 것이 가장 단순했다.

---

## Unity에서 gRPC를 붙일 때 부딪힌 문제

Unity에서 `Grpc.Net.Client`를 바로 쓰면 끝날 것 같았지만 실제로는 그렇지 않았다.

가장 먼저 부딪힌 문제는 이것이었다.

```text
Bad gRPC response. Response protocol downgraded to HTTP/1.1.
```

원인은 Unity 런타임의 HTTP/2/h2c 지원 제약이었다.

- 서버는 `5132` 포트를 `HTTP/2` 전용으로 열고 있었다.
- Unity 쪽 기본 핸들러는 `http://localhost:5132`에 대해 `h2c`를 안정적으로 처리하지 못했다.
- 그 결과 gRPC 요청이 HTTP/1.1로 다운그레이드되면서 서버와 프로토콜이 맞지 않았다.

해결은 `YetAnotherHttpHandler` 도입이었다.

### 적용 포인트

- Unity Package에 `YetAnotherHttpHandler` 추가
- `GrpcChannelProvider`에서 전용 핸들러 사용
- `http://` 환경에서는 `Http2Only = true`로 강제

즉 클라이언트 쪽에서 “가능하면 HTTP/2”가 아니라 “반드시 HTTP/2(h2c)”로 붙게 만든 것이 핵심이었다.

---

## GrpcChannelProvider 정리

클라이언트는 서비스마다 직접 채널을 만들지 않고 `GrpcChannelProvider`를 통해 공유하도록 정리했다.

역할은 네 가지다.

1. 서버 주소 관리
2. gRPC `CallInvoker` 생성
3. 인증 토큰 자동 주입
4. Unity/HTTP2 환경 제약 캡슐화

이 구조로 바꾼 이유는 테스트성과 유지보수성 때문이다.

- 서비스별 생성 로직을 중복하지 않는다.
- AccessToken이 바뀌어도 채널 제공자에서 일괄 반영할 수 있다.
- 테스트 코드에서 같은 방식으로 Auth/User/Lobby/Chat 서비스를 만들 수 있다.

실제로 이후 PlayMode E2E 테스트는 전부 이 채널 제공자를 공통 진입점으로 사용했다.

---

## VContainer 도입

클라이언트 네트워크 계층을 MonoBehaviour 싱글톤으로 이어 붙이면 빠르게는 갈 수 있지만, 테스트와 확장성에서 금방 막힌다.

그래서 의존성 구성은 VContainer 기준으로 정리했다.

도입 목적은 명확했다.

- 서비스 생성 시점을 명시적으로 관리
- 테스트에서 네트워크 레이어를 독립적으로 교체 가능하게 유지
- UI/Presenter/Service 간 생성 책임 분리

### VContainer 도입으로 얻은 효과

- `AuthGrpcService`, `UserGrpcService`, `DungeonLobbyGrpcService`, `ChatGrpcService`를 조합 가능한 서비스로 유지
- 네트워크 계층이 특정 씬 오브젝트 생명주기에 묶이지 않음
- 향후 Mock/Fake/실서버 교체가 쉬워짐

이 프로젝트에서 VContainer는 “DI 프레임워크를 썼다”가 핵심이 아니라,

**Unity 클라이언트 코드를 테스트 가능한 단위로 쪼개는 기반**이 되었다는 점이 중요했다.

---

## Docker 기반 개발 루프

이번 챕터에서 중요한 전환점은 “Unity가 로컬 임시 서버가 아니라 Docker로 띄운 실제 서버와 계속 통신한다”는 개발 루프를 만든 것이다.

구성 방향은 이랬다.

- `docker compose`로 `postgres`, `redis`, `graylog`, `gameserver`, `socketserver` 실행
- Unity는 `localhost:5132`로 `GameServer` gRPC 연결
- Socket은 `localhost:7777`로 연결
- 서버 로그는 `docker compose logs -f gameserver`, `graylog`로 확인

### 서버를 Docker로 묶으며 정리한 점

- `ListenLocalhost` → `ListenAnyIP`
- Graylog 주소 하드코딩 제거, 환경변수화
- `GameServer.API.csproj`에서 Docker 빌드 시 ClientCodegen 스킵
- `GameServer`, `SocketServer` 전용 Dockerfile 작성
- `docker-compose.yml`에 서비스 추가
- `.dockerignore`로 빌드 컨텍스트 정리

이렇게 해두니 개발 루프가 바뀌었다.

```text
코드 수정
→ 서버 빌드 / Docker 재기동
→ Unity PlayMode 실행
→ Docker 로그 / Graylog 확인
→ 문제 원인 파악 후 수정
```

이 방식의 장점은 “내 로컬 환경에 우연히 맞는 실행”이 아니라,
**실제 배포와 더 비슷한 조건에서 클라이언트-서버 상호작용을 검증**할 수 있다는 점이다.

---

## PlayMode E2E 테스트를 왜 썼는가

기존 서버 단위 테스트만으로는 다음 문제를 잡기 어려웠다.

- Unity gRPC 채널 설정 문제
- 인증 헤더 주입 누락
- 스트리밍 취소 처리 차이
- 실제 멀티 유저 흐름
- Docker 서버와 Unity 런타임 조합에서만 드러나는 문제

그래서 `Client/Assets/Script/Tests/PlayMode/E2E/` 아래에 실제 서버를 대상으로 하는 PlayMode E2E 테스트를 추가했다.

공용 베이스는 `E2ETestBase`로 정리했다.

### 공용 베이스 역할

- `GrpcChannelProvider` 생성
- `Auth/User/Lobby/Chat` 서비스 생성
- `RegisterAndLoginAsync(...)`
- `LoginAsync(...)`
- `RegisterLoginAndSetNicknameAsync(...)`
- 테스트용 토큰 상태 관리

이렇게 해두니 각 테스트 파일은 “시나리오”에만 집중할 수 있었다.

---

## Auth E2E

`AuthE2ETests`에는 아래 흐름을 넣었다.

- 회원가입 성공
- 중복 이메일 실패
- 빈 이메일 실패
- 로그인 성공
- 잘못된 비밀번호 실패
- 존재하지 않는 계정 실패
- 빈 `DeviceId` 실패
- Refresh 성공
- 잘못된 `DeviceId`로 Refresh 실패
- Logout 성공
- Logout 이후 Refresh 실패
- Register → Login → Refresh → Logout 전체 흐름

### 여기서 실제로 잡힌 문제

- Unity gRPC 연결이 HTTP/1.1로 다운그레이드되던 문제
- 인증 헤더 자동 주입이 빠져 `Unauthenticated`가 나던 문제
- 서버 DI 누락으로 AuthService가 기동 시 깨지던 문제

즉 Auth E2E는 단순한 기능 검증이 아니라,
**Unity ↔ gRPC ↔ Docker 서버가 실제로 연결되는지 확인하는 가장 기초적인 건강검진** 역할을 했다.

---

## User E2E

`UserE2ETests`에는 아래 시나리오를 넣었다.

- 닉네임 정상 설정
- 중복 닉네임 실패
- 너무 짧은 닉네임 실패
- 허용되지 않은 문자 실패
- 욕설 포함 닉네임 실패

### 여기서 실제로 잡힌 문제

- `ProfanityFilter`가 더미 구현으로 항상 `true`를 반환하던 문제
- 회원가입 후 `UserProfile`이 생성되지 않아 닉네임 변경이 `INTERNAL_SERVER_ERROR`로 떨어지던 문제
- 서버가 닉네임 중복을 아예 검사하지 않던 문제

즉 User E2E를 통해 “유저 기능이 있다” 수준이 아니라,
**실제 계정 생성 이후 상태 전이가 올바르게 이어지는지** 검증하게 됐다.

---

## Dungeon Lobby E2E

`DungeonLobbyE2ETests`는 로비 상태 전이를 확인하는 데 집중했다.

### 포함한 시나리오

- 방 생성 성공
- 생성한 방 조회 성공
- 존재하지 않는 방 조회 실패
- 방 목록 조회
- 다른 유저 입장 성공
- 정원 초과 실패
- 입장 후 퇴장 성공
- 방장 설정 변경 성공
- 비방장 설정 변경 실패
- SubscribeRoom 이벤트 수신
- 비방장 StartRoom 실패
- 방 생성 → 입장 → 방장 재로그인 → 시작 전체 흐름

### 여기서 실제로 잡힌 문제

- 테스트가 방장이 아닌 다른 계정으로 `StartRoom`을 호출하던 문제
- 동일 유저 재로그인 시 `user_sessions` 유니크 충돌이 나던 문제
- 재로그인 과정에서 `UserCredential` EF tracking 충돌이 나던 문제
- 스트림 취소를 `OperationCanceledException`만 처리해서 `RpcException(Cancelled)`를 놓치던 문제

로비 쪽은 상태 전이가 많아서 “한 API만 성공하면 된다”가 아니었다.

특히 이 챕터에서 중요한 학습은:

**멀티 유저 흐름은 단일 요청 단위 테스트로는 충분히 커버되지 않는다**는 점이었다.

---

## Chat E2E

이번 챕터에서 새로 추가한 것이 `ChatE2ETests`였다.

### 포함한 시나리오

- 글로벌 채팅 수신
- 방 채팅 수신
- 귓속말 대상자 수신

채팅은 unary RPC보다 어려웠다. 이유는 스트림이기 때문이다.

### 여기서 실제로 조심한 점

- 스트림 시작 시 과거 메시지가 먼저 들어올 수 있음
- “첫 번째 수신 메시지” 기준 검증은 쉽게 깨짐
- 따라서 “기대한 메시지 내용이 수신될 때까지 대기”하는 방식으로 테스트 작성

이 부분은 단순하지만 중요했다.

스트리밍 테스트는 자주 이렇게 망가진다.

- 테스트는 새 메시지를 기대했는데
- 실제로는 이전 글로벌 메시지나 backlog가 먼저 옴
- 그래서 테스트가 서버 버그처럼 보이지만 사실은 테스트가 취약한 경우

이번 보강에서는 이 함정을 피하도록 테스트 구조를 바꿨다.

---

## 이번 챕터에서 정리된 클라이언트 구조

```text
Unity Client
 ├─ GrpcChannelProvider
 │   ├─ YetAnotherHttpHandler
 │   ├─ HTTP/2(h2c) 강제
 │   └─ Authorization 헤더 주입
 │
 ├─ AuthGrpcService
 ├─ UserGrpcService
 ├─ DungeonLobbyGrpcService
 └─ ChatGrpcService

VContainer
 └─ 서비스 생성 / 수명주기 관리

PlayMode E2E
 ├─ AuthE2ETests
 ├─ UserE2ETests
 ├─ DungeonLobbyE2ETests
 └─ ChatE2ETests

Docker
 ├─ GameServer
 ├─ SocketServer
 ├─ Postgres
 ├─ Redis
 └─ Graylog
```

---

## 이번 챕터에서 배운 점

### 1. Unity에서 gRPC는 “그냥 붙이면 되는 기술”이 아니다

서버가 gRPC라고 해서 Unity도 기본 `Grpc.Net.Client`만 넣으면 끝날 줄 알았는데, 실제로는 런타임 제약과 HTTP/2 지원 문제를 먼저 해결해야 했다.

즉 Unity + gRPC는 라이브러리 선택까지 포함한 설계 문제였다.

### 2. DI는 클라이언트 테스트성을 크게 바꾼다

VContainer를 도입한 뒤 네트워크 서비스 생성이 정리되면서, 테스트 코드 구조도 훨씬 단순해졌다.

클라이언트에서도 DI는 “고급 패턴”이 아니라,
**실제 서버 붙는 테스트를 만들기 위한 기반 기술**이었다.

### 3. Docker 로그를 보는 습관이 E2E 품질을 바꾼다

Unity에서 보이는 `RpcException`만 보면 원인을 잘못 짚기 쉽다.

실제 원인은 서버 로그에 있었다.

- DI 등록 누락
- EF tracking 충돌
- 세션 유니크 충돌
- 패키지 버전 꼬임

즉 E2E는 클라이언트 코드만 보는 작업이 아니라,
**클라이언트 예외 + 서버 로그를 한 세트로 해석하는 작업**이었다.

### 4. 테스트 코드도 프로덕션 코드처럼 관리해야 한다

특히 스트리밍 테스트는 취약하게 작성하면 쉽게 오탐이 난다.

그래서 이번에는 테스트를 “한 번 통과하는 코드”가 아니라,
실제 개발 루프에서 계속 돌릴 수 있는 코드로 보강했다.

---

## 다음 단계

- [ ] UI 계층과 gRPC 서비스 연결
- [ ] 로그인/로비/채팅 화면 Presenter 정리
- [ ] SocketServer 연결과 인게임 진입 흐름 연결
- [ ] PlayMode E2E를 CI에서 자동 실행할 수 있는 형태로 정리
- [ ] 테스트용 Docker seed / reset 전략 정리

---

## 참고 경로

| 용도 | 경로 |
|------|------|
| gRPC 채널 제공자 | `Client/Assets/Script/Network/Https/Core/GrpcChannelProvider.cs` |
| Auth 서비스 | `Client/Assets/Script/Network/Https/Services/AuthGrpcService.cs` |
| User 서비스 | `Client/Assets/Script/Network/Https/Services/UserGrpcService.cs` |
| Lobby 서비스 | `Client/Assets/Script/Network/Https/Services/DungeonLobbyGrpcService.cs` |
| Chat 서비스 | `Client/Assets/Script/Network/Https/Services/ChatGrpcService.cs` |
| PlayMode E2E 베이스 | `Client/Assets/Script/Tests/PlayMode/E2E/E2ETestBase.cs` |
| Auth E2E | `Client/Assets/Script/Tests/PlayMode/E2E/AuthE2ETests.cs` |
| User E2E | `Client/Assets/Script/Tests/PlayMode/E2E/UserE2ETests.cs` |
| Lobby E2E | `Client/Assets/Script/Tests/PlayMode/E2E/DungeonLobbyE2ETests.cs` |
| Chat E2E | `Client/Assets/Script/Tests/PlayMode/E2E/ChatE2ETests.cs` |
| Docker Compose | `ServerAll/Infra/docker-compose.yml` |

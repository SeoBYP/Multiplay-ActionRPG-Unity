# 09. Unity 클라이언트 — 목(mock)을 쓰지 않는 E2E

> **한 줄** — Unity에서 gRPC는 "라이브러리 추가하면 끝"이 아니었다(런타임이 HTTP/2를 다운그레이드한다). 그리고 이 챕터의 진짜 산출물은 기능이 아니라 **Docker로 띄운 실제 서버를 대상으로 도는 PlayMode E2E**다 — 목으로는 잡히지 않는 종류의 버그가 실제로 잡혔다.
>
> **범위** Unity+gRPC 제약 · 채널 공유 · VContainer · Docker 개발 루프 · E2E 전략
> **현재 규모** E2E 테스트 클래스 5개 → **13개**로 확장 (`Tests/PlayMode/E2E/Network/{Https,Socket}/`)

---

## 1. Unity + gRPC는 라이브러리 선택 문제였다

붙이자마자 이 에러가 났다.

```
Bad gRPC response. Response protocol downgraded to HTTP/1.1.
```

서버는 `5132`를 **HTTP/2 전용**으로 열어 두고 있는데, Unity 런타임의 기본 HTTP 핸들러는 TLS 없는 평문 HTTP/2(**h2c**)를 안정적으로 처리하지 못한다. 그래서 요청이 HTTP/1.1로 내려가고 서버와 프로토콜이 어긋난다.

```csharp
// GrpcChannelProvider.cs:23 — "가능하면 HTTP/2"가 아니라 "반드시 HTTP/2"
var handler = new YetAnotherHttpHandler { Http2Only = true };
```

`YetAnotherHttpHandler`(Cysharp)는 Unity에서 h2c를 제대로 처리하는 핸들러다. **표준 라이브러리가 표준대로 동작하지 않는 런타임이 있다**는 것, 그리고 그 대응이 설계 결정(의존성 추가)이 된다는 것이 이 절의 요점이다.

## 2. 채널은 하나, 인증은 인터셉터로

서비스마다 채널을 만들면 연결이 서비스 수만큼 생기고, 토큰이 갱신될 때 손댈 곳이 여러 군데가 된다. `GrpcChannelProvider` 하나로 모았다.

```
GrpcChannelProvider
 ├ 서버 주소 관리
 ├ CallInvoker 생성 (채널 공유)
 ├ AuthorizationInterceptor 로 토큰 자동 주입   ← 서비스 코드는 인증을 모른다
 └ Unity/HTTP2 제약 캡슐화                      ← 핸들러 선택이 여기서만 보인다
```

**인증을 인터셉터로 뺀 것이 핵심**이다. 각 서비스가 헤더를 붙이는 구조였다면 새 서비스를 만들 때마다 빠뜨릴 수 있고, 실제로 초기에 `Unauthenticated`가 났던 원인이 그것이었다. 인터셉터는 **모든 호출이 반드시 통과하는 지점**이라 빠뜨릴 수가 없다.

그리고 테스트를 위한 문이 하나 열려 있다.

```csharp
// GrpcChannelProvider.cs:31 — 실제 채널 없이 CallInvoker를 주입할 수 있다
protected GrpcChannelProvider(CallInvoker overrideInvoker) { Address = "fake://test"; ... }
```

E2E는 실제 서버를 쓰지만, **네트워크가 필요 없는 단위 테스트**는 이 생성자로 가짜 invoker를 넣는다. 같은 계층에 두 가지 검증 방식을 공존시킨 장치다.

## 3. VContainer — DI는 고급 패턴이 아니라 테스트의 전제

MonoBehaviour 싱글톤으로 네트워크 계층을 이어 붙이면 빨리 갈 수는 있다. 대신 **네트워크가 씬 오브젝트의 생명주기에 묶인다** — 씬을 바꾸면 연결이 끊기고, 테스트에서 교체할 수도 없다.

VContainer로 옮기면서 얻은 것은 세 가지다.

- 서비스 **생성 시점을 명시적으로** 관리 (Unity 생명주기와 분리)
- 테스트에서 네트워크 계층만 독립적으로 교체
- UI / Presenter / Service의 생성 책임 분리

> 이 프로젝트에서 DI의 값어치는 "DI를 썼다"가 아니라 **테스트 가능한 단위로 쪼갤 수 있게 됐다**는 데 있다. 실제로 이후 E2E 테스트가 서비스들을 조립하는 방식이 프로덕션 조립과 같아졌다.

## 4. 개발 루프를 Docker로 옮긴 이유

```
코드 수정 → 서버 빌드 · Docker 재기동 → Unity PlayMode 실행 → Docker/Graylog 로그 확인 → 수정
```

로컬에서 서버를 직접 실행하면 **내 PC에만 우연히 맞는 상태**가 만들어진다. Docker로 묶으면서 실제로 드러난 것들:

- `ListenLocalhost` → `ListenAnyIP` (컨테이너 밖에서 접근 불가였음)
- Graylog 주소 하드코딩 → 환경변수
- Docker 빌드 시 ClientCodegen 스킵 (컨테이너 안에 Unity가 없다)
- `.dockerignore`로 빌드 컨텍스트 정리

전부 **"로컬에서는 안 보이던 결합"** 이다. 배포 환경과 비슷한 조건에서 돌려야 이런 게 개발 중에 드러난다.

## 5. 왜 목(mock)을 쓰지 않았나

서버 단위 테스트는 이미 있었다. 그런데도 다음 부류는 **아무도 잡지 못했다.**

| E2E가 실제로 잡은 문제 | 목이었다면? |
|---|---|
| Unity gRPC가 HTTP/1.1로 다운그레이드 | 목은 프로토콜을 타지 않는다 → **영원히 못 잡음** |
| 인증 헤더 주입 누락 → `Unauthenticated` | 목은 헤더를 검사하지 않는다 |
| 서버 DI 등록 누락으로 기동 시 서비스가 깨짐 | 목은 실제 컨테이너를 구성하지 않는다 |
| `ProfanityFilter`가 더미라 **항상 통과**시키던 문제 | 목이 곧 더미다 → **더미로 더미를 검증** |
| 회원가입 후 `UserProfile`이 생성되지 않던 문제 | 목은 상태 전이를 흉내만 낸다 |
| 서버가 닉네임 중복을 아예 검사하지 않던 문제 | 목이 중복을 막아줬을 것이다 |
| 재로그인 시 `user_sessions` 유니크 충돌 · EF 추적 충돌 | 실제 DB가 없으면 발생 자체가 불가능 |
| 스트림 취소를 `OperationCanceledException`만 처리해 `RpcException(Cancelled)`를 놓침 | 목은 gRPC 예외 체계를 재현하지 않는다 |

특히 세 번째 줄이 상징적이다 — **더미 구현이 항상 성공을 반환하고 있었고, 목 기반 테스트는 그걸 검증할 수 없다.** "테스트가 통과한다"와 "기능이 동작한다"가 갈리는 지점이 정확히 여기다. (이 주제는 [27](./chapter-27-silent-failure.md)에서 다시 크게 터진다.)

그래서 규칙을 세웠다 — **E2E는 Docker 서버를 대상으로 하고, 목으로 서버를 대체하지 않는다.**

## 6. 스트리밍 테스트는 기본적으로 취약하다

채팅 E2E를 만들면서 배운 것.

```
❌ "스트림에서 받은 첫 메시지" 를 검증
   → 실제로는 이전에 쌓인 backlog 나 다른 테스트의 글로벌 메시지가 먼저 온다
   → 서버 버그처럼 보이지만 테스트가 취약한 것

✅ "기대한 내용의 메시지가 올 때까지 대기" 로 검증
```

스트림은 **언제 시작하든 과거가 딸려 올 수 있다.** 순서와 시점을 가정하는 검증은 반드시 깨진다. 이건 채팅이 Streams라 이력을 보존하기 때문에 생기는 성질이고([04](./chapter-04-chat.md)), 기능의 장점이 테스트에서는 함정이 된 경우다.

## 7. 정상 종료를 에러로 기록하지 않기

Socket E2E를 붙이자 테스트가 끝날 때마다 양쪽 로그에 에러가 쌓였다.

```
클라: OperationCanceledException
서버: ConnectionReset (10054)
```

둘 다 **의도한 종료**였다. 테스트가 소켓을 닫았을 뿐이다. 클라(`SocketConnector`·`SocketSession`)와 서버(`Session`) 양쪽에서 "의도된 disconnect"를 정상 경로로 처리하도록 예외 정책을 정리했다.

> 사소해 보이지만 중요하다 — **정상 종료가 에러로 찍히면 진짜 에러가 노이즈에 묻힌다.** 같은 판단을 채팅 방 전환에서도 했다([04](./chapter-04-chat.md) 5절). 로그의 가치는 양이 아니라 신호 대 잡음비다.

## 8. Socket E2E는 "연결 확인"이 아니라 상태 전이 검증이다

```
host/guest 계정 생성 → host 방 생성 → guest 입장 → host StartRoom
   → 두 클라 소켓 접속·입장 → host C_Move → guest 가 S_Move 수신 ✓
```

TCP가 붙는지만 보면 절반도 검증하지 못한다. **로비가 방을 준비했는가 / 소켓 입장에 필요한 유저·방 정보가 일치하는가 / 입장 후 상대에게 실제로 브로드캐스트가 가는가** — 이 전이를 다 통과해야 인게임 네트워크가 살아 있다고 말할 수 있다.

여기서 도메인 규칙도 하나 배웠다. 처음엔 `MaxPlayers = 1` 방으로 테스트를 짰는데 **서버 규칙상 방 생성·시작 최소 인원이 2명**이었다. 테스트가 통과하지 못한 게 아니라 **테스트가 도메인을 몰랐다.**

## 9. 그 이후 — 이 챕터의 다음 단계는 어떻게 됐나

| 당시 다음 단계 | 결말 |
|---|---|
| UI 계층과 gRPC 연결 · Presenter 정리 | ✅ MVI 아키텍처로 완성 ([MVI 아키텍처](../wiki/unity-mvi-architecture.md)) |
| `SocketSession`을 인게임 진입 흐름과 연결 | ✅ 세션 기반 입장으로 재설계([11](./chapter-11-socket-session-entry.md)) |
| Docker seed / reset 전략 | ✅ `AdminController`(`api/admin`: ClearAll·ClearRooms·ClearSessions)([01](./chapter-01-architecture.md) 4절) |
| **PlayMode E2E를 CI에서 자동 실행** | ❌ **미달성** — `.github/workflows` 없음 |

CI 대신 다른 방향으로 갔다 — **Unity CLI로 컴파일·EditMode·PlayMode를 직접 구동**하고, 세션 종료 시 도는 훅(`check-network-e2e-coverage.ps1`·`check-stale-server-image.ps1` 등)이 "연결 소스를 고쳤는데 소켓 테스트가 안 바뀌었다", "서버 이미지가 소스보다 낡았다" 같은 것을 경고하게 했다.

CI를 대체하지는 못한다(자동 실행이 아니라 경고다). 다만 **혼자 개발하는 환경에서는 "잊어버림"이 가장 큰 실패 원인**이라, 실행 자동화보다 누락 감지가 먼저 필요했다. 실제로 이 훅들이 나중에 여러 번 작동했다.

## 10. 현재 구조

```
Unity Client
 ├ GrpcChannelProvider ── YetAnotherHttpHandler (h2c 강제)
 │                     └ AuthorizationInterceptor (토큰 주입)
 ├ Auth / User / DungeonLobby / Chat / Inventory / Equipment / Codex ... GrpcService
 └ Socket (Connector + Session)

VContainer — 생성/수명 관리, 씬 생명주기와 분리

PlayMode E2E (Docker 서버 대상)  ※ 5개 → 13개
 ├ Network/Https/  Auth · AuthFlow · User · DungeonLobby · Chat · Inventory
 │                 Equipment · Progression · Quest · MainLoot · CharacterPersistence
 └ Network/Socket/ SocketE2ETests · GameSessionConnectorE2ETests
```

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| E2E는 목을 쓰지 않는다 | 이후 모든 도메인이 Docker 대상 E2E를 함께 추가 (현재 13종) |
| 채널·인증을 한 지점으로 | 서비스가 7개 이상으로 늘어도 인증 배선 변경 0 |
| DI로 씬 생명주기와 분리 | GameHud처럼 씬을 넘나드는 컴포넌트 설계의 전제 |
| 정상 종료 ≠ 에러 | 연결 생존성·재접속 처리 전반([21](./chapter-21-connection-liveness-hp-authority.md)·[29](./chapter-29-multiplayer-sync-invisible-failures.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-09-unity-client.md](../learning-log/chapter-09-unity-client.md)

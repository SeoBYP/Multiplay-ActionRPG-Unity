# Unity 클라이언트 규칙

## MVI 레이어 의존성 규칙 (최우선)

### View는 자신의 Model만 안다

View(MonoBehaviour)가 주입받을 수 있는 타입은 **해당 View 전용 Model 하나**뿐이다.

```
❌ LoginWindow   → IAuthService       (Game.System 직접 참조)
❌ DungeonRoomItemView → RoomStatusType  (Game.Network 직접 참조)
✅ LoginWindow   → TitleModel         (Game.OutGame.Title)
✅ DungeonRoomItemView → DungeonRoomModel (Game.OutGame.DungeonLobby)
```

### 레이어 의존 방향

```
Game.GUI  →  Game.OutGame  →  Game.System  →  Game.Network
```

- `Game.GUI`가 `Game.System`을 직접 참조하면 위반.
- `Game.GUI`가 `Game.Network`를 직접 참조하면 위반.
- `Game.GUI.asmdef`에 `Game.System` 또는 `Game.Network` GUID/이름을 추가하지 않는다.

### proto 타입은 View에 노출하지 않는다

`GameServer.Grpc.*` 타입(RoomInfo, UserInfo, RoomStatusType 등)은
`Game.OutGame` 레이어에서 도메인 타입으로 변환한 후 노출한다.

```
DungeonRoomModel.Status  → RoomStatus     (도메인 enum, Game.OutGame)
DungeonRoomModel.Players → RoomPlayerInfo (도메인 클래스, Game.OutGame)
```

### System 타입이 필요한 경우

View가 auth/startup 등 System 레벨 관심사를 알아야 한다면,
해당 관심사를 담당하는 Model을 OutGame 레이어에 만든다.

```
인증 상태 → TitleModel      (Game.OutGame.Title)
스타트업 인텐트 → LobbyModel.StartAsync (Game.OutGame.DungeonLobby)
```

### ViewController(POCO)도 동일한 규칙 적용

`IInitializable`, `IAsyncStartable` 등 POCO 컨트롤러도 `Game.GUI` 어셈블리에 속하면
`Game.System`을 직접 참조해서는 안 된다.
auth 대기, 큐 소진 등 System 관심사는 해당 Model의 `StartAsync`로 이동한다.

---

## gRPC 채널

Unity 기본 `Grpc.Net.Client`는 HTTP/2(h2c) 미지원 → `YetAnotherHttpHandler` 필수.
서비스마다 채널 별도 생성 금지. `GrpcChannelProvider`에서 채널을 공유.

```
GrpcChannelProvider
  ├── YetAnotherHttpHandler (HTTP/2 h2c 강제)
  ├── Authorization 헤더 자동 주입 (AccessToken)
  └── 채널 공유 — 서비스마다 별도 채널 생성 금지
```

파일: `Client/Assets/Script/Network/Https/Core/GrpcChannelProvider.cs`

## VContainer DI

- MonoBehaviour Singleton 도입 금지. VContainer scope 사용.
- 네트워크 레이어를 씬 오브젝트 생명주기에 묶지 않는다.
- 새 서비스 등록 시 lifetime, scope, 인스턴스 공유 여부를 명시적으로 결정한다.
- 생산자·소비자가 동일 객체를 참조해야 하는 경우 (예: `CharacterInputBuffer`) 반드시 같은 scope에서 단일 인스턴스로 등록.

## Unity lifecycle vs DI lifecycle

`OnEnable()`이 `IInitializable.Initialize()`보다 먼저 실행될 수 있다.  
주입 객체를 `OnEnable()`에서 즉시 사용하는 코드 작성 금지. 초기화 완료 여부를 확인 후 사용.

DI 디버깅 시 "null 여부"만 보지 않는다. 반드시 확인:
- 생명주기(Transient vs Singleton vs Scoped)
- 동일 인스턴스 공유 여부
- scope 경계

## Socket 클라이언트

```
SocketConnector  → TCP 연결 관리
SocketSession    → 패킷 송수신 (MemoryPack 직렬화)
```

연결 후 순서: `C_Auth` 완료 → `C_PlayerJoin`. Auth 전 Join 요청 금지.

## gRPC 스트림 취소 처리

`OperationCanceledException`과 `RpcException(StatusCode.Cancelled)` 둘 다 처리.
한쪽만 처리하면 정상 종료가 에러로 기록됨.

---

## 불필요한 추상화 금지 (인터페이스 도입 기준)

인터페이스는 아래 **하나 이상**이 충족될 때만 만든다. 그렇지 않으면 구체 클래스를 직접 사용한다.

| 조건 | 예시 |
|------|------|
| 구현체가 2개 이상 실제로 존재하거나, 곧 생긴다 | `IBroadcastChannel` / `IMessageQueue` |
| 테스트에서 실제로 Mock/Stub으로 교체한다 | `ISocketSession` (E2E 제외 단위테스트) |
| asmdef 경계를 넘어 구체 타입을 감춰야 한다 | `IAuthService` (Game.System → Game.Network 격리) |

**위 조건 미충족 시 인터페이스 도입 금지:**

```
❌ ILoadingOverlay   → 구현체 Loading 하나뿐, 교체 계획 없음, 테스트 미사용
❌ IFaderCanvas      → 씬 어댑터. 구현체 1개, Mock 불필요
❌ IGameHud          → MonoBehaviour 래퍼. 구현체 1개

✅ ICharacterInputWriter / ICharacterInputSource  → 생산자·소비자 역할 분리 (동일 구현체지만 계약 분리 목적)
✅ IAuthService      → asmdef 경계 격리
```

### 인터페이스 없이 DI 등록하는 법

VContainer에 인터페이스가 없어도 구체 클래스를 그대로 등록한다:

```csharp
// 인터페이스 없이 등록 — 이것으로 충분하다
builder.RegisterComponentInHierarchy<Loading>().AsSelf();
builder.Register<FaderCanvas>(Lifetime.Scoped);
```

### 기존 불필요 인터페이스 발견 시

코드를 읽다가 위 조건을 충족하지 않는 인터페이스를 발견하면:
1. 먼저 사용자에게 보고 (CLAUDE.md 원칙 6번)
2. 승인 후 인터페이스 제거 → 구체 타입으로 교체 (원칙 5번)

---

## 인터페이스/타입 위치 결정 (DIP — 작성 *전* 필수)

인터페이스는 **소비자(호출하는 쪽) 레이어에 둔다.** 구현체 위치가 아니라 소비자 위치가 기준이다.

```
ILoadingOverlay 소비자 = GameSceneManager (Presentation)  → ILoadingOverlay는 Presentation에 둔다
구현체 FaderCanvas (GUI) → GUI가 Presentation을 참조 → 허용 ✓
```

❌ "씬 매니저가 System에 있으니 인터페이스도 System에" — 만든 사람 편의로 위치 결정 → GUI→System 역참조 유발.

새 인터페이스/타입을 만들기 **전에** 순서대로 답한다:

1. **소비자부터 찾는다** — 이걸 호출하는 쪽이 누구고 어느 asmdef인가? → 인터페이스는 그 레이어에 둔다.
2. **asmdef 방향을 먼저 검증** — 이 타입이 들어갈 asmdef가 필요한 참조를 *허용 방향*으로 가질 수 있나? 안 되면 위치가 틀린 것.

asmdef 의존 방향은 **코드 작성 후 검사가 아니라 작성 전 제약**이다. 일단 짜고 위반인지 나중에 보지 않는다.

## 위반 진단 절차 (확대 진단 금지)

레이어/참조 위반을 발견하면:

1. **최소 단위로 국소화** — "파일 1개 이동 / 참조 1줄 제거로 끝나나?"를 **먼저** 답한다.
2. **단정 금지** — "어디 둬도 위반" 같은 단정은 검증 전에 하지 않는다. 대부분 인터페이스를 올바른 레이어로 옮기면 해결된다.
3. **내가 원인인지부터 확인** — `git diff`로 "원래 그랬나 / 내 직전 변경이 만들었나" 구분. 내가 만든 위반이면 재설계가 아니라 **되돌리기**다.
4. **관련 없는 컴포넌트를 끌어들이지 않는다** — 위반 지점과 무관한 클래스(예: 네트워크 커넥터)를 리팩토링 범위에 넣지 않는다.

→ 1파일짜리 문제를 8단계 리팩토링으로 부풀리는 것은 CLAUDE.md 원칙 1번(간결성·과추상화 금지) 위반이다.

# VContainer Lifetime 관리와 인증 초기화 순서

> **레퍼런스 문서** — Global → Scene 스코프 순서 보장과 인증 준비 시점을 정의한다.
> 최종 코드 대조: 2026-08-22 (`System/AuthSystem/` → **`System/Auth/`**, `VContainer/Installers/` → **`VContainer/LifetimeScopes/`**)

## 왜 이 문제가 생겼는가

에디터에서 OutGame 씬을 직접 실행하면(Title 씬을 거치지 않으면) 로그인이 되지 않은 상태로 씬이 뜬다.

기존 구조에서 `LobbyModel.Initialize()`는 씬 시작 즉시 `Accept(LoadRooms)`를 호출해
방 목록을 gRPC로 요청했다. 당연히 Authorization 헤더가 없어 서버가 거부한다.

첫 번째 시도: `EditorAutoLoginInitializer`를 만들어 씬 스코프에 등록하고 `Initialize()`에서 `AutoLoginAsync().Forget()`으로 게스트 로그인을 실행했다.

```
OutGameLifetimeScope.Initialize 순서:
  1. LobbyModel.Initialize()        → LoadRooms 즉시 호출 (토큰 없음) ← 실패
  2. EditorAutoLoginInitializer.Initialize() → AutoLoginAsync().Forget() (비동기, 아직 완료 안 됨)
```

두 번째 시도: `LobbyModel.Initialize()`에서 `LoadRooms`를 제거하고
`LobbyViewController.OpenLobbyAsync()`에서만 호출하게 했다.
L 키를 누를 때는 이미 로그인이 완료돼 있을 것이라는 가정이었다.

문제는 이 가정이 틀릴 수 있다는 것이다.

- 에디터에서 Play 누르자마자 바로 L 키를 누르면 여전히 로그인 전이다.
- MainScene처럼 로그인 완료가 필요하지만 L 키와 무관한 시스템이 있으면 보호할 방법이 없다.

**근본 원인:** `Initialize()`에서 `Forget()`으로 시작한 비동기 작업은 완료 시점을 누구도 알 수 없다.

---

## 설계 결정 — Global → Scene 순서 보장

### 요구사항 정리

1. 에디터에서 로그인 실패 시 로그를 출력하고 Play 모드를 즉시 종료한다.
2. 씬별 시스템은 인증이 완료된 이후에만 동작이 시작된다.
3. 빌드에서는 Title 씬에서 로그인이 완료된 후 씬을 전환하므로 별도 처리가 필요 없다.

### 계층 구조

```
ProjectLifetimeScope (전역, 씬 전환에도 유지)
  └─ EditorAutoLoginInitializer (IAsyncStartable) ← 에디터 전용 (#if UNITY_EDITOR)
       ├─ 성공 → AuthSession.Update() → UniTaskCompletionSource 완료
       └─ 실패 → LogError + EditorApplication.isPlaying = false

OutGameLifetimeScope / MainLifetimeScope (씬별)
  └─ SceneStartup (IAsyncStartable)
       └─ await _authService.AuthenticatedAsync()  ← TCS 완료까지 블로킹
            └─ 이후 씬별 초기화 진행
```

---

## 구성 요소별 역할

### AuthSession — UniTaskCompletionSource 추가

```csharp
public class AuthSession
{
    private readonly UniTaskCompletionSource _authenticatedTcs = new();

    public UniTask AuthenticatedAsync() => _authenticatedTcs.Task;

    public void Update(string accessToken, string refreshToken, long expiresAt)
    {
        // 토큰 저장...
        _authenticatedTcs.TrySetResult(); // 최초 인증 시 1회, 이후 호출은 무시됨
    }
}
```

**설계 의도:**

처음에는 `ReactiveProperty<bool>` 또는 별도 `AuthReadySignal` 클래스를 만드는 방법을 검토했다.

`ReactiveProperty<bool>`: 구독이 필요한 것이 아니라 **"한 번 완료될 때까지 기다리는"** 것만 필요하다.
R3 의존성을 추가하면서까지 구독 기능이 들어갈 이유가 없다.

`AuthReadySignal` 별도 클래스: 인증 완료 시점은 `AuthSession`이 이미 알고 있는 정보다.
"언제 완료됐는가"를 별도 객체로 분리하면 책임이 두 곳으로 분산된다.
`AuthSession`은 원래부터 인증 상태를 소유하는 객체이므로 여기에 두는 것이 맞다.

`UniTaskCompletionSource`는 리셋하지 않는다. "최초 인증 완료"는 한 번 켜지면 끝이다.
로그아웃 후 재로그인 시에도 이미 완료된 TCS는 다음 `await`을 즉시 통과시킨다.
이 동작이 의도한 것이다 — SceneStartup은 "한 번이라도 인증된 적 있다"는 것만 확인하면 된다.

---

### IAuthService — 인터페이스 노출

```csharp
public interface IAuthService
{
    bool IsAuthenticated { get; }
    UniTask AuthenticatedAsync();  // 추가
    // ...
}
```

`AuthService`는 `_authSession.AuthenticatedAsync()`를 위임한다.
씬 시스템은 `AuthSession`을 직접 의존하지 않고 `IAuthService`를 통해 접근한다.

---

### EditorAutoLoginInitializer — IInitializable → IAsyncStartable

```csharp
#if UNITY_EDITOR
public class EditorAutoLoginInitializer : IAsyncStartable
{
    public async UniTask StartAsync(CancellationToken ct)
    {
        if (_authService.IsAuthenticated) return;

        // 기기별 해시로 개발자 계정 분리
        var hash  = Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode()).ToString("x8");
        var email = $"guest_{hash}@editor.test";

        try
        {
            var result = await _authService.LoginOrRegisterAsync(email, password, ct);
            if (result == AuthResult.Success)
            {
                Debug.Log("[EditorAutoLogin] 성공");
                return;
            }
            Debug.LogError($"[EditorAutoLogin] 실패({result}) — Play 모드를 종료합니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EditorAutoLogin] 예외 — 서버가 실행 중인지 확인하세요.\n{ex.Message}");
        }

        EditorApplication.isPlaying = false;
    }
}
#endif
```

**`IInitializable`을 쓰지 않는 이유:**

`IInitializable.Initialize()`는 동기 메서드다.
여기서 `Forget()`으로 비동기를 실행하면 완료 시점을 아무도 기다리지 않는다.

`IAsyncStartable.StartAsync()`는 VContainer가 `Start()` 타이밍에 호출하며 `UniTask`를 반환한다.
async 작업이 실제로 완료될 때까지 await이 유지된다.

**`ProjectLifetimeScope`에 등록하는 이유:**

씬 스코프에 등록하면 씬마다 로그인을 시도한다.
`AuthSession`은 전역 싱글톤이므로 이미 로그인된 상태에서 다시 로그인을 시도하는 것은 불필요하다.

전역 스코프에 1회 등록하면:
- 최초 씬 진입 시 한 번만 실행
- 이후 씬 전환 시 `IsAuthenticated` 체크에서 즉시 반환

**`deviceUniqueIdentifier` 해시로 개발자 계정 분리:**

여러 개발자가 같은 게임 서버를 공유할 때 동일한 게스트 이메일로 로그인하면 세션이 충돌한다.
기기 고유 식별자의 해시값으로 이메일을 만들면 개발자마다 고유한 계정이 생성된다.

---

### SceneStartup — 씬별 인증 대기

```csharp
public class OutGameSceneStartup : IAsyncStartable
{
    public async UniTask StartAsync(CancellationToken ct)
    {
        await _authService.AuthenticatedAsync().AttachExternalCancellation(ct);
        Debug.Log("[OutGameSceneStartup] 인증 확인 완료 — 씬 초기화 진행");
    }
}
```

**설계 의도:**

- 빌드 환경: Title 씬에서 로그인 완료 후 씬을 전환하므로 `AuthenticatedAsync()`는 즉시 반환된다.
- 에디터 환경: `EditorAutoLoginInitializer.StartAsync()`가 TCS를 완료하기 전까지 이 `await`이 블로킹한다.
- `AttachExternalCancellation(ct)` — 씬이 언로드되거나 Play 모드가 종료되면 대기가 취소된다.

씬이 전환될 때 SceneStartup이 할 일이 추가되면 `await` 이후에 코드를 쌓으면 된다.
인증 완료가 전제조건으로 보장된 상태에서 씬 초기화가 진행되는 구조다.

---

## 실행 흐름

### 에디터

```
[Play 버튼]
   │
ProjectLifetimeScope 초기화
   └─ EditorAutoLoginInitializer.StartAsync()
        ├─ LoginOrRegisterAsync() 호출 (gRPC)
        │    ├─ 계정 있음 → 로그인 성공
        │    └─ 계정 없음 → 회원가입 후 로그인 (LoginOrRegisterAsync 내부 처리)
        └─ AuthSession.Update() → TCS.TrySetResult() ← 완료 신호

씬 로드
   └─ OutGameSceneStartup.StartAsync()
        └─ await AuthenticatedAsync()
             └─ TCS 이미 완료됨 → 즉시 통과 (또는 완료 대기 후 통과)
                  └─ 씬 초기화 진행
```

### 에디터 — 서버 미실행 시

```
EditorAutoLoginInitializer.StartAsync()
   └─ LoginOrRegisterAsync() → RpcException 발생
        └─ catch → LogError("서버가 실행 중인지 확인하세요")
             └─ EditorApplication.isPlaying = false → Play 즉시 종료
```

### 빌드

```
Title 씬 → 사용자 로그인 → AuthSession.Update() → TCS.TrySetResult()
씬 전환 → SceneStartup.StartAsync()
   └─ await AuthenticatedAsync() → 즉시 통과
```

---

## 실제로 배운 점

### "IInitializable.Initialize()에서 Forget()하면 안 되는가?"

안 되는 건 아니지만, "완료를 기다리는 것이 필요한 경우"에는 쓰면 안 된다.

`Forget()`은 "시작하고 잊어라"다. 완료 시점에 아무것도 하지 않는다.
UniTask의 기본 UnhandledExceptionHandler가 예외를 잡긴 하지만, 성공/실패 여부를 다른 코드에서 알 방법이 없다.

비동기 초기화 작업이 완료된 후에 다른 시스템이 동작해야 한다면 반드시 `IAsyncStartable`을 써야 한다.

### "`IAsyncStartable`이 `IInitializable`보다 무조건 좋은가?"

아니다.

| 구분 | IInitializable | IAsyncStartable |
|------|---------------|-----------------|
| 타이밍 | `Awake` 직후 | `Start` 타이밍 |
| 반환 | void (동기) | UniTask (비동기) |
| 완료 대기 | 불가 | 가능 |
| 용도 | 빠른 동기 초기화 | 비동기 작업이 필요한 초기화 |

InputRouter, InteractionSystem, LobbyViewController처럼 콜백 등록이나 이벤트 구독처럼 즉각적인 동기 초기화는 `IInitializable`이 더 적합하다. 불필요하게 `IAsyncStartable`로 바꾸면 `Start` 타이밍까지 지연될 뿐이다.

### "ReactiveProperty 대신 UniTaskCompletionSource인 이유"

`ReactiveProperty<bool>`은 값이 바뀔 때마다 구독자에게 알린다. 구독자가 있을 때 의미가 있다.

`AuthenticatedAsync()`의 소비 패턴은 "완료될 때까지 한 번 기다린다"다. 구독이 아니라 대기다.

`UniTaskCompletionSource`는 정확히 이 용도다:
- 완료 전: `await tcs.Task`가 블로킹
- 완료 후: 즉시 통과 (완료된 Task는 다시 await해도 즉시 반환)
- 중복 완료: `TrySetResult()`는 두 번째 호출을 무시

R3 의존성 없음, 불필요한 구독 로직 없음, 의도가 코드에서 명확하게 드러난다.

---

## 참고 경로

| 역할 | 경로 |
|------|------|
| AuthSession | `Client/Assets/Script/System/Auth/AuthSession.cs` |
| IAuthService | `Client/Assets/Script/System/Auth/IAuthService.cs` |
| AuthService | `Client/Assets/Script/System/Auth/AuthService.cs` |
| EditorAutoLoginInitializer | `Client/Assets/Script/System/Auth/EditorAutoLoginInitializer.cs` |
| ProjectLifetimeScope | `Client/Assets/Script/VContainer/LifetimeScopes/ProjectLifetimeScope.cs` |
| OutGameSceneStartup | `Client/Assets/Script/VContainer/LifetimeScopes/Scenes/Startup/OutGameSceneStartup.cs` |
| MainSceneStartup | `Client/Assets/Script/VContainer/LifetimeScopes/Scenes/Startup/MainSceneStartup.cs` |

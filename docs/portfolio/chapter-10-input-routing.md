# 챕터 10 학습 로그 — Input 라우팅 시스템

## 왜 InputRouter를 만들었는가

Unity New Input System의 기본 사용 방식은 `PlayerInputActions.Player.Interact.performed += handler` 처럼 각 액션에 직접 콜백을 등록하는 것이다.

문제는 한 키를 여러 시스템이 동시에 반응해야 할 때다.

던전 로비 화면이 열려 있는 동안 `E`키를 누르면 어떻게 되는가?

- `InteractionSystem` — 월드 오브젝트와 상호작용
- `LobbyViewController` — UI 버튼에 포커스가 있을 때 확인

두 시스템이 같은 이벤트를 직접 구독하면 누가 먼저인지 알 수 없고,
한 쪽이 처리했을 때 다른 쪽을 막을 방법도 없다.

더 근본적인 문제: 테스트할 수 없다.
Unity Input System은 실제 하드웨어 없이 이벤트를 발생시키기가 어렵고,
MonoBehaviour에 콜백이 직접 붙으면 테스트 환경에서 분리할 수가 없다.

---

## 설계 결정 — Chain of Responsibility

```
PlayerInputActions.performed
  └─ InputRouter.Route(GameInputAction)
       ├─ Priority 100: LobbyViewController.TryHandle(action)
       │    └─ ToggleLobby면 true(consumed) → 체인 중단
       ├─ Priority 50:  InteractionSystem.TryHandle(action)
       │    └─ Interact고 범위 내 대상 있으면 true(consumed) → 체인 중단
       └─ Priority 10:  다른 핸들러...
```

핵심 규칙:
- `TryHandle`이 `true`를 반환하면 그 액션은 소비된다. 이후 핸들러는 호출되지 않는다.
- 우선순위가 높을수록 먼저 처리할 기회를 갖는다.
- UI 시스템(100) > 월드 인터랙션(50) 순서가 보장된다.

---

## 구성 요소별 역할

### GameInputAction — 열거형 추상화

```csharp
public enum GameInputAction
{
    Interact,
    Attack,
    Dodge,
    ToggleLobby,
    Pause,
}
```

**설계 의도:**

`InputAction` 오브젝트(New Input System 타입)를 그대로 전달하지 않고
자체 열거형으로 변환한다.

- `IInputHandler`가 `InputAction`에 의존하면 `Game.Network`, `Game.OutGame` 같은 하위 레이어가 `UnityEngine.InputSystem` 어셈블리에 의존하게 된다.
- 열거형 하나로 추상화하면 핸들러를 순수 C# 클래스로 구현할 수 있어 EditMode 테스트에서 Unity 엔진 없이 검증 가능하다.

---

### IInputHandler — 핸들러 인터페이스

```csharp
public interface IInputHandler
{
    int Priority { get; }
    bool TryHandle(GameInputAction action);
}
```

**설계 의도:**

`Priority`를 인터페이스에 포함시킨 이유는 핸들러 자신이 우선순위를 선언하게 하기 위해서다.
`InputRouter`에 우선순위를 외부에서 등록할 때 지정하는 방식이면
등록 코드와 핸들러 구현이 분리되어 우선순위를 추적하기 어려워진다.

---

### InputRouter — 라우팅 코어

```csharp
private void Route(GameInputAction action)
{
    if (_dirty)
    {
        _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        _dirty = false;
    }

    foreach (var handler in _handlers)
    {
        if (handler.TryHandle(action)) return; // consumed
    }
}
```

**설계 의도:**

- `_dirty` 플래그 — 매 `Route` 호출마다 정렬하지 않는다. 핸들러 목록이 바뀔 때만 다음 라우팅에서 1회 정렬한다. 게임 중 핸들러 등록/해제는 드물게 발생하므로 오버헤드가 없다.
- `IInitializable` 구현 — VContainer가 씬 시작 시 `Initialize()`를 호출한다. 여기서 `PlayerInputActions` 콜백을 등록하고 `Player` 맵을 Enable한다. `Dispose()`에서 해제.
- `PlayerInputActions`를 생성자 주입받는다 — 테스트에서 `InputTestFixture`의 가상 디바이스가 연결된 인스턴스를 주입할 수 있다.

---

### LobbyViewController — IInputHandler 구현 예

```csharp
public sealed class LobbyViewController : IInputHandler, IInitializable, IDisposable
{
    public int Priority => 100;  // UI는 월드보다 우선

    public bool TryHandle(GameInputAction action)
    {
        if (action != GameInputAction.ToggleLobby) return false;

        if (_isLobbyOpen) CloseLobby();
        else              OpenLobbyAsync().Forget();

        return true; // consumed
    }
}
```

`MonoBehaviour`가 아니다. POCO(Plain Old C# Object).
VContainer가 DI로 생성하고 `IInitializable`/`IDisposable`로 수명을 관리한다.
`Initialize()`에서 `_router.Register(this)`, `Dispose()`에서 `_router.Unregister(this)`.

---

### Hold 인터랙션 제거

`.inputactions` 파일의 Interact 액션에 `"interactions": "Hold"` 가 설정되어 있었다.

```json
// 변경 전
{ "interactions": "Hold" }

// 변경 후
{ "interactions": "" }
```

**문제:** `Hold` 인터랙션은 기본 0.4초 누름을 유지해야 `performed`가 발생한다.
New Input System의 `InputTestFixture.Press(key)`는 즉시 `performed`를 발생시키므로
테스트에서 `E`키 → Interact 라우팅이 항상 실패했다.

**판단:** 게임플레이에서 E키 상호작용에 긴 누름이 필요한 경우는 없다.
Hold가 필요한 특수 상호작용은 `IInteractable` 구현체 내부에서 별도로 처리하는 것이 맞다.
인풋 레이어에서 강제하면 모든 상호작용이 0.4초 지연을 갖게 된다.

---

## 테스트 전략

### EditMode 단위 테스트 — `InputRouterTests`

`InputTestFixture`를 상속해 가상 키보드를 생성하고 실제 Input System 이벤트를 시뮬레이션한다.

```csharp
// 우선순위 소비 테스트
[Test]
public void 높은_우선순위_핸들러가_소비하면_낮은_우선순위는_호출되지_않는다()
{
    var high = new TrackingHandler(priority: 100, consumes: true);
    var low  = new TrackingHandler(priority: 10,  consumes: false);
    _router.Register(high);
    _router.Register(low);

    Press(_keyboard.lKey);

    Assert.IsTrue(high.WasCalled(GameInputAction.ToggleLobby));
    Assert.IsFalse(low.WasCalled(GameInputAction.ToggleLobby));
}
```

`TrackingHandler`는 테스트 전용 페이크 — `TryHandle` 호출 횟수와 액션을 기록한다.
실제 로비를 열거나 네트워크 요청을 하지 않는다.

검증 항목:
- L키 → ToggleLobby, E키 → Interact, ESC → Pause 매핑
- 우선순위 정렬 (낮은 순서로 등록해도 높은 우선순위가 먼저 호출됨)
- consumed 시 하위 핸들러 차단
- 중복 등록 방지 (동일 핸들러를 두 번 Register해도 한 번만 호출)
- Unregister 후 호출 안 됨

### PlayMode 통합 테스트 — `InputSystemIntegrationTests`

실제 VContainer 씬 컨텍스트와 함께 E2E 흐름을 검증한다.

```csharp
[Test]
public IEnumerator InteractionSystem_유효한_대상_있으면_E키를_소비한다()
{
    // InputRouter + InteractionSystem이 함께 동작
    // 실제 Press → performed → Route → TryHandle → Interact 호출
}
```

---

## 실제로 배운 점

### "왜 GameInputAction 열거형을 따로 만드나?"

처음엔 `InputAction`을 그대로 전달하려 했다.

`IInputHandler.TryHandle(InputAction action)` 이렇게 하면 `InteractionSystem`, `LobbyViewController` 가 `UnityEngine.InputSystem` 어셈블리를 직접 참조해야 한다.

이 두 클래스는 `Game.OutGame`, `Game.GUI` 어셈블리에 속하는데, `UnityEngine.InputSystem`을 의존하면 해당 어셈블리 전체가 Input System 버전에 묶인다. 나중에 Input System을 바꾸거나 버전을 올리면 연쇄 영향이 생긴다.

열거형 하나로 추상화하면 핸들러는 Input System을 전혀 모른다. 변환은 `InputRouter` 한 곳에서만 발생한다.

### "POCO 핸들러는 MonoBehaviour보다 뭐가 나은가?"

`LobbyViewController`를 `MonoBehaviour`로 만들면 프리팹에 붙어야 하고, 씬에 게임 오브젝트가 필요하고, `GetComponent`로 의존성을 찾아야 한다.

POCO + VContainer 방식:
- 생성자 주입 → 의존성 명시적
- `IInitializable` / `IDisposable` → 수명 VContainer가 관리
- 프리팹 없음, 게임 오브젝트 없음
- 단위 테스트에서 직접 인스턴스 생성 가능

---

## 참고 경로

| 역할 | 경로 |
|------|------|
| GameInputAction | `Client/Assets/Script/Input/GameInputAction.cs` |
| IInputHandler | `Client/Assets/Script/Input/IInputHandler.cs` |
| InputRouter | `Client/Assets/Script/Input/InputRouter.cs` |
| InteractionSystem | `Client/Assets/Script/Input/InteractionSystem.cs` |
| LobbyViewController | `Client/Assets/Script/GUI/OutGame/LobbyViewController.cs` |
| InputRouterTests | `Client/Assets/Script/Tests/EditMode/Input/InputRouterTests.cs` |
| InteractionSystemTests | `Client/Assets/Script/Tests/EditMode/Input/InteractionSystemTests.cs` |
| InputSystemIntegrationTests | `Client/Assets/Script/Tests/PlayMode/Input/InputSystemIntegrationTests.cs` |

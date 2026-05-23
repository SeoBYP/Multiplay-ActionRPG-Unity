# Unity 입력 시스템 설계

> Chapter 1 (입력 버퍼 추상화) + Chapter 10 (InputRouter) 통합 문서

---

## 문제 정의

### 초기 구조의 문제

`PlayerController`가 한 곳에서 너무 많은 책임을 가지고 있었다.

- Unity Input System 콜백 처리
- 이동 속도 계산
- 중력, 접지 체크
- 카메라 회전
- 애니메이션 파라미터 반영

이 구조의 근본 문제는 두 가지였다.

1. **게임플레이 코드가 Unity Input System을 직접 안다** — NPC, 리플레이, 네트워크 재생 입력을 지원하려면 구조 전체를 뒤집어야 한다.
2. **DI와 Unity 생명주기 타이밍이 뒤섞인다** — `OnEnable()`이 `IInitializable.Initialize()`보다 먼저 실행될 수 있어 주입된 입력 객체가 null일 수 있다.

### 두 번째 문제: 동일 키에 복수 소비자

던전 로비 화면에서 `E` 키를 누르면?

- `InteractionSystem` — 월드 오브젝트와 상호작용
- `LobbyViewController` — UI 버튼 확인

두 시스템이 같은 이벤트를 직접 구독하면 처리 순서를 보장할 수 없고, 한 쪽이 처리했을 때 다른 쪽을 막을 방법도 없다.

---

## 설계 원칙

**핵심 원칙:**

게임플레이 코드는 Unity Input System을 직접 참조하지 않는다.  
`PlayerController`, `LocomotionStateMachine`, `ActionStateMachine`은 `PlayerInputActions`를 알면 안 된다.

이 원칙이 가능하게 하는 것:

- 플레이어 입력, NPC 입력, 리플레이 입력, 네트워크 재생 입력을 동일한 인터페이스로 처리
- 게임플레이 로직을 Unity 없이 단위 테스트 가능

---

## 계층 구조

```
Unity Input System
    │  (이벤트)
    ▼
PlayerInputComponent          ← MonoBehaviour, Unity 입력 어댑터
    │  (ICharacterInputWriter)
    ▼
CharacterInputBuffer          ← 단일 입력 저장소
    │  (ICharacterInputSource)
    ▼
게임플레이 시스템              ← 매 프레임 폴링

        ────────────────

PlayerInputActions.performed
    │
    ▼
InputRouter.Route(GameInputAction)
    │  Chain of Responsibility
    ├─ Priority 100: LobbyViewController.TryHandle() → consumed
    ├─ Priority 50:  InteractionSystem.TryHandle()   → consumed
    └─ Priority 10:  ...
```

---

## Layer 1 — 입력 버퍼 시스템

### 입력 생산자와 소비자 분리

처음에는 `ICharacterInputSource` 하나로 읽기와 쓰기를 해결하려 했다.
문제는 `PlayerInputComponent`는 입력을 **써야** 하고, `PlayerController`는 **읽어야** 한다는 것이다.

해결: 읽기와 쓰기 인터페이스를 분리했다.

```csharp
// 입력 생산자 계약 (PlayerInputComponent, NPC AI, 리플레이 시스템이 사용)
public interface ICharacterInputWriter
{
    void SetMove(Vector2 move);
    void SetLook(Vector2 look);
    void PressJump();
    void PressAttack();
    // ...
}

// 입력 소비자 계약 (PlayerController, StateMachine이 사용)
public interface ICharacterInputSource
{
    CharacterInputFrame Current { get; }
    bool ConsumeJumpPressed();
    bool ConsumeAttackPressed();
    // ...
}

// 둘 다 구현하는 단일 저장소
public class CharacterInputBuffer : ICharacterInputWriter, ICharacterInputSource { }
```

`CharacterInputBuffer`는 VContainer에서 **단일 인스턴스(Singleton)**로 등록해야 한다. Transient로 등록하면 생산자와 소비자가 서로 다른 객체를 참조하게 된다.

### 연속 입력과 단발 입력 구분

```
Continuous input (상태값):  Move, Look, SprintHeld
One-shot input  (소비형):   JumpPressed, DodgePressed, InteractPressed, AttackPressed
```

이 두 가지를 같은 방식으로 처리하지 않는다.

**One-shot 입력은 consume 패턴 사용:**

```csharp
// 잘못된 방식 — 누가 false로 돌릴지 책임이 불분명
bool JumpPressed = true;
// 프레임 끝에서 ClearTransientInputs() ← 읽기 순서 의존성 발생

// 올바른 방식 — 읽는 순간 즉시 소모
bool consumed = buffer.ConsumeJumpPressed(); // true 반환 후 즉시 false로 전환
```

Consume 패턴을 쓰면:
- 누가 소비했는지 명확하다
- 여러 시스템이 같은 입력을 두 번 소비하는 사고를 방지한다

### CharacterInputFrame

```csharp
// 값 객체 — 매 프레임 현재 입력 상태의 스냅샷
public struct CharacterInputFrame
{
    public Vector2 Move;
    public Vector2 Look;
    public bool SprintHeld;

    public CharacterInputFrame WithMove(Vector2 move) => new CharacterInputFrame { Move = move, ... };
    public CharacterInputFrame WithLook(Vector2 look) => ...;
}
```

`WithXxx` 패턴으로 부분 갱신 — 부작용 추적이 명확하다.

### 수집은 이벤트, 소비는 폴링

- Unity Input System 이벤트 수집: `performed` 콜백 (이벤트형)
- StateMachine / 게임플레이 입력 소비: `Current` 프로퍼티 매 프레임 읽기 (폴링형)

NPC는 이벤트가 아니라 매 프레임 "의도된 입력 상태"를 만들어내기 때문에 소비 레이어는 폴링이 자연스럽다.

---

## Layer 2 — InputRouter (고수준 입력 라우팅)

### 왜 InputRouter가 필요한가

`PlayerInputActions.Player.Interact.performed += handler`로 직접 구독하면:
- 동일 키를 여러 시스템이 구독할 때 처리 순서 보장 불가
- 한 쪽이 처리했을 때 다른 쪽 차단 불가
- MonoBehaviour에 직접 콜백이 붙어 있으면 Unity 없이 테스트 불가

### GameInputAction 열거형 추상화

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

`InputAction` 오브젝트를 핸들러에 직접 전달하지 않고 자체 열거형으로 변환한다.

이유: `IInputHandler`가 `UnityEngine.InputSystem.InputAction`에 의존하면 `Game.Network`, `Game.OutGame` 같은 하위 레이어가 InputSystem 어셈블리에 의존하게 된다. 열거형 하나로 추상화하면 핸들러를 순수 C# 클래스로 구현할 수 있어 EditMode 테스트에서 Unity 없이 검증 가능하다.

### IInputHandler — Chain of Responsibility

```csharp
public interface IInputHandler
{
    int Priority { get; }
    bool TryHandle(GameInputAction action);  // true 반환 = consumed, 이후 핸들러 차단
}
```

`Priority`를 인터페이스에 포함시킨 이유: 핸들러 자신이 우선순위를 선언하게 한다. 등록 시점에 외부에서 지정하면 핸들러 구현과 우선순위 추적이 분리되어 관리하기 어려워진다.

### InputRouter 구현

```csharp
private void Route(GameInputAction action)
{
    if (_dirty)
    {
        _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        _dirty = false;  // 핸들러 목록이 바뀔 때만 1회 정렬
    }

    foreach (var handler in _handlers)
    {
        if (handler.TryHandle(action)) return;  // consumed → 체인 중단
    }
}
```

UI 핸들러(Priority 100) → 월드 인터랙션(Priority 50) 순서가 항상 보장된다.

### POCO 핸들러 — MonoBehaviour 없이

```csharp
public sealed class LobbyViewController : IInputHandler, IInitializable, IDisposable
{
    public int Priority => 100;  // UI는 월드보다 우선

    public bool TryHandle(GameInputAction action)
    {
        if (action != GameInputAction.ToggleLobby) return false;
        if (_isLobbyOpen) CloseLobby();
        else              OpenLobbyAsync().Forget();
        return true;  // consumed
    }

    public void Initialize() => _router.Register(this);
    public void Dispose()    => _router.Unregister(this);
}
```

MonoBehaviour가 아닌 POCO. VContainer가 생성자 주입으로 의존성을 제공하고 `IInitializable`/`IDisposable`로 수명을 관리한다. 프리팹 없음, 게임 오브젝트 없음.

### Hold 인터랙션 제거

`.inputactions`의 Interact에 `"interactions": "Hold"` 설정이 있었다.  
`InputTestFixture.Press(key)`는 즉시 `performed`를 발생시키므로 Hold가 있으면 테스트에서 항상 실패한다.  
게임플레이에서 E키 상호작용에 0.4초 누름이 필요한 경우는 없다. 특수 상호작용의 Hold 처리는 `IInteractable` 구현체 내부에서 별도로 한다.

---

## 파일 경로

| 역할 | 경로 |
|------|------|
| PlayerInputComponent | `Client/Assets/Script/Main/Character/Input/PlayerInputComponent.cs` |
| ICharacterInputWriter | `Client/Assets/Script/Main/Character/Input/ICharacterInputWriter.cs` |
| ICharacterInputSource | `Client/Assets/Script/Main/Character/Input/ICharacterInputSource.cs` |
| CharacterInputBuffer | `Client/Assets/Script/Main/Character/Input/CharacterInputBuffer.cs` |
| CharacterInputFrame | `Client/Assets/Script/Main/Character/Input/CharacterInputFrame.cs` |
| GameInputAction | `Client/Assets/Script/Input/GameInputAction.cs` |
| IInputHandler | `Client/Assets/Script/Input/IInputHandler.cs` |
| InputRouter | `Client/Assets/Script/Input/InputRouter.cs` |
| InteractionSystem | `Client/Assets/Script/Input/InteractionSystem.cs` |
| LobbyViewController | `Client/Assets/Script/GUI/OutGame/LobbyViewController.cs` |
| InputRouterTests | `Client/Assets/Script/Tests/EditMode/Input/InputRouterTests.cs` |

---

## 테스트 전략

### EditMode 단위 테스트

`InputTestFixture`를 상속해 가상 키보드로 실제 Input System 이벤트를 시뮬레이션한다.

```csharp
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

`TrackingHandler`는 테스트 전용 페이크 — `TryHandle` 호출을 기록하고 실제 동작은 하지 않는다.

---

## 핵심 설계 결정 요약

| 결정 | 이유 |
|------|------|
| 생산자/소비자 인터페이스 분리 | NPC, 리플레이 등 다른 입력 공급자 지원 |
| CharacterInputBuffer Singleton 등록 | 같은 버퍼를 생산자/소비자가 공유해야 함 |
| One-shot → consume 패턴 | 읽기 순서 의존성 제거, 중복 소비 방지 |
| GameInputAction 열거형 | 하위 레이어가 InputSystem 어셈블리에 의존하는 것을 방지 |
| IInputHandler POCO | 생성자 주입, VContainer 수명 관리, 단위 테스트 가능 |
| Priority in interface | 핸들러 자신이 우선순위를 선언 — 추적 가능 |

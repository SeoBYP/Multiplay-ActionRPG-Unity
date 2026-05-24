# 입력 시스템 규칙

## 핵심 원칙

게임플레이 코드는 Unity Input System 타입을 직접 참조하지 않는다.  
`PlayerController`, `LocomotionStateMachine`, `ActionStateMachine`은 `PlayerInputActions`를 알면 안 된다.

## 계층 구조

```
Unity Input System
    ↓ (이벤트)
PlayerInputComponent (MonoBehaviour — 입력 어댑터)
    ↓ (ICharacterInputWriter)
CharacterInputBuffer (단일 저장소, VContainer Singleton)
    ↓ (ICharacterInputSource)
게임플레이 시스템 — 매 프레임 폴링
```

## 각 계층 책임

`PlayerInputComponent`:
- Unity Input System 이벤트 수신 → writer 호출로 변환만 담당.
- 이동 계산, 점프 판정, 상태 전이, 애니메이션 재생 금지.

`ICharacterInputWriter` — 입력 생산자 계약:
- 사용 주체: `PlayerInputComponent`, NPC AI, 리플레이 시스템.

`ICharacterInputSource` — 입력 소비자 계약:
- 사용 주체: `PlayerController`, `LocomotionStateMachine`, `ActionStateMachine`.

`CharacterInputBuffer`:
- `ICharacterInputWriter`와 `ICharacterInputSource` 둘 다 구현.
- VContainer에서 단일 인스턴스(Singleton)로 등록. Transient 등록 시 생산자·소비자가 다른 객체를 참조.

## 입력 종류 구분

Continuous input (상태값 — 매 프레임 현재 값):
- `Move`, `Look`, `SprintHeld`

One-shot input (소비형 신호 — 한 번 읽히면 소모):
- `JumpPressed`, `DodgePressed`, `InteractPressed`, `AttackPressed`

두 종류를 같은 방식으로 처리하지 않는다.

## One-shot 처리 원칙

One-shot 입력은 consume 패턴 사용. 프레임 끝 일괄 초기화 금지.
```csharp
// 올바른 방식
bool consumed = buffer.ConsumeJumpPressed();  // 읽는 순간 즉시 false로 소모
```

프레임 끝에서 `ClearTransientInputs()`로 일괄 초기화하면 읽기 순서 의존성이 생긴다.

## MonoBehaviour 책임 범위

MonoBehaviour는 Unity 경계 어댑터로만 사용한다:
- Unity Input System 연결
- Scene/Transform 연결
- Unity lifecycle 대응

게임 규칙, 이동 계산, 상태 판단은 순수 C# 계층으로 분리.

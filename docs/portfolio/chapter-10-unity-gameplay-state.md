# Unity 게임플레이 상태머신 설계

> Chapter 2 (Locomotion), Chapter 3 (Interaction), Chapter 4 (AttackState / Hit Detection) 통합 문서

---

## 왜 상태머신을 도입했는가

초기 구조에서 `CharacterAgent`와 `PlayerController`는 다음을 한데 섞고 있었다.

- 입력 읽기
- 접지 판정
- 중력 / 이동 계산
- 점프 / 낙하 / 착지 처리
- 상호작용 감지
- 공격 애니메이션 트리거

이 구조에서 점프 하나를 추가하는 순간 문제가 드러났다. 점프는 "버튼 누를 때 위로 이동"이 아니라 **지상 → 공중 → 낙하 → 착지**의 이동 모드 전환이다. 상태 개념 없이는 이 전환을 깔끔하게 표현할 수 없다.

**핵심 원칙:**

State는 캐릭터의 현재 **행동 모드**를 표현한다.  
State 자체가 이동 계산이나 데미지 처리를 직접 하지 않는다.

---

## 상태머신 구조

```
CharacterStateConfig (ScriptableObject)
    ← 어떤 State를 사용할지 데이터로 정의
        ↓
StateMachineBuilder
    ← Config를 읽어 State + Transition 조립
        ↓
StateFactory
    ← StateKind → State 인스턴스 생성만 담당
        ↓
StateMachine
    ← 현재 State, Transition 체크, State 전환
        ↓
State (Grounded / Jump / Fall / Land / Attack / Interact ...)
    ← 현재 행동 모드. Motor에 의도를 전달
        ↓
Motor
    ← 실제 이동 계산 + CharacterController.Move() 적용
```

### StateFactory 책임 범위

기존 StateFactory는 "State를 어떻게 만드는가" + "어떤 캐릭터가 어떤 State를 갖는가"를 동시에 알고 있어 계속 커질 위험이 있었다.

분리 기준:

- `StateFactory`: `StateKind`를 보고 인스턴스를 만드는 것만. 캐릭터 타입을 알지 않는다.
- `CharacterStateConfig`: Player/NPC/Boss가 각각 어떤 State를 사용하는지 ScriptableObject로 정의.
- `StateMachineBuilder`: Config를 읽고 State·Transition을 StateMachine에 조립.

이렇게 나누면 `NPCStateConfig`에서 Attack을 제외하면 NPC는 AttackState를 갖지 않는다. Factory 코드를 수정할 필요 없다.

### Motor의 역할

Motor는 State의 의도를 받아 실제 물리를 적용한다.  
Motor는 "지금 공격 중인가", "점프 중인가"를 알지 않는다. 속도 벡터와 방향만 받는다.

State가 Motor에 과도하게 접근하면 State 간 결합이 생긴다. Motor는 항상 상태 맥락을 모르게 유지한다.

---

## Locomotion States

### 기본 상태 축

```
Grounded → (JumpPressed) → Jump → Fall → Land → Grounded
Grounded → (낙하 시작)   → Fall → Land → Grounded
```

각 State의 책임:

| State | 담당 |
|-------|------|
| `GroundedState` | 수평 이동, 회전, Sprint, 접지 체크 |
| `JumpState` | 점프 초속도 설정, 상승 중 처리 |
| `FallState` | 하강 중력 가속, 착지 체크 |
| `LandState` | 착지 애니메이션, Grounded 복귀 |

### One-shot 입력과 State 연결 순서

점프 입력(`JumpPressed`)을 즉시 처리하지 않는다.  
**JumpState가 생길 때** `ConsumeJumpPressed()`로 연결한다.

이유: CharacterInputBuffer에서 JumpPressed를 consume하는 시점은 JumpState가 결정하는 것이 맞다. 입력 레이어가 "언제 점프해야 한다"는 결정을 하면 안 된다.

### Motor와 State 분리

```csharp
// 잘못된 방식 — State가 물리를 직접 제어
class JumpState {
    void Update() {
        _controller.Move(Vector3.up * jumpSpeed * Time.deltaTime);  // ← Motor 역할 침범
    }
}

// 올바른 방식 — State는 의도를 전달, Motor가 적용
class JumpState {
    void Update() {
        _motor.SetVerticalVelocity(_jumpVelocity);  // 의도만 전달
    }
}
```

---

## Interaction

### 왜 "버튼 → 즉시 실행"이 안 되는가

```csharp
// 이렇게 하면 안 된다
void Update() {
    if (ConsumeInteractPressed() && _nearestInteractable != null)
        _nearestInteractable.Interact(gameObject);
}
```

문제:
- 상호작용 가능 대상을 어떻게 감지하는가?
- 상호작용 중에는 이동을 막아야 하는가?
- NPC 대화, 퀘스트, 무기 줍기가 모두 다른 방식으로 처리된다면?

### 설계: 탐지 → 상태 전환 → 실제 실행

```
InteractionDetector.DetectNearest()  ← 범위 내 IInteractable 탐지
    ↓
Grounded 상태에서 ConsumeInteractPressed() 확인
    ↓
InteractionState로 전환 (이동 입력 무시, 상호작용 애니메이션)
    ↓
IInteractable.Interact(context)  ← 실제 동작
```

`IInteractable`을 구현하는 각 오브젝트(스위치, NPC, 무기 픽업)가 실제 동작을 결정한다. Interaction 상태는 "무언가와 상호작용하는 중"이라는 모드만 표현한다.

이 구조를 쓰면 나중에 "상호작용 가능 대상을 강조 표시", "상호작용 가능 여부를 HUD에 표시" 같은 기능도 `InteractionDetector.CurrentTarget`을 참조하면 되어 확장이 자연스럽다.

---

## AttackState와 Hit Detection

### AttackState의 책임 범위

공격이 필요한 이유: 공격은 단순 함수 호출이 아니라 캐릭터의 행동 모드다.  
공격 중에는 이동 제한, 콤보 입력 수용, 캔슬 조건, 피격 처리, 애니메이션 종료 시점 같은 규칙이 붙는다.

그러나 AttackState가 데미지 처리까지 직접 알면 안 된다.

**AttackState가 담당하는 것:**
- 공격 상태 진입 / 종료
- `Animator.SetTrigger("Attack")` 실행
- 공격 지속 시간 관리
- 중복 타격 방지용 HitTarget Set 초기화

**AttackState가 하면 안 되는 것:**
- Hit 판정
- 데미지 계산 / 적용
- `AbilitySystemComponent` 직접 호출
- Health 직접 수정

### Hit 판정 — Animation Event 기반

공격 버튼을 누른 순간이 아니라 **실제 타격 프레임**에 Hit 판정이 발생해야 한다.

```
PlayerInputActions
    → CharacterInputBuffer (AttackPressed)
    → GroundToAttackTransition
    → AttackState (Animator Trigger 실행)
    → [공격 애니메이션 재생 중]
    → Animation Event: PerformHit
    → CharacterHitEventReceiver.PerformHit()
    → HitDetector.Detect()  (OverlapBox)
    → AbilitySystemComponent (대상)
    → GameplayEffect 적용
    → Health 감소
```

이 구조가 가능하게 하는 확장:
- 무기별 HitBox (주먹, 검, 창, 대검)
- 공격마다 다른 타격 프레임
- 다단 히트, 콤보
- 마법 투사체, 원거리 공격
- 서버 권위 판정

### GAS (Gameplay Ability System) 방향

단순 `target.health -= damage` 방식은 쓰지 않는다.

```csharp
// 현재 MVP 흐름 (임시)
GameplayEffect effect = new GameplayEffect { AttributeType = AttributeType.Health, Modifier = -10 };
target.GetComponent<AbilitySystemComponent>().ApplyEffect(effect);
```

나중에 확장:
- `DamageSpec` — 데미지 유형, 관통, 크리티컬 정의
- `DamageExecution` — 방어력, 저항, 팀 필터링 포함 계산
- 팀/진영/소유자 필터링
- Hit Reaction, Death, Knockback, Guard, 무적 처리

### GameplayAttribute Inspector 직렬화

`GameplayAttribute`를 get-only Property 중심으로 설계하면 Unity Inspector에 직렬화되지 않는다.

```csharp
// Inspector에 보이게 하려면 SerializeField 기반 field가 필요
[Serializable]
public class GameplayAttribute
{
    [SerializeField] private AttributeType _type;
    [SerializeField] private float _baseValue;
    [SerializeField] private float _maxValue;
    [SerializeField] private float _currentValue;

    public AttributeType Type       => _type;
    public float BaseValue          => _baseValue;
    public float MaxValue           => _maxValue;
    public float CurrentValue       => _currentValue;
}
```

`AbilitySystemComponent`는 Attribute 목록이 비어 있으면 기본 Health Attribute를 생성하고, `OnValidate`에서 값 범위를 검증한다.

---

## 파일 경로

| 역할 | 경로 |
|------|------|
| StateKind | `Client/Assets/Script/Main/Character/State/Configs/StateKind.cs` |
| StateDefinition | `Client/Assets/Script/Main/Character/State/Configs/StateDefinition.cs` |
| CharacterStateConfig | `Client/Assets/Script/Main/Character/State/Configs/CharacterStateConfig.cs` |
| StateFactory | `Client/Assets/Script/Main/Character/State/Factory/StateFactory.cs` |
| StateMachineBuilder | `Client/Assets/Script/Main/Character/State/Builder/StateMachineBuilder.cs` |
| AttackState | `Client/Assets/Script/Main/Character/State/AttackState.cs` |
| GroundToAttackTransition | `Client/Assets/Script/Main/Character/State/Transitions/GroundToAttackTransition.cs` |
| CharacterAgent | `Client/Assets/Script/Main/Character/Agent/CharacterAgent.cs` |
| HitDetector | `Client/Assets/Script/Main/Character/Weapon/HitDetector.cs` |
| CharacterHitEventReceiver | `Client/Assets/Script/Main/Character/CharacterHitEventReceiver.cs` |
| AbilitySystemComponent | `Client/Assets/Script/Main/System/GamePlayAbilitySystem/AbilitySystemComponent.cs` |
| GameplayEffect | `Client/Assets/Script/Main/System/GamePlayAbilitySystem/Effects/GameplayEffect.cs` |
| PlayerStateConfig | `Client/Assets/GameResources/StateConfigs/PlayerStateConfig.asset` |
| NpcStateConfig | `Client/Assets/GameResources/StateConfigs/NpcStateConfig.asset` |

---

## 현재 구현 현황

**완료:**
- StateKind, StateDefinition, CharacterStateConfig
- StateFactory (StateKind 기반 생성)
- StateMachineBuilder (Config 읽고 조립)
- CharacterAgent에 CurrentStateKind Inspector 노출
- 공격 입력 → GroundToAttackTransition → AttackState → Animator Trigger
- HitDetector (OverlapBox), CharacterHitEventReceiver (Animation Event)
- GameplayAttribute SerializeField 직렬화
- Health 감소 확인, 빌드 성공

**미완료:**
- TransitionDefinition, TransitionFactory (Transition 데이터 기반화)
- 팀/진영/소유자 필터링
- HitReaction, Death, Knockback, Guard, 무적
- 서버 권위 Hit 판정 및 데미지 처리

---

## 핵심 설계 결정 요약

| 결정 | 이유 |
|------|------|
| StateFactory는 생성만 | 조합 책임을 분리해 Factory가 계속 커지는 것을 방지 |
| CharacterStateConfig ScriptableObject | 코드 수정 없이 캐릭터별 State 구성 변경 가능 |
| Motor 분리 | State 간 결합 제거, 물리 테스트 가능 |
| AttackState가 데미지 모름 | State 책임 제한, GAS 흐름과 분리 |
| Animation Event 기반 Hit | 실제 타격 프레임과 판정 타이밍 일치 |
| IInteractable 인터페이스 | 스위치, NPC, 픽업 등 다양한 상호작용 확장 가능 |

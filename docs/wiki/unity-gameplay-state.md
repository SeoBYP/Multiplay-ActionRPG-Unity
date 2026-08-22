# Unity 게임플레이 상태머신 설계

> **레퍼런스 문서** — 캐릭터 상태머신(Locomotion 축)과 Action 축의 경계를 정의한다.
> 강제 규칙 요약 = [.claude/rules/unity-gameplay-state.md](../../.claude/rules/unity-gameplay-state.md)
> 최종 코드 대조: 2026-08-22
>
> ⚠️ **이 문서는 원래 `AttackState`/`InteractState`가 FSM 상태였던 시절에 쓰였다.**
> 그 둘은 이후 **제거**됐고(두 축 분리, CA-1), 아래 3·4절은 현재 구조로 다시 썼다.

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
State (Ground / Jump / Fall / Land / Climb)   ← Locomotion 축만
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

이렇게 나누면 `NPCStateConfig`에서 `Jump`를 제외하는 것만으로 NPC는 점프 상태를 갖지 않는다. Factory 코드를 수정할 필요 없다.

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

## 두 축 분리 (CA-1) — Action은 FSM 상태가 아니다

이 문서의 초기 버전은 `AttackState`·`InteractState`를 FSM 상태로 두었다. **둘 다 제거됐다.**

```
Locomotion 축 = FSM        Ground → Jump → Fall → Land → Ground,  Climb
                           배타적 "이동 모드". 한 번에 하나만 참이다.

Action 축     = FSM 아님    공격 / 상호작용 / 스킬
                           입력 → 발동. 이동과 동시에 성립한다(직교).
```

**왜 뺐는가** — 공격을 상태로 두면 "이동하면서 공격"이 표현 불가능해진다. 두 축이 배타적이지
않은데 하나의 FSM에 밀어 넣었던 것이 문제였다. 이동 제약(루트 모션·감속)이 필요하면
**상태 전이가 아니라 GameplayEffect/태그**로 표현한다.

---

## 공격 (Action 축)

```
로컬 드라이버(PlayerCharacterAgent.HandleAttackInput)
    → CharacterInputBuffer.ConsumeAttackPressed()   ← 매 프레임 폴링
    → 히트 타깃 Set 리셋 + 공격 애니메이션 트리거
    → [애니메이션 재생 중]
    → Animation Event: PerformHit
    → CharacterHitEventReceiver.PerformHit()
    → HitDetector.Detect()  (OverlapBox)
    → 대상 AbilitySystemComponent → GameplayEffect → Health 감소
```

**Hit 판정은 입력 순간이 아니라 Animation Event 기준**이다. 이래야 무기별 히트박스,
공격마다 다른 타격 프레임, 다단 히트·콤보가 자연스럽게 붙는다.

입력 핸들러가 **하면 안 되는 것**: Hit 판정(`HitDetector`)·데미지 적용·`Health` 직접 수정.
발동과 판정은 분리돼 있어야 서버 권위로 옮길 때 클라 코드가 바뀌지 않는다.

> 던전(코옵)에서는 **판정 자체가 서버 권위**다 — 클라는 `C_Attack`(트리거)만 보내고
> 서버가 시전자 위치·yaw로 히트박스를 재계산한다. 클라의 로컬 판정은 연출·싱글(Main) 경로용이다.
> 상세 = [authority-model.md](authority-model.md) · [gas-architecture.md](gas-architecture.md)

---

## 상호작용 (Action 축)

```
InteractionDetector        매 프레임 최근접 IInteractable 선택 (탐지 전담)
    → 로컬 드라이버가 ConsumeInteractPressed() 폴링
    → IInteractable.Interact(GameObject interactor)   ← 대상이 행동을 소유
```

- 탐지를 건너뛴 `if (E pressed) Interact()` 직결은 금지 — 대상 선택은 `InteractionDetector` 책임.
- `Interact(interactor)`가 **instigator를 받는다**. 아이템 줍기·소비 아이템이 "누구에게" 적용할지
  알아야 하기 때문. 소비 아이템은 그 `IInteractable` 구현체 안에서 `ASC.ApplyEffect`를 호출한다.
- HUD 강조 표시 등은 `InteractionDetector`의 현재 타깃을 참조하면 된다.

> 실작동 경로는 `Game.Gameplay.Character`(detector + `IInteractable`)다.
> `Game.Gameplay.Input.InteractionSystem`(리치/라우터)은 아웃게임 등록·휴면 상태로 중복이며 정리 대상.

---

## GAS 연동

단순 `target.health -= damage`는 쓰지 않는다. 데미지는 항상 GameplayEffect로 간다.

```csharp
target.GetComponent<AbilitySystemComponent>().ApplyEffect(effect);
```

Attribute·Effect·Ability·Cue의 전체 구조와 **2층 연출 분리 / 발동 권위**는
[gas-architecture.md](gas-architecture.md)가 정식 문서다.

---

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
| StateKind | `Client/Assets/Script/Gameplay/Character/State/Configs/StateKind.cs` |
| StateDefinition | `Client/Assets/Script/Gameplay/Character/State/Configs/StateDefinition.cs` |
| CharacterStateConfig | `Client/Assets/Script/Gameplay/Character/State/Configs/CharacterStateConfig.cs` |
| StateFactory | `Client/Assets/Script/Gameplay/Character/State/Factory/StateFactory.cs` |
| StateMachineBuilder | `Client/Assets/Script/Gameplay/Character/State/Builder/StateMachineBuilder.cs` |
| GroundToAttackTransition | `Client/Assets/Script/Gameplay/Character/State/Transitions/GroundToAttackTransition.cs` |
| CharacterAgent | `Client/Assets/Script/Gameplay/Character/Agent/CharacterAgent.cs` |
| HitDetector | `Client/Assets/Script/Gameplay/Character/Weapon/HitDetector.cs` |
| CharacterHitEventReceiver | `Client/Assets/Script/Gameplay/Character/CharacterHitEventReceiver.cs` |
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
- 공격 입력 → 로컬 드라이버 폴링 → Animator Trigger (FSM 전이 없음)
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
| 발동과 판정 분리 | 입력 핸들러는 데미지를 모른다 — 서버 권위 이관이 쉬워진다 |
| Animation Event 기반 Hit | 실제 타격 프레임과 판정 타이밍 일치 |
| IInteractable 인터페이스 | 스위치, NPC, 픽업 등 다양한 상호작용 확장 가능 |

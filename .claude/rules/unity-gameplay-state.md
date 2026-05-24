# 상태머신 및 전투 규칙

## 상태머신 구조

```
CharacterStateConfig   ← 어떤 State를 쓸지 (데이터)
        ↓
StateMachineBuilder    ← Config를 읽어 State + Transition 조립
        ↓
StateFactory           ← StateKind → State 인스턴스 생성만
        ↓
State                  ← 현재 행동 모드 표현
        ↓
Motor                  ← 실제 이동/물리 적용
```

## 각 계층 책임

`StateFactory`:
- `StateKind`를 보고 State 인스턴스를 생성하는 것만 담당.
- "어떤 캐릭터가 어떤 State를 갖는가"는 `CharacterStateConfig` 책임. Factory에 넣지 않는다.

`StateMachineBuilder`:
- `CharacterStateConfig`를 읽는다.
- Config에 포함된 `StateKind`만 생성한다.
- State와 Transition을 StateMachine에 등록한다.
- `InitialState`로 StateMachine을 시작한다.

`State`:
- 현재 행동 모드를 표현한다.
- 이동 수치를 직접 계산하지 않는다. Motor에 의도를 전달한다.

`Motor`:
- 이동 계산과 `CharacterController.Move()` 적용을 담당한다.
- State의 고수준 의도(공격 중, 점프 중)를 알지 않는다.

## Locomotion State 축

기본 상태: `Grounded` → `Jump` → `Fall` → `Land` → `Grounded`

점프는 "버튼을 누르면 위로 움직이는 기능"이 아니라 이동 모드 전환이다.  
`Jump` 입력은 `JumpState`가 생길 때 `ConsumeJumpPressed()`로 연결. 즉시 강제 연결 금지.

## AttackState 책임 범위

담당하는 것:
- 공격 상태 진입 및 종료
- Animator Trigger 실행
- 공격 지속 시간 관리
- 중복 타격 방지용 HitTarget 목록 초기화

직접 하면 안 되는 것:
- Hit 판정 (`HitDetector` 책임)
- 데미지 적용 (`AbilitySystemComponent` → `GameplayEffect` 흐름)
- Health 직접 수정
- `AbilitySystemComponent` 직접 호출

## Hit 판정 흐름

Hit 판정은 공격 버튼을 누른 순간이 아니라 **Animation Event** 기준으로 실행한다.

```
Animation Event (타격 프레임)
    → CharacterHitEventReceiver.PerformHit()
    → HitDetector.Detect()
    → AbilitySystemComponent (대상)
    → GameplayEffect 적용
    → Health 감소
```

## 상호작용 원칙

상호작용을 `if (E pressed) Interact()` 패턴으로 구현하지 않는다.  
탐지(InteractionDetector) → 상태 전환(InteractionState) → 실제 실행(IInteractable.Interact()) 흐름으로 처리.

## CharacterStateConfig와 캐릭터 분리

`PlayerStateConfig`와 `NPCStateConfig`는 별도 ScriptableObject.  
NPC가 사용하지 않는 State (예: Attack)는 NPCStateConfig에서 제외.  
StateFactory는 두 Config를 동일하게 처리 — 캐릭터 타입을 알지 않는다.

## GameplayAttribute Inspector 직렬화

`GameplayAttribute`는 `SerializeField` 기반 private field로 값을 보관.  
get-only Property만 있으면 Unity Inspector에 직렬화되지 않는다.

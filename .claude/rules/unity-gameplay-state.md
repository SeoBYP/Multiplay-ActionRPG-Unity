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

## 두 축 분리 (CA-1, 최우선)

캐릭터는 **두 축**으로 나눈다. 섞지 않는다.
- **Locomotion 축** = FSM (`Ground/Jump/Fall/Land`) — 배타적 이동 모드.
- **Action 축**(공격/상호작용/스킬) = **FSM 상태가 아니다.** 입력→발동(GAS/대상 위임).

→ **`AttackState`/`InteractState`는 없다(제거됨).** Action을 새 FSM 상태로 만들지 말 것.
이동 제약(루트/감속)이 필요하면 **GameplayEffect/태그**로 표현(상태 전이 아님).

## Attack = Action 축 (FSM 아님)

- 발동: 로컬 드라이버(`PlayerCharacterAgent.HandleAttackInput`)가 `ConsumeAttackPressed`로 폴링 → 히트타겟 리셋 + 공격 애니 트리거. (이동 중 공격 가능 = 두 축 직교)
- 데미지: **Animation Event 기준**(아래 Hit 흐름) — 입력 순간이 아님.
- 직접 하면 안 되는 것: Hit 판정(`HitDetector`)/데미지 적용은 입력 핸들러가 하지 않는다.
- ※ 스윙의 정식 GAS 어빌리티化(쿨다운·active window·서버 권위 예측)는 CA-3. 그 전엔 입력 폴링 + 기존 GAS 데미지 체인.

## Hit 판정 흐름

Hit 판정은 공격 버튼을 누른 순간이 아니라 **Animation Event** 기준으로 실행한다.

```
Animation Event (타격 프레임)
    → CharacterHitEventReceiver.PerformHit()
    → HitDetector.Detect()
    → GasComponent (대상 — Shared AbilitySystemComponent 로 위임)
    → GameplayEffect 적용
    → Health 감소
```

## 상호작용 원칙 (Action 축 — FSM 아님)

`InteractState`(FSM)는 제거됐다. 탐지/위임 흐름으로 처리한다:

```
InteractionDetector (탐지, 매 프레임 최근접 IInteractable 선택)
   → 로컬 드라이버 입력 폴링 (ConsumeInteractPressed)
   → IInteractable.Interact(interactor)   ← 대상(문/아이템/NPC)이 행동을 소유
```

- 탐지를 건너뛴 `if (E pressed) Interact()` 직결 금지(탐지는 `InteractionDetector`가 분리 담당).
- **`IInteractable.Interact(GameObject interactor)`** — instigator를 받는다(아이템 줍기/효과 대상 식별). 소비 아이템 등은 그 구현체가 `ASC.ApplyEffect`를 호출(아이템↔GAS는 interactable 안에서 합류).
- ※ 던전 상호작용 실작동 경로 = `Game.Gameplay.Character`(detector+`IInteractable`). `Game.Gameplay.Input.InteractionSystem`(리치/라우터)은 아웃게임 등록·휴면 중복 → 일원화는 별도 정리 대상.

## CharacterStateConfig와 캐릭터 분리

`PlayerStateConfig`와 `NPCStateConfig`는 별도 ScriptableObject.  
캐릭터가 사용하지 않는 Locomotion State(예: NPC가 `Jump` 미사용)는 해당 Config에서 제외.  
(Action은 더 이상 State가 아니므로 Config엔 Locomotion만 남음.)  
StateFactory는 두 Config를 동일하게 처리 — 캐릭터 타입을 알지 않는다.

## GameplayAttribute Inspector 직렬화

`GameplayAttribute`는 `SerializeField` 기반 private field로 값을 보관.  
get-only Property만 있으면 Unity Inspector에 직렬화되지 않는다.

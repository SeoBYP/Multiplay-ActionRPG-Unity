# Chapter_4 - Attack State와 Hit Detection 학습 로그

## 문서 목적

이번 학습 로그는 공격 애니메이션을 State 시스템에 연결하고, 근접 공격의 Hit 판정과 임시 데미지 처리를 붙인 과정을 정리한다.

핵심은 공격을 단순히 데미지를 주는 코드로 보지 않고, 다음 세 가지 책임으로 나누는 것이다.

- State는 캐릭터의 현재 행동 모드와 애니메이션 전환을 담당한다.
- Hit 판정은 공격 애니메이션의 실제 타격 프레임에 맞춰 실행한다.
- 데미지는 단순 Health 감소가 아니라 AbilitySystemComponent와 GameplayEffect 흐름으로 처리한다.

## 이번 챕터의 핵심 주제

이번 챕터에서 다룬 주제는 네 가지다.

1. StateFactory의 책임을 줄이고 StateMachineBuilder를 분리했다.
2. 공격 입력을 StateMachine 흐름에 연결했다.
3. AttackState는 공격 애니메이션 진입만 담당하도록 정리했다.
4. HitDetector와 CharacterHitEventReceiver를 통해 데미지 적용 흐름을 만들었다.

## 시작 시점의 문제

처음 고민한 문제는 다음과 같았다.

- DamageSystem이 너무 간단해서 Action RPG 전투 구조로 확장하기 어렵다.
- StateFactory가 State 생성뿐 아니라 어떤 State를 조합할지도 알고 있어서 계속 길어질 위험이 있다.
- State가 많아질수록 Code Small 기준에는 맞지만, 전체 구조를 어디서 조립해야 하는지 애매하다.
- HitDetector가 근접, 마법, 원거리까지 확장하기에는 부족하다.
- 공격 애니메이션은 먼저 확인해야 하므로, 데미지보다 State와 Animation 연결이 우선이었다.

## 실제로 했던 질문

이번 챕터에서 기준을 잡기 위해 던진 질문은 다음과 같다.

- AttackState로 빼는 것이 맞는가?
- StateFactory는 생성만 담당하고, 어떤 State를 쓸지는 Data로 관리해야 하지 않는가?
- StateMachineBuilder를 따로 두는 것이 맞는가?
- 공격 입력과 StateConfig는 어디서 연결해야 하는가?
- RightHook 공격 애니메이션의 Hit 판정은 어떤 방식으로 처리해야 하는가?
- Attribute가 Inspector에 보이지 않는 문제는 왜 발생했는가?

## 결정 1 - AttackState는 필요하다

AttackState는 필요하다고 판단했다.

이유는 공격이 단순한 함수 호출이 아니라 캐릭터의 행동 모드이기 때문이다. 공격 중에는 이동, 회전, 캔슬, 피격, 콤보 입력, 무기 판정, 애니메이션 종료 시점 같은 규칙이 붙는다.

다만 AttackState가 데미지 처리까지 직접 알면 안 된다.

AttackState의 책임은 다음 정도로 제한한다.

- 공격 상태 진입
- Animator Trigger 실행
- 공격 상태 지속 시간 관리
- 공격 중 중복 타격 목록 초기화 요청
- 공격 종료 후 다른 State로 복귀

Hit 판정과 데미지 적용은 AttackState 밖에서 처리한다.

## 결정 2 - StateFactory는 생성만 담당한다

기존 StateFactory는 계속 커질 위험이 있었다.

StateFactory가 다음 두 가지를 동시에 알고 있으면 구조가 무거워진다.

- State 객체를 어떻게 생성하는가
- 어떤 캐릭터가 어떤 State들을 가지는가

이번 작업에서는 책임을 나눴다.

- StateDefinition과 CharacterStateConfig는 사용할 State 목록과 초기 State를 가진다.
- StateFactory는 StateKind를 보고 실제 State 인스턴스를 생성한다.
- StateMachineBuilder는 Config를 읽고 StateMachine에 State와 Transition을 조립한다.

이렇게 나누면 Player, NPC, Boss가 서로 다른 State 구성을 가져도 Factory가 계속 커지는 문제를 줄일 수 있다.

## 결정 3 - StateMachineBuilder를 분리한다

StateMachineBuilder는 새 폴더와 새 클래스로 분리했다.

역할은 명확하다.

- CharacterStateConfig를 읽는다.
- Config에 포함된 StateKind만 생성한다.
- 생성된 State를 StateMachine에 등록한다.
- 필요한 Transition을 등록한다.
- InitialState를 기준으로 StateMachine을 시작한다.

현재 Transition은 아직 코드 기반으로 연결되어 있다. 다음 단계에서는 TransitionDefinition과 TransitionFactory를 추가해서 Transition도 데이터 기반으로 옮기는 것이 자연스럽다.

## 결정 4 - 공격 입력 흐름

공격 입력은 기존 입력 버퍼 구조에 맞춰 추가했다.

흐름은 다음과 같다.

```text
PlayerInputActions
-> PlayerInputComponent
-> CharacterInputBuffer
-> CharacterInputFrame.AttackPressed
-> GroundToAttackTransition
-> AttackState
-> Animator Attack Trigger
```

StateConfig에는 PlayerStateConfig에 Attack State를 추가했다.

이 구조의 장점은 NPCStateConfig에는 Attack을 넣지 않을 수 있다는 점이다. 즉, 캐릭터 타입별로 사용 가능한 State를 Config에서 다르게 가져갈 수 있다.

## 결정 5 - Hit 판정은 Animation Event 기반으로 간다

근접 공격의 Hit 판정은 공격 버튼을 누른 순간이 아니라 실제 손, 무기, 발이 맞는 프레임에 발생해야 한다.

따라서 AttackState 안에서 바로 데미지를 주지 않고, 공격 애니메이션 클립의 타격 프레임에서 Animation Event를 호출하는 방식으로 정리했다.

현재 의도한 흐름은 다음과 같다.

```text
RightHook Animation Event
-> CharacterHitEventReceiver.PerformHit()
-> HitDetector.Detect()
-> AbilitySystemComponent target
-> GameplayEffect 적용
-> Health 감소
```

이 방식은 나중에 다음 기능으로 확장하기 좋다.

- 주먹, 검, 창, 대검 같은 무기별 HitBox
- 공격마다 다른 타격 프레임
- 다단 히트
- 콤보 공격
- 마법 투사체
- 원거리 공격
- 서버 권위 판정

## 구현된 주요 구조

이번 챕터에서 추가하거나 수정한 주요 구조는 다음과 같다.

- StateKind로 State 종류를 명시한다.
- StateDefinition으로 State별 설정을 가진다.
- CharacterStateConfig로 캐릭터가 사용할 State 목록을 가진다.
- IStateFactory와 StateFactory는 State 생성만 담당한다.
- IStateMachineBuilder와 StateMachineBuilder는 StateMachine 조립을 담당한다.
- CharacterAgent는 현재 StateKind를 Inspector에서 확인할 수 있게 한다.
- GroundToAttackTransition은 AttackPressed 입력을 보고 AttackState로 전환한다.
- AttackState는 Animator Attack Trigger와 HitTarget 초기화를 담당한다.
- HitDetector는 OverlapBox 기반으로 공격 범위 내 AbilitySystemComponent를 찾는다.
- CharacterHitEventReceiver는 Animation Event를 받아 HitDetector를 실행하고 데미지를 적용한다.

## Attribute가 Inspector에 보이지 않았던 이유

GameplayAttribute가 get-only Property 중심으로 되어 있어서 Unity Inspector에 직렬화되지 않았다.

Unity에서 Inspector에 보이려면 일반적으로 SerializeField가 붙은 Field가 필요하다. 그래서 GameplayAttribute를 다음 방식으로 정리했다.

- AttributeType
- BaseValue
- MaxValue
- CurrentValue

위 값들을 SerializeField 기반의 private field로 보관하고, 외부에는 read-only property로 노출했다.

AbilitySystemComponent는 Attribute 목록이 비어 있으면 기본 Health Attribute를 생성하도록 정리했다. OnValidate에서도 Attribute 값이 유효한 범위에 있도록 검증한다.

## 오늘 얻은 기준

이번 작업에서 얻은 기준은 다음과 같다.

- AttackState는 필요하지만 데미지 시스템을 직접 알면 안 된다.
- StateFactory는 생성만 담당해야 한다.
- StateMachineBuilder는 Config를 읽고 StateMachine을 조립하는 책임을 가진다.
- 어떤 State를 사용할지는 코드가 아니라 CharacterStateConfig에서 결정해야 한다.
- 근접 공격 Hit 판정은 Animation Event가 기준이 되어야 한다.
- 데미지는 단순 Health 감소보다 GameplayEffect 흐름으로 보내는 것이 확장에 유리하다.
- Unity Inspector에 보여야 하는 값은 SerializeField 기반으로 설계해야 한다.

## 현재 검증된 내용

현재 확인된 내용은 다음과 같다.

- Attack 입력으로 AttackState에 진입한다.
- PlayerController에서 RightHook 공격 애니메이션이 실행된다.
- Hit 판정 이후 대상 Health가 감소한다.
- GameplayAttribute가 Inspector에 보인다.
- Game.Main.csproj 기준 빌드가 성공한다.

## 아직 남은 문제

현재 구조는 MVP 단계다.

다음 문제는 아직 남아 있다.

- TransitionDefinition과 TransitionFactory가 아직 없다.
- Transition 연결은 아직 StateMachineBuilder 코드에 남아 있다.
- HitDetector는 OverlapBox 기반이라 무기 궤적이나 프레임 사이 누락 문제를 완전히 해결하지는 않는다.
- 공격자와 대상의 팀, 진영, 소유자 필터링이 없다.
- GameplayEffect는 임시로 Health를 Additive 감소시키는 수준이다.
- HitReaction, Death, Knockback, Guard 같은 전투 결과 처리가 없다.
- 멀티플레이 기준 서버 권위 데미지 처리가 아직 없다.

## 다음 챕터로 이어질 작업

다음에 이어서 할 작업은 다음 순서가 좋다.

1. RightHook 애니메이션 클립의 정확한 타격 프레임에 PerformHit Animation Event를 배치한다.
2. HitDetector의 BoxCenterOffset, BoxSize, TargetLayerMask를 Player 기준으로 튜닝한다.
3. 더미 적 Prefab에 AbilitySystemComponent, Health Attribute, Collider, Layer를 정리한다.
4. HitReaction 또는 Death State를 추가한다.
5. TransitionDefinition과 TransitionFactory를 만들어 Transition도 데이터 기반으로 옮긴다.
6. GameplayEffect를 DamageSpec 또는 DamageExecution 구조로 확장한다.
7. 멀티플레이 기준으로 서버 권위 Hit 판정과 데미지 적용 흐름을 설계한다.


# Chapter_2 - Jump State 학습 로그

## 문서 목적

이 문서는 Locomotion 구조를 만들면서 `Jump / Fall / Land`를 상태로 분리한 과정에서:

- 어떤 질문이 나왔는지
- 어디서 설계 판단이 흔들렸는지
- 어떤 개념으로 정리했는지
- 지금 구조를 왜 이렇게 가져갔는지

를 기록하는 학습 로그다.

이 문서는 구현 변경 내역 요약이 아니라, 이후 새 채팅에서도 같은 판단 기준을 재사용하기 위한 개념 문서다.

---

## 이번 챕터의 핵심 주제

이번 챕터의 핵심은 단순히 “점프를 추가했다”가 아니다.

핵심은 아래 두 가지였다.

1. `Jump`를 기능이 아니라 **Locomotion State**로 본다.
2. 상태 판단과 실제 이동 적용을 분리하기 위해 **Motor 계층**을 둔다.

즉 이번 챕터는:

- `Grounded`
- `Jump`
- `Fall`
- `Land`

를 갖는 최소 locomotion 상태축을 세우는 단계였다.

---

## 시작 시점의 문제

처음 구조에서는 `CharacterAgent` 안에 다음이 한데 섞여 있었다.

- 입력 읽기
- 접지 판정
- 점프 입력 처리
- 중력 적용
- 수평 이동 계산
- 실제 `CharacterController.Move(...)`
- 애니메이션 반영

이 구조의 문제는 점프 하나를 넣는 순간 바로 드러났다.

점프는 단순히 버튼을 눌렀을 때 위로 튀는 기능이 아니라:

- 지상에서 시작하고
- 공중으로 전환되고
- 정점 이후 낙하하며
- 착지 후 다시 지상 상태로 복귀하는

**이동 모드 전환**이기 때문이다.

즉 점프를 함수 하나로 붙이면 이후 `Fall`, `Land`, `Traversal`, `Climb`까지 같은 방식으로 얽히게 된다.

---

## 처음 던졌던 질문

이번 챕터에서 실제로 중요했던 질문은 아래와 같았다.

### 1. Jump는 함수인가, 상태인가?

처음에는 `TryJump()` 같은 메서드로 붙일 수도 있어 보였다.

하지만 점프는:

- 초기 수직 속도 부여
- 공중 체류
- 낙하 전환
- 착지 전환

을 가지므로 기능 하나가 아니라 **상태 흐름**으로 보는 게 맞다.

정리:

**Jump는 액션 메서드가 아니라 locomotion state 축에 들어가는 상태다.**

---

### 2. 그러면 Vault, Mantle, Climb도 전부 상태로 늘어나는가?

이 질문 때문에 상태가 폭발할 수 있다는 우려가 생겼다.

여기서 정리한 기준은 다음과 같다.

- 큰 이동 모드 변화: `State`
- 모드 안의 세부 실행: `Action`

즉:

- `Jump / Fall / Land / Traversal / Climb`는 상태
- `Vault / Mantle / Shimmy / Hop`은 상태 내부 액션

으로 관리해야 한다.

이 기준이 없으면 이후 파쿠르와 등반에서 상태 수가 제어되지 않는다.

---

### 3. Motor 같은 계층이 정말 필요한가?

처음에는 이런 의문이 있었다.

- 이미 `GroundedDetector`도 분리했고
- 애니메이션도 분리했는데
- `CharacterMotor`까지 또 나눠야 하나?

여기서 중요한 깨달음은:

**State와 Transition은 판단 계층이고, Motor는 실행 계층**이라는 점이었다.

상태는:

- 지금 Ground인지 Jump인지 판단하고
- 어떤 속도로 움직여야 하는지 계산한다.

하지만 실제로:

- 속도를 `CharacterController.Move(...)`에 적용하고
- 현재 이동 속도를 읽고
- 회전과 이동을 조합하는 일

은 상태의 책임이 아니다.

정리:

**Motor는 “지금 공중인지 지상인지”를 모른다. 오직 계산된 움직임을 실제로 적용하는 역할만 맡는다.**

---

## 이번 챕터에서 정리된 최종 역할

### 1. `State`

역할:

- 현재 locomotion 모드 표현
- 중력/수직 속도 규칙 결정
- 애니메이션 상태 결정
- 전이 트리거 연결

현재 정의:

- `GroundState`
- `JumpState`
- `FallState`
- `LandState`

---

### 2. `Transition`

역할:

- 언제 상태를 바꿀지 판단

예:

- `GroundedToJumpTransition`
- `GroundedToFallTransition`
- `JumpToFallTransition`
- `FallToLandTransition`
- `LandToMovementTransition`

이번 챕터에서 중요한 포인트는:

**점프 가능 여부, 착지 복귀 시간, 낙하 전환 같은 조건을 state 내부 if문 덩어리로 몰지 않고 transition으로 분리했다는 점**이다.

---

### 3. `CharacterMotor`

역할:

- 실제 이동 적용
- 현재 속도 보관
- 회전 전략과 연동

중요한 개념:

`GroundState`와 `JumpState`는 모두 Motor를 사용하지만, Motor는 그것이 지상 이동인지 공중 이동인지 구분하지 않는다.

즉:

- State는 의도 계산
- Motor는 적용

으로 역할이 분리된다.

---

### 4. `LocomotionSettings`

역할:

- locomotion 관련 튜닝 수치의 단일 진실 공급원

이전에는:

- move speed
- gravity
- jump height
- fall timeout
- land duration

같은 값들이 각 state와 transition 안에 흩어져 있었다.

이번 챕터에서는 이 값을 `LocomotionSettings`로 모아,
Factory가 state 생성 시 넘겨주도록 정리했다.

이로 인해:

- 튜닝 포인트가 한 곳으로 모이고
- VContainer 기반 생성과도 자연스럽게 연결됐다.

---

## VContainer를 왜 여기서 붙였는가

이번 챕터에서 DI는 모든 것을 주입하기 위한 목적이 아니었다.

핵심 목적은:

**`CharacterAgent`가 state 생성 책임을 직접 들고 있지 않게 만드는 것**이었다.

정리된 구조:

- `CharacterAgent`
  - 로컬 컴포넌트 수집
  - 현재 state 보관
  - state 전환 실행
- `LocomotionStateFactory`
  - settings를 받아 state/transition 생성
- `CharacterLocomotionContext`
  - 캐릭터 로컬 참조 묶음

즉:

- 전역 설정과 생성 책임은 VContainer
- 캐릭터 로컬 참조는 `GetComponent`

이 경계가 이번 챕터에서 매우 중요한 설계 기준이었다.

---

## 이번 챕터에서 틀렸던 가정

### 1. “점프는 입력 처리만 붙이면 된다”

틀렸다.

점프는 입력 이벤트가 아니라 이동 모드 전환이다.

---

### 2. “Motor는 없어도 된다”

짧게는 가능했지만, 상태가 생기기 시작하면 이동 적용 로직이 곧바로 중복된다.

즉 장기적으로 틀린 판단이었다.

---

### 3. “DI를 붙이면 state 구조가 곧 정리된다”

틀렸다.

DI는 상태 설계를 대신하지 않는다.

먼저:

- Context
- Settings
- Factory

경계를 정해야 하고, 그 다음에 주입이 의미를 가진다.

---

## 이번 챕터에서 얻은 기준

앞으로 locomotion 관련 작업에서는 아래 기준을 유지한다.

1. 큰 이동 모드 변화는 `State`로 관리한다.
2. 세부 실행은 state 내부 action 또는 variant로 관리한다.
3. 실제 이동 적용은 `Motor`가 담당한다.
4. 캐릭터 로컬 참조는 무리하게 DI하지 않는다.
5. 공통 튜닝 값은 `Settings`로 모은다.
6. 상태 생성 책임은 `Factory`로 모은다.

---

## 다음 챕터로 이어지는 연결점

이번 챕터가 끝난 시점에서 locomotion은 다음 구조를 가지게 되었다.

- `GroundState`
- `JumpState`
- `FallState`
- `LandState`
- `CharacterMotor`
- `LocomotionStateFactory`
- `LocomotionSettings`

이 기반 위에서 다음에 붙은 기능이 `Interact`다.

즉 다음 챕터는:

**지상 locomotion 위에 상호작용 상태를 어떻게 얹을 것인가**

를 다룬다.

---

## 이후 같은 형식으로 로그를 쓸 때의 기준

다음 챕터 문서도 아래 형식을 유지한다.

1. 문서 목적
2. 이번 챕터의 핵심 주제
3. 시작 시점의 문제
4. 실제로 던졌던 질문
5. 틀렸던 가정
6. 최종 역할 정리
7. 이번 챕터에서 얻은 기준
8. 다음 챕터로 이어지는 연결점

구현 변경 목록보다:

- 왜 이런 구조가 필요했는지
- 어떤 질문 때문에 구조가 바뀌었는지
- 어떤 개념으로 정리됐는지

를 우선 기록한다.

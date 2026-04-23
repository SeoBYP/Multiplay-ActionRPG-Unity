# Chapter_1 - 입력 아키텍처 학습 로그

## 문서 목적

이 문서는 이번 클라이언트 입력 분리 작업에서:

- 내가 어떤 문제를 겪었는지
- 어떤 질문을 했는지
- 어디서 잘못 생각했는지
- 그 문제를 어떤 개념으로 풀었는지
- 앞으로는 어떤 기준으로 판단할지

를 정리한 학습 로그다.

이 문서는 구현 로그가 아니라 **개념 중심 학습 로그**다.
다음 채팅에서 이어서 작업할 때 먼저 읽는 기준 문서로 사용한다.

---

## 이번 챕터의 핵심 주제

이번 챕터의 주제는 단순히 "입력 코드 분리"가 아니다.

핵심은 다음 한 줄이다.

**게임플레이 코드는 Unity Input System을 직접 알지 않고, 정규화된 캐릭터 입력만 알아야 한다.**

이 원칙이 필요한 이유는 앞으로 다음을 모두 지원해야 하기 때문이다.

- 플레이어 입력
- NPC 입력
- 나중의 상태머신 기반 locomotion
- 파쿠르 / traversal
- 무기 상태
- 리플레이 / 네트워크 재생 입력

즉, 이번 챕터는 "입력을 받는 법"이 아니라 **입력을 게임 시스템에서 어떻게 다뤄야 하는가**에 대한 기준을 세우는 작업이었다.

---

## 시작 시점 문제 상황

기존 구조에서 `PlayerController`는 너무 많은 책임을 가지고 있었다.

- Unity Input System 콜백 처리
- 이동 속도 계산
- 회전
- 중력
- 접지 체크
- 카메라 회전
- 애니메이션 파라미터 반영

이 구조의 문제는 단순히 코드가 길다는 게 아니었다.

실제 문제는 두 가지였다.

1. **책임이 섞여 있어서 확장이 어려움**
2. **DI와 Unity 생명주기 타이밍이 섞여 입력 초기화가 불안정함**

특히 `OnEnable()`이 `IInitializable.Initialize()`보다 먼저 실행될 수 있기 때문에,
주입된 입력 객체를 `OnEnable()`에서 바로 사용하면 null 문제가 터질 수 있었다.

여기서 중요한 학습 포인트는:

**Unity 생명주기와 DI 생명주기는 같은 타이밍 축이 아니다.**

---

## 내가 처음에 헷갈렸던 지점들

이번 작업에서는 입력 구조를 설계하면서 몇 번의 중요한 오해가 있었다.
이걸 남겨둬야 다음 챕터에서 같은 실수를 반복하지 않는다.

### 1. "Input은 MonoBehaviour Component로 가야 하나?"

처음 질문은 이거였다.

입력을 분리하려고 할 때 `PlayerInputComponent` 같은 MonoBehaviour가 필요한가?
아니면 일반 C# 클래스로만 가야 하는가?

### 당시 헷갈린 이유

- Unity Input System은 보통 MonoBehaviour와 같이 쓴다.
- 하지만 입력 상태를 게임플레이 전반에서 쓰려면 MonoBehaviour에 묶이면 불편하다.
- NPC까지 생각하면 Unity 이벤트 콜백 구조를 그대로 재사용할 수 없다.

### 정리된 결론

여기서 핵심은 "입력 전체가 MonoBehaviour인가?"가 아니라
**어느 층이 MonoBehaviour여야 하는가**였다.

정리하면:

- **Unity Input System과 직접 붙는 어댑터**는 MonoBehaviour여도 된다.
- **게임플레이가 읽는 입력 상태/계약**은 MonoBehaviour에 묶지 않는 게 좋다.

즉:

- `PlayerInputComponent`는 MonoBehaviour
- `CharacterInputBuffer`, `ICharacterInputSource`, `ICharacterInputWriter`는 일반 C# 계층

이 구분이 중요하다.

---

### 2. "InputSource에 Current만 있으면 역할이 너무 약한 것 아닌가?"

이 질문도 매우 중요했다.

처음에는 `ICharacterInputSource`에 `Current`만 두고 시작했다.

예:

```csharp
public interface ICharacterInputSource
{
    CharacterInputFrame Current { get; }
}
```

그런데 곧바로 문제가 보였다.

- `PlayerInputComponent`는 입력을 **읽는 게 아니라 써야 한다**
- NPC도 입력을 **만들어 넣어야 한다**
- 그런데 `Current`만 있으면 읽기만 가능하고 쓰기가 안 된다

### 당시 잘못 생각한 부분

처음에는 `InputSource` 하나로 읽기와 쓰기를 다 해결하려고 했다.
그런데 실제로는 **입력 생산자와 입력 소비자는 역할이 다르다.**

### 정리된 결론

읽기와 쓰기를 분리해야 했다.

- `ICharacterInputSource`
  - 게임플레이가 읽는 계약
- `ICharacterInputWriter`
  - 입력 생산자가 쓰는 계약
- `CharacterInputBuffer`
  - 둘 다 구현하는 저장소

이렇게 분리하고 나니 구조가 명확해졌다.

즉, 이 질문의 결론은:

**`Current`만 있는 `InputSource`는 읽기 계약으로는 충분하지만, 전체 입력 시스템으로는 writer가 별도로 필요하다.**

---

### 3. "이벤트형 vs 폴링형이 뭐고, 무엇을 써야 하나?"

이 질문은 구조를 결정하는 핵심 질문이었다.

처음엔 Unity Input System이 이벤트 기반으로 동작하니,
게임 전체도 이벤트 중심으로 끌고 가야 하나 고민이 있었다.

### 여기서 부딪힌 개념 문제

입력은 두 층으로 나눠 생각해야 한다.

1. **입력을 수집하는 층**
2. **게임플레이가 입력을 소비하는 층**

### 정리된 결론

이번 구조는 **하이브리드**로 가기로 했다.

- Unity Input System 입력 수집: 이벤트형
- Locomotion / Action / 상태머신 입력 소비: 폴링형

왜냐하면:

- Unity Input System은 이벤트로 받는 게 자연스럽다.
- 하지만 locomotion/state machine은 매 프레임 현재 입력 상태를 읽는 게 더 안정적이다.
- NPC는 이벤트가 아니라 매 프레임 "의도된 입력 상태"를 만들어내기 때문이다.

즉 결론은:

**수집은 이벤트, 소비는 폴링**

이게 앞으로도 유지할 기준이다.

---

### 4. "one-shot 입력은 어떻게 관리해야 하나?"

이건 이번 챕터에서 가장 중요한 학습 포인트였다.

처음엔 `JumpPressed`, `DodgePressed`, `InteractPressed`를 bool로 저장하는 정도로 생각했다.

그런데 바로 문제가 생겼다.

예를 들어:

1. `PressJump()` 호출
2. `JumpPressed = true`
3. 아무도 false로 안 돌림
4. 게임 로직은 매 프레임 점프가 눌렸다고 오해

즉, 버튼을 한 번 눌렀는데 영원히 눌린 상태처럼 보일 수 있었다.

### 당시 막혔던 지점

"그럼 이걸 언제 false로 돌려야 하지?"

여기서 두 가지 후보가 나왔다.

#### 후보 1. 프레임 끝에서 전부 지우기

- 입력이 들어오면 true
- 프레임 끝에서 `ClearTransientInputs()`

문제:

- 누가 clear할지 책임이 애매하다
- 읽는 순서에 따라 미묘한 타이밍 문제가 생긴다
- 나중에 시스템이 많아질수록 추적이 어려워진다

#### 후보 2. consume 패턴

- `ConsumeJumpPressed()`
- 읽을 때 true를 반환하고 바로 false로 되돌림

### 정리된 결론

이번 구조는 **consume 패턴**으로 가기로 했다.

즉, one-shot 입력은 단순 bool이 아니라:

- 한 번만 읽히고
- 읽히는 순간 소모되는 신호

로 취급한다.

이 결론은 앞으로도 매우 중요하다.

즉:

- `Move`, `Look`, `SprintHeld`는 상태값
- `JumpPressed`, `DodgePressed`, `InteractPressed`는 소비형 신호

이 구분은 절대 섞지 않는다.

---

### 5. "DI가 안 들어오는 것 같아요"

이건 구현 단계에서 실제로 헷갈렸던 부분이다.

입력을 쓰는 쪽과 읽는 쪽을 분리한 뒤,
겉으로 보기에는 DI가 안 되는 것처럼 보였다.

### 실제 원인

문제는 DI가 "안 들어온 것"이 아니었다.

문제는 `CharacterInputBuffer`를 `Transient`로 등록해 둔 것이었다.

그 결과:

- `PlayerInputComponent`는 `ICharacterInputWriter`로 A 인스턴스를 받음
- `PlayerController`는 `ICharacterInputSource`로 B 인스턴스를 받음

즉, 둘 다 주입은 됐는데 서로 **다른 객체**를 보고 있었다.

### 여기서 배운 점

DI 문제를 볼 때는 단순히 "null인가?"만 보면 안 된다.

다음도 같이 봐야 한다.

- 생명주기
- scope
- 같은 인터페이스를 통해 같은 인스턴스를 받는지

이번 케이스의 핵심 학습은:

**주입이 됐는가보다, 같은 역할의 객체가 같은 생명주기로 공유되는가가 더 중요할 때가 있다.**

---

## 이번 챕터에서 정리된 개념 구조

### 1. `PlayerInputComponent`

역할:

- Unity Input System 이벤트를 받는다
- 그 이벤트를 writer 호출로 변환한다

하면 안 되는 것:

- 이동 계산
- 점프 판정
- 상태 전이
- 애니메이션 재생

즉, 이 컴포넌트는 **입력 어댑터**다.

---

### 2. `ICharacterInputWriter`

역할:

- 입력 생산자가 버퍼에 값을 기록하는 계약

사용 주체:

- `PlayerInputComponent`
- 나중의 NPC AI
- 나중의 리플레이 시스템

핵심 개념:

**입력을 "생산"하는 쪽의 인터페이스**

---

### 3. `ICharacterInputSource`

역할:

- 게임플레이 시스템이 입력을 읽는 계약

사용 주체:

- `PlayerController`
- 나중의 `LocomotionStateMachine`
- 나중의 `ActionStateMachine`

핵심 개념:

**입력을 "소비"하는 쪽의 인터페이스**

---

### 4. `CharacterInputBuffer`

역할:

- 현재 입력 상태를 저장
- writer 호출을 받아서 갱신
- source를 통해 읽히게 함
- one-shot 입력을 consume 방식으로 소모

핵심 개념:

**입력의 단일 저장소**

이 버퍼는 지금 단계에서:

- continuous input의 현재 상태
- one-shot input의 임시 플래그

를 모두 갖고 있다.

---

### 5. `CharacterInputFrame`

역할:

- 현재 프레임 기준 입력 상태 표현

핵심 개념:

- 값 객체
- 매번 상태를 새로 만들어 교체하는 방식
- `WithMove`, `WithLook`, `WithJump` 같은 작은 갱신 함수 제공

이 구조를 쓴 이유는:

- 부분 갱신이 명확하다
- 부작용 추적이 쉽다
- 상태 변화가 눈에 잘 들어온다

---

## 이번 챕터에서 정리된 기준

이 기준은 다음 채팅과 다음 챕터에서도 유지한다.

### 기준 1

게임플레이 시스템은 Unity Input System을 직접 참조하지 않는다.

즉:

- `PlayerController`
- `LocomotionStateMachine`
- `ActionStateMachine`

은 `PlayerInputActions`를 직접 알면 안 된다.

---

### 기준 2

입력은 생산자와 소비자를 분리한다.

- 생산자: `ICharacterInputWriter`
- 소비자: `ICharacterInputSource`

---

### 기준 3

continuous input과 one-shot input을 같은 방식으로 다루지 않는다.

- continuous:
  - `Move`
  - `Look`
  - `SprintHeld`
- one-shot:
  - `JumpPressed`
  - `DodgePressed`
  - `InteractPressed`

---

### 기준 4

one-shot 입력은 consume 패턴을 기본으로 한다.

프레임 끝 일괄 초기화보다:

- 누가 소비했는지
- 언제 소비됐는지

가 명확한 구조를 우선한다.

---

### 기준 5

MonoBehaviour는 Unity 바깥 책임을 최소화한다.

즉 MonoBehaviour는:

- Input System 연결
- Scene/Transform 연결
- Unity lifecycle 대응

정도만 담당하고,

게임 규칙은 순수 로직 계층으로 보낸다.

---

### 기준 6

DI 문제를 볼 때는 null 여부만 보지 않는다.

반드시 같이 확인한다.

- 수명 주기
- 동일 인스턴스 공유 여부
- scope 경계
- Unity lifecycle과 DI lifecycle의 순서

---

## 이번 챕터에서 아직 완료되지 않은 것

이번 챕터는 입력 구조 정리까지다.

아직 안 한 것:

- `Jump` 실제 구현
- `Dodge` 실제 구현
- `Interact` 실제 구현
- `LocomotionStateMachine`
- `ActionStateMachine`
- `WeaponStateMachine`
- NPC 입력 공급자

즉, consume 메서드는 준비됐지만 아직 실제 gameplay transition에 연결되진 않았다.

이건 설계상 의도된 순서다.

---

## 다음 챕터로 넘어갈 때 할 일

다음 단계는 입력이 아니라 locomotion 책임 분리다.

권장 순서:

1. `PlayerController`에서 이동 책임 분리
2. `CharacterMotor` 추출
3. `GroundSensor` 추출
4. 카메라 회전 책임 분리
5. `LocomotionStateMachine` 도입
6. 그 다음 `JumpState`에서 `ConsumeJumpPressed()` 연결

중요:

점프 입력을 지금 당장 억지로 쓰는 게 아니라,
**점프 상태가 생길 때 consume 패턴을 연결하는 것**이 올바른 순서다.

---

## 다음 채팅에서도 같은 형식으로 작성할 기준

앞으로 Chapter 문서를 쓸 때는 아래 형식을 유지한다.

### 문서 형식 기준

1. **문서 목적**
   - 이번 챕터가 무엇을 다루는지

2. **시작 시점 문제 상황**
   - 작업 전 어떤 문제가 있었는지

3. **내가 실제로 했던 질문**
   - 설계 중 헷갈린 질문을 그대로 남김

4. **틀렸던 생각 / 막혔던 지점**
   - 어디서 잘못 가정했는지

5. **정리된 개념**
   - 질문을 어떤 개념으로 해결했는지

6. **최종 결론**
   - 이번 챕터에서 채택한 구조

7. **기준**
   - 다음 챕터에서도 유지해야 할 설계 원칙

8. **미완료 항목**
   - 아직 안 했지만 다음으로 넘기는 것

9. **다음 단계**
   - 바로 이어질 실제 작업

### 서술 기준

- 구현 코드 나열보다 개념과 판단 근거를 우선한다
- "무엇을 했는가"보다 "왜 그렇게 했는가"를 더 강조한다
- 내가 했던 질문과 오해를 숨기지 않고 기록한다
- 다음 채팅에서 읽었을 때 설계 맥락이 바로 복원되도록 쓴다
- 추상적으로 쓰지 말고 실제 문제와 연결해서 쓴다

---

## 최종 요약

이번 챕터의 핵심 학습은 세 가지다.

1. 입력 수집과 입력 소비는 분리해야 한다
2. one-shot 입력은 consume 패턴으로 다뤄야 한다
3. DI 문제는 "주입 여부"보다 "동일 인스턴스 공유와 생명주기"까지 봐야 한다

이 세 가지가 이후 locomotion, traversal, weapon, NPC 구조의 출발점이다.

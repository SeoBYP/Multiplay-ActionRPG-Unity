# 플레이어 애니메이션 — ARPGWarrior 배선 (현행 진실원)

> 상태: **✅ 구현 완료 (2026-08-20 ~ 08-21, P0~P16)** · 범위: **로컬 전용**(사다리·콤보 연출은 네트워크 동기 없음)
> 이전 문서 [player-animation-setup.md](player-animation-setup.md) 는 **PROTOFACTOR 시절 기록**이다(모델 교체로 무효).
> 관련: [character-architecture.md](character-architecture.md) · [gas-architecture.md](gas-architecture.md) · [codemap.md](codemap.md) §2.104·P8~P16
> 이 작업의 **설계 판단·시행착오 회고** = 포트폴리오 [챕터 28](../portfolio/chapter-28-animation-retarget-units-ik.md)
> 원격(멀티플레이) 동기화 회고 = [챕터 29](../portfolio/chapter-29-multiplayer-sync-invisible-failures.md)

## 0. 한 줄 요약

플레이어 애니메이터를 **코드로 생성**하고(`PlayerAnimatorControllerBuilder`), 블렌드 좌표를 **클립 실측 속도(m/s)** 로 두어
발 슬라이딩을 없앴다. 공격은 4단 콤보 + 루트모션, 사다리는 로컬 전용 Locomotion 상태 + 최소개입 IK 다.

---

## 1. 컴포넌트 배치 (누가 누구를 호출하는가)

```
[Editor 도구 — 에셋 생성]                        [런타임]
PlayerAnimatorControllerBuilder                  PlayerCharacterAgent (로컬 드라이버)
   │ 생성                                            │ 입력 폴링(ConsumeAttackPressed 등)
   ▼                                                 ├─▶ ComboDriver ─────▶ FireSkill(skillId, step)
PlayerController_ARPG.controller                     │                         │
   │ 배선(Wire)                                      │                         ▼
   ├─▶ PlayerCharacter.prefab                        │        CharacterAgentAnimations (파라미터 어댑터)
   └─▶ RemotePlayerCharacter.prefab                  │                         │ SetFloat/SetBool/SetTrigger/SetInt
                                                     │                         ▼
ARPGWarriorClipSetup ──▶ 클립 Humanoid 재임포트      │                     Animator (Base Layer, IK Pass ON)
MonsterWalkSpeedSetup ─▶ 몬스터 배속 파라미터 배선   │                    ／        ＼
                                                     │        RootMotionRelay        LadderIK
                                                     │        (태그 있을 때만        (사다리 부착 중에만
                                                     │         deltaPosition 적용)     닿는 팔다리 보정)
                                                     │
                                    LocomotionStateMachine (Ground/Jump/Fall/Land/Climb)
                                                     │
                                       InteractionDetector ──▶ InteractionPromptNotifier ──▶ InGameModel ──▶ GameHud
```

**의존 방향**: Gameplay 는 System 의 notifier 로 **밀어 넣기만** 한다(GUI 를 직접 알지 않는다 — MVI 규칙).

---

## 2. 애니메이터 계약 (`PlayerController_ARPG.controller`)

**생성 전용**이다. 손으로 고치지 말고 `Tools/ARPGWarrior/플레이어 컨트롤러 생성 + 프리팹 배선` 을 다시 돌린다
(빌더가 매번 `DeleteAsset` 후 처음부터 만든다 — 부분 갱신은 잔여 상태가 남아 재현이 깨진다).

| 파라미터 | 타입 | 쓰는 곳 |
|---|---|---|
| `Speed` | Float | 1D 로코모션(비스트레이프) |
| `MoveX` / `MoveY` | Float | 2D 스트레이프 — **단위 m/s** |
| `Strafe` / `Grounded` / `Climbing` | Bool | 상태 분기 |
| `ComboStep` | Int | 콤보 단계(0~3) |
| `DodgeX` / `DodgeY` | Float | 회피 8방향 |
| `ClimbSpeed` | Float | 사다리 클립 **배속**(음수 = 역재생 = 하강) |
| `Jump` `Fall` `Land` `Attack` `Interact` `Dead` `Dodge` `Revive` `Hit` | Trigger | 이벤트 |

**상태 15개**: `Locomotion`(1D) · `StrafeLocomotion`(2D, 18모션) · `Jump` `Airborne` `Landing` ·
`ComboA~D` · `Dodge`(8방향) · `Climb` · `Hit` · `Interact` · `Dead` · `GetUp`.

Base Layer 는 **`iKPass = true`** — 이게 꺼져 있으면 `OnAnimatorIK` 가 아예 호출되지 않는다(사다리 IK 전제).

### 2-1. `Locomotion ↔ StrafeLocomotion` 은 **즉시 전환**(duration 0) — 건드리지 말 것

블렌드 시간을 주면 두 가지가 동시에 깨진다(둘 다 실측으로 겪었다):

| 넣은 것 | 무슨 일이 났나 |
|---|---|
| `duration 0.15` | 블렌드 창 동안 Unity 가 **AnyState 전이를 평가하지 않는다** → 그 사이 들어온 Dodge/Attack/Jump 트리거가 조용히 사라진다 |
| `+ interruptionSource` (위를 풀려고) | 조건(`Strafe=true`)이 계속 참이라 **전이가 매 프레임 자기 자신으로 재시작** → StrafeLocomotion 에 영영 도달 못 함 = **Walk/Run 이 아예 안 나옴**(실측: 40프레임 뒤 `normalizedTime 0.03` 고정) |

`Strafe` 는 스폰 직후 한 번 켜지고 그대로 유지되며(로컬 `GroundState`·원격 `RemoteDriver`) 두 트리의 중심이 같은 Idle 이라,
즉시 전환이 시각적으로 안전하다. 이 계약은 `PlayerAnimatorContractTests.스트레이프_전이는_즉시여야_한다` 가 지킨다.

---

## 3. 발 슬라이딩을 없앤 방법 — 블렌드 좌표 = 실측 속도

**문제**: 블렌드 좌표를 0~1 정규화나 "코드가 원하는 속도"로 두면, 블렌드된 **발 속도**와 실제 **이동 속도**가 어긋난다.

```
            [예전 — 미끄러짐]                       [지금 — 정합]
 좌표: Walk 2.0 / Run 4.0 (코드 속도)      좌표: Walk 2.26 / Run 3.31 (클립 averageSpeed 실측)
 실제 클립 속도: 2.26 / 3.31                   ⇒ 파라미터에 m/s 를 그대로 넣으면
 ⇒ 2.0 을 넣으면 2.26 짜리 클립이 나옴          블렌드 결과 발 속도 == 이동 속도
 ⇒ 발이 13% 헛돎                               ⇒ 실측 비율 1.00
```

- 좌표는 `AnimationClip.averageSpeed` 로 **측정**해 상수로 저작한다(`PlayerAnimatorControllerBuilder` 상단).
- **Sprint 만 예외**: 게임 속도 5.335 인데 클립은 3.44 라 좌표로는 못 맞춘다 → `ChildMotion.timeScale ≈ 1.55` 로 배속.
- `LocomotionSettings.MoveSpeed = 2.3` 도 같은 이유로 클립 실측(2.26~2.32)에 맞춘 값이다.
- 방향 전환(좌 → 우)이 툭 끊기던 문제는 `MoveBlendDamp = 0.12` 의 **damped SetFloat** 로 해결(프레임당 변화 4.6 → 0.216).

### 3-1. 몬스터는 왜 다른 방식인가 (절충안 C)

몬스터 보행 클립은 전부 **제자리(in-place)** 라 `averageSpeed` 가 0 이다 → 좌표 기법을 못 쓴다.
대신 **배속 보정 + 초과분은 이동 속도를 낮춘다**:

```
LocomotionSpeedMatch.Multiplier(실제속도, 클립속도, min 0.6, max 2.0)
   → CharacterAgentAnimations.SetFloat(MoveSpeedMul) → AnimatorState.speedParameter
   → 배속 상한(2.0)을 넘는 몬스터는 이동 속도 자체를 줄여 맞춘다
```

클립 속도는 **발 본의 후방 이동 속도**로 측정해 `MonsterWalkSpeedSetup.ClipSpeeds` 에 저작한다
(리그마다 본 이름이 달라 자동 측정은 오측정이 그대로 데이터가 된다 — 실제로 리바이어던이 10m/s 로 잘못 나왔다).
**미측정 몬스터는 0 = 무보정**이 안전하다(gargoyle · leviathan).

---

## 4. 공격 = Action 축 (FSM 아님, CA-1)

```
좌클릭 ─▶ ComboDriver.OnAttackPressed(now)        [접수 — 선입력 버퍼]
                    │
                    ▼  매 프레임
           ComboDriver.TryFire(now)                [발동 판정]
                    │  직전 스윙 + ComboChainMs 경과?
                    ├─ 아직 ─▶ 버퍼 유지(= 선입력이 그 시점에 자동 발동)
                    ├─ 창(ComboWindowMs) 초과 ─▶ 단계 리셋 → A 부터
                    └─ 발동 ─▶ FireSkill(skillId, step)
                                   ├─▶ SetInt(ComboStep, step) + SetTrigger(Attack)
                                   └─▶ GAS/서버(cadence 권위 게이트는 같은 데이터를 본다)
                                            │
                        Animation Event(타격 프레임) ─▶ HitDetector ─▶ ASC ─▶ GameplayEffect
```

- **타이밍의 진실원 = 스킬 데이터**(`SkillTimeline.ComboChainMs/ComboWindowMs`, SO → bake → json). 서버도 같은 값을 쓴다.
- **왜 애니메이터 `hasExitTime` 을 안 쓰나**: 체인 전이에 exit time 을 걸면 Unity 가 `Attack` **트리거를 소실**한다(실측).
  그래서 전이는 `hasExitTime=false`(코드가 쏘는 순간 전이)로 두고 **언제 쏠지**를 데이터가 정한다.
- **마무리 타 중 입력은 버린다** — 마지막 단계 뒤엔 이어질 타가 없어 새 콤보가 시작되므로, 버퍼하면
  손을 뗐는데도 Idle 직후 한 대가 더 나간다(사용자 피드백으로 발견).
- **루트모션**: `ComboA~D` 상태에만 `tag = "RootMotion"` 을 달고, `RootMotionRelay` 가 그 태그일 때만
  `animator.deltaPosition` 을 적용한다. 태그 게이팅이 없으면 로코모션까지 루트모션에 끌려간다.

---

## 5. 사다리 (P6·P13~P16) — 로컬 전용

**두 축 원칙**: 사다리는 **Locomotion 축의 배타적 이동 모드**다(Action 이 아니다). 중력·수평 이동을 끄고 수직으로만 움직인다.

```
[탐지]   InteractionDetector(최근접 IInteractable) ─▶ InteractionPromptNotifier ─▶ GameHud "[E] 사다리 오르기"
[부착]   E ─▶ Ladder.Interact() ─▶ ClimbSensor.RequestAttach ─▶ GroundToClimbTransition ─▶ ClimbState.Enter
                                                                    └▶ GetAttachPose 로 면(face) 스냅
[이동]   Move.y ─▶ MoveRaw(up * axis * ClimbSpeed) + SetFloat(ClimbSpeed, axis * ClimbSpeed/ClipSpeed)
[이탈]   ① 상단 도달 ─▶ GetTopExitPosition(레이캐스트로 실제 바닥) 텔레포트 ─▶ Ground
         ② 바닥 0.6m 이내 + 아래 입력 ─▶ 즉시 Idle 복귀(최하단까지 안 내려가도 됨)
         ③ Space ─▶ RequestJumpOff ─▶ 반대쪽으로 0.7m 밀어내고 ─▶ Fall
```

### 5-1. 방향 함정 세 가지 (전부 실제로 겪음)

| 증상 | 원인 | 해결 |
|---|---|---|
| 트리거가 아예 안 잡힘 | 콜라이더를 **월드 바운드**로 만들어 FBX 의 −90° 회전·스케일 100 을 무시 → 납작한 판(0.06m)이 됨 | 로컬 공간 8코너로 계산, 패딩도 world→local 변환 |
| 옆에서 다가가면 측면 기둥에 매달림 | 접근 **방향 그대로** 부착 | `GetFaceAxes()` 로 **면 법선에 스냅** |
| 한쪽 면에선 좌우가 반대 | 좌우를 **사다리 고정 축**으로 계산 | 그립이 **캐릭터의 `right`** 를 받아 판정 |

`GetFaceAxes` 는 콜라이더의 **월드 두께**로 축을 정한다 — 얇은 수평축 = 면 법선, 넓은 쪽 = 폭. FBX 회전에 무관하게 동작한다.

### 5-2. IK — "애니를 살리고 어긋난 축만 고친다"

```
클립이 만든 손 위치(GetIKPosition)
        │ 사다리 좌표계로 분해
        ├── 좌우(팔 간격)  ──▶ 클립 값 그대로 유지   ← 덮어쓰면 어깨너비 간격이 무너진다
        ├── 깊이(면까지)   ──▶ 보정 (손 0.06 / 발 0.12)
        └── 높이           ──▶ 가장 가까운 발판으로 스냅 (발판 간격 0.6m)
                                        │
                        거리 판정: |클립위치 − 목표| < 0.28m 일 때만 가중치 ↑
                        (뻗는 중·당기는 중 = 0 → 클립 그대로 보인다)
```

- 계산은 순수 함수 `LadderIK.ResolveGrip` / `ContactWeight` 로 분리해 테스트로 고정했다.
- **왜 거리 판정인가**: 이전엔 속도비(붙잡은 팔다리는 월드에서 정지)로 판정했는데, 요구가
  "팔을 뻗어 **닿을 때만**" 이라 거리로 바꾸는 게 요구와 1:1이다.

---

## 5-3. 원격 플레이어 동기화 (던전, P17)

**무엇을 보내고 무엇을 역산하는가** — 방향은 이미 흐르는 정보라 보내지 않는다.

```
[보내지 않는다]  8방향 MoveX/MoveY
      위치(→속도) + RotY(→facing) 로 로컬 GroundState 와 같은 공식으로 복원
      RemoteLocomotion.ToFacingFrame(worldVelocity, rotY) — 단위 m/s 유지

[보낸다]         C_Move/S_Move + byte AnimState  (= StateKind)
      0=Ground 1=Jump 2=Fall 3=Land 4=Climb
      점프·낙하·사다리는 전부 "y 가 변한다"라 위치만으로는 구분 불가

[보낸다]         C_Dodge/S_Dodge + DirX/DirY (캐릭터 기준)
      1회성 신호라 상시 스트림이 아니라 이벤트에 싣는다

[서버]           해석하지 않고 불투명 byte 릴레이 (연출은 클라 권위)
      MovementHandler.BuildBroadcast — 서버에 enum 을 두면 진실원이 둘이 된다
```

원격 재생 규칙:
- **트리거는 상태가 바뀌는 순간에만** 쏜다 — 이동 패킷마다 쏘면 Jump/Land 가 매 프레임 리셋돼 제자리에서 떤다.
- **사다리 배속은 목표 y 의 변화**로 만든다 — 보간된 실제 y 로 재면 lerp 지연만큼 늘 작게 나온다. 상승 +, 하강 −(역재생).
- 사망/부활은 `S_PlayerDead`/`S_PlayerRevived` 이벤트로만 — 그 이벤트가 안 오면 원격은 계속 서 있다(§8 함정 6).

> ⚠ `RemotePlayerCharacter.prefab` 의 파라미터명이 비면 `CharacterAgentAnimations` 가 **조용히 스킵**한다.
> 실제로 `MoveX/MoveY/Strafe/Jump/Fall/Land` 6개가 비어 있어 원격이 늘 전진 클립만 재생했다(에러 0건).

---

## 6. 상호작용 프롬프트 UI

```
InteractionDetector (Gameplay)
   └─▶ InteractionPromptNotifier (System.Player)   ← 밀어넣기만. GUI 를 모른다
          └─▶ InGameModel.OnInteractionPrompt (R3 Subject, Presentation)
                 └─▶ GameHud.RenderInteractionPrompt  →  "[E] 사다리 오르기"
```

키 라벨은 하드코딩하지 않고 입력 바인딩에서 온다(키 리매핑 시 자동 반영).

---

## 7. 재현 방법 (에셋을 다시 만들 때)

```
Tools/ARPGWarrior/클립 Humanoid 재임포트                 # ARPGWarriorClipSetup
Tools/ARPGWarrior/플레이어 컨트롤러 생성 + 프리팹 배선   # PlayerAnimatorControllerBuilder
Tools/Monster/발 슬라이딩 보정 배선                      # MonsterWalkSpeedSetup
```

> `Wire()` 는 프리팹 재생성 후 `AssetDatabase.ImportAsset(ForceUpdate|ForceSynchronousImport)` 를 반드시 부른다.
> 없으면 디스크 YAML 은 맞는데 런타임 컨트롤러가 null 이 되어 `Animator is not playing an AnimatorController` 가 쏟아진다(실측 193건).

---

## 8. 함정 모음 (재발 방지)

1. **Generic 클립은 Humanoid 아바타에 안 붙는다.** 실측 뼈 회전 0.0도, 빈 휴머노이드 포즈가 `root` 를 −1.06m 로 밀어 캐릭터가 파묻힌다.
2. **enum 은 항상 끝에 추가한다.** `AnimationTriggerType` 중간에 넣으면 SO 에 int 로 직렬화된 기존 값이 밀린다(보스 슬램 큐가 실제로 깨졌다).
3. **`Game.*` 네임스페이스 안에서 `System.Array` 는 `Game.System` 으로 해석된다**(CS0234). `using System;` 후 비수식 이름을 쓴다.
4. **VContainer 는 C# 기본 인자를 채워주지 않는다.** 생성자에 파라미터를 추가하면 테스트 컨테이너에도 등록해야 한다.
5. **클라 컴파일 판정은 Unity 콘솔로만** 한다(`dotnet build Client/*.csproj` 금지 — CLAUDE.md 참조).
6. **파라미터를 넣었다 ≠ 재생됐다.** 애니 테스트가 파라미터 값만 검사하면, 상태 머신이 그 값을 소비하지 못해
   화면에 아무것도 안 나와도 전부 통과한다(실측: 213개가 Walk/Run 미재생을 통과시켰다).
   → 상태 도달(`IsName`)과 `normalizedTime` 진행까지 단언한다(`PlayerLocomotionAnimTests`).
7. **테스트에서 캐릭터 프리팹을 통째로 Instantiate 하지 않는다.** DI 없는 컴포넌트가 NRE 를 던지고 그 로그가
   테스트를 실패시켜 **무관한 실패가 진짜 신호를 덮는다**. 보려는 것만 좁혀서 만든다(컨트롤러 단독 등).

---

## 9. 검증 현황

| 항목 | 결과 |
|---|---|
| 컴파일 | 에러 0 |
| EditMode | **213/213** |
| PlayMode | **202/202** (249.1s) |
| 발 슬라이딩 비율 | 플레이어 6케이스 **1.00** · 몬스터 6종 **1.00** |
| 사다리 | `ClimbTests` **10/10** |
| 원격 동기화 | `RemoteLocomotionSyncTests` **9/9** · `RemoteDriverAnimTests` **3/3** |
| 로코모션 재생 | `PlayerLocomotionAnimTests` **2/2** (고장 주입 시 2/2 실패 확인) |

**미실측(눈으로 맞출 값)**: `m_handGripDepth` 0.06 · `m_footGripDepth` 0.12 · `m_contactRadius` 0.28 ·
IK 가중치 0.8 · 몬스터 2종(gargoyle · leviathan) 클립 속도.

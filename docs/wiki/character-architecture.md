# Character 관리 아키텍처 (설계 기준)

> 멀티플레이 액션 RPG의 캐릭터(로컬/원격/몬스터) 관리·전투·애니메이션 구조의 **합의된 설계 방향**.
> 구현 진행 상황은 [plan.md](plan.md), 결정 요약은 [codemap.md](codemap.md) 참조.
> (작성 시점: 방향 확정. 코드는 점진 리팩터로 적용.)

---

## 1. 진단 — 왜 바꾸나

기존 `Gameplay/Character`는 **하나의 FSM에 두 축을 혼용**했다.
- Locomotion 축(`Ground/Jump/Fall/Land`) — 배타적 이동 모드
- Action 축(`Attack/Interact`) — 발동형 행동

부작용:
- 전이 조합 폭발(`ITransitionRule` 10개 + `StateMachineBuilder` switch)
- "이동 중 공격" 같은 동시성 표현 불가(상태가 배타적)
- 공격 로직이 `AttackState`(FSM)와 `BasicAttackAbility`(GAS) **양쪽에 이중화**

→ "상태머신이 아닌 것 같은 느낌"의 정확한 원인 = **축 혼용**.

---

## 2. 핵심 원칙: 두 축 분리

| 축 | 담당 | 비고 |
|---|---|---|
| **Locomotion** | **작은 FSM 유지** (Grounded/Air, 필요시 Land) | 배타적 이동 모드 → FSM이 정답. 전이 ~4개 |
| **Action**(공격/상호작용/스킬) | **GAS로 일원화** | 이동과 직교·발동형. `AttackState`/`InteractState`+전이 제거 |

- 이동 제약(루트/감속)은 **GameplayEffect/태그**로 표현(상태 전이 아님).
- 데미지·HP는 **GameplayEffect**로, 서버 권위.

---

## 3. Character = 합성 + 교체 가능한 Driver

```
Character (합성)
├─ Visual/Animation        ← View 레이어 (로컬·원격 공통)
├─ Locomotion FSM + Motor  ← 로컬 시뮬
├─ AbilitySystem(GAS)      ← 로컬 예측 / 서버 권위
└─ Driver (DI 주입 교체)
     • LocalInputDriver  : 입력 → FSM/GAS/Motor 시뮬
     • NetworkDriver     : 네트워크 스냅샷 → transform 보간 + 애니 파라미터
```

- **로컬/원격 구분 = Driver 주입**. `CharacterSpawner`가 타입(Playable/Network/Monster)에 따라 결정.
- 원격 캐릭터는 FSM/Motor/GAS를 **로컬 시뮬하지 않는다** — 서버가 보낸 상태를 재생만(desync 방지).

---

## 4. Animation = "관찰하는 View" (게임플레이를 구동하지 않음)

| 애니 | 구동원 | 방식 |
|---|---|---|
| Locomotion | Motor 속도·grounded | **MotionMatching** (이동 파라미터) |
| Action | **GAS Ability** | 트리거 클립(상체 오버레이 또는 풀바디 일시 대체) |

- Ability는 Animator를 직접 만지지 않고 View의 `PlayAction(id)`만 호출(게임플레이↔표현 분리).
- 타격 프레임은 Animation Event(클라 cue) → `CharacterHitEventReceiver`.
- **로컬·원격 동일 View** — 파라미터/트리거의 *출처*만 다름(내 시뮬 vs 네트워크). 멀티 통일의 핵심.
- MotionMatching = Locomotion 전담. Action은 그 위 오버레이/일시 대체(블렌딩은 애니 레이어 내부 결정).

---

## 5. 전투: Hit 판정 vs Hit Stop (소유권이 정반대)

| | 소유권 | 메커니즘 |
|---|---|---|
| **Hit 판정 / 데미지** | 게임플레이 · **서버 권위** | active window(데이터) 동안 hitbox 평가 → GameplayEffect |
| **Hit Stop · VFX · SFX · 셰이크** | **클라 로컬 연출** | 히트 확정 이벤트 반응. **per-actor(Animator.speed)** — 전역 `Time.timeScale` 금지 |

- 공격 생명주기: `CanActivate(쿨다운/코스트)` → `Activate(애니+VFX)` → `active window 판정` → `GameplayEffect(데미지)` → `종료/쿨다운`.
- **판정 타이밍은 애니가 아니라 데이터(active window)** — 서버가 애니 없이 평가 가능.

---

## 6. 스킬 데이터 + 공유 결정론 코어

서버도 GAS로 돌고, 데이터·로직을 클라/서버 공통으로 두기 위한 구조.

```
Shared.Gameplay  (netstandard2.1, UnityEngine 의존 0)   ← 단일 진실원
  ├─ SkillTimeline 스키마 (startup/active/recovery, hitbox, cue 리스트)
  └─ GAS 결정론 코어 (어빌리티 스테핑·쿨다운·Attribute 수학·hitbox 겹침)

Unity 클라  ── DLL 참조 ──▶ Shared.Gameplay  + Presentation(애니/VFX/HitStop)
서버        ── 프로젝트 참조 ─▶ Shared.Gameplay  + Authority(검증/브로드캐스트/HP)
```

- Unity는 netstandard DLL 참조 가능(이미 `R3.dll`이 그 방식) → **codegen 드리프트 없이 같은 어셈블리 공유**.
- **스킬 데이터**: **저작 진실원 = Client SO**(편집 쉬움·클라 프리뷰), **서버 검증용 = bake JSON**(엔진 비종속, 서버가 읽음). SO→export 로 배포 = 데이터 진실원 교리 [gas-architecture.md §2.5](gas-architecture.md)와 동일. (런타임/서버는 SO를 직접 진실로 삼지 않고 bake JSON 을 읽는다 — 서버가 SO 못 읽음.)
- **저작 = Unity Editor "Skill Timeline" 툴** → SO 편집 → 공유 JSON export(클라·서버 합류).
- 같은 코어 + 같은 데이터 → **같은 입력 = 같은 판정**(클라 예측 ↔ 서버 권위 reconcile).

### 결정론 수준 (과설계 경계)
- **Co-op PVE** = **서버 권위 + 클라 예측(reconcile)** 로 충분.
- 비트정확 락스텝·fixed-point는 **경쟁 PvP 전용** → 지금 불필요. ("결정론" = 양쪽이 같은 코어·데이터로 같은 판정, 부동소수 비트 일치까지는 X.)

---

## 7. 안 하는 것 (과설계 차단 — 최우선 원칙 #1)

- ❌ ECS/DOTS (캐릭터 소수: 파티 2~4 + 몬스터)
- ❌ Unity Timeline(PlayableDirector)로 전투 구동 (컷씬용, netcode 비친화)
- ❌ ScriptableObject를 스킬 데이터 진실원으로 (서버 비호환)
- ❌ fixed-point/롤백 netcode (지금 Co-op엔 과함)
- ❌ 처음부터 재작성 (Motor·Input·GAS·MotionMatching 재사용)

---

## 8. 점진 적용 순서

1. **Attack/Interact → GAS 이관** (`AttackState`/`InteractState`+전이 제거), Locomotion FSM 슬림화
2. **`Shared.Gameplay`** 스키마 + GAS 코어 골격 (Unity DLL 참조 배선)
3. **BasicAttack 1개 end-to-end** (같은 JSON: 클라 예측 + 서버 권위 판정/데미지)
4. **Character 합성 + Driver(Local/Network)** → `CharacterSpawner` 연동
5. (스킬 증가 시) **Skill Timeline 에디터 툴**
6. 각 단계 **단위 테스트 동반** (현재 State 테스트 없음 → 회귀 위험)

> 원칙: **데이터 스키마·공유 코어를 먼저 굳히고, 저작 툴은 그 위에 얹는다.**

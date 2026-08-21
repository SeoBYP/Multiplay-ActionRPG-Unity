# 플레이어 애니메이션 배선 — 설계·작업 계획

> ## ⛔ 2026-08-20: 아래 PROTOFACTOR 내용은 **낡았다**(모델 교체로 무효)
> 이 문서가 전제한 "플레이어 메시 = `SK_Protof-Actor`(Generic), 프로젝트에 Humanoid Avatar 0개"는 **더 이상 사실이 아니다.**
> 현재 메시는 `SK_HornedKnight_M_02`(**Humanoid** 아바타)이고, PROTOFACTOR 클립 22개는 전부 **Generic** 이라 이 조합에서는
> 하나도 바인딩되지 않는다(실측 뼈 회전 0.0도). 게다가 빈 휴머노이드 포즈가 `root` 본을 -1.06m 로 밀어 캐릭터가 파묻힌다.
> **현행 진실원 = [player-animation-arpg.md](player-animation-arpg.md)** (ARPGWarrior 배선 전체) · 결정 로그 `docs/wiki/codemap.md` §2.104,
> 생성 스크립트 `Client/Assets/Script/Gameplay/Editor/PlayerAnimatorControllerBuilder.cs`.
> 아래 본문은 **당시 기록으로만** 남긴다(왜 그렇게 했는지의 이력).


> 상태: **✅ 구현 완료 (2026-07-09)** — 로컬 플레이어 MVP. 검증: SampleAnimation 재생확인 + 프리팹 배선 + 스크린샷 + EditMode 152/152.
> 범위: 로컬 플레이어 MVP (`PlayerCharacter.prefab` + `PlayerController.controller`)
> 관련: [character-architecture.md](character-architecture.md) · [gas-architecture.md](gas-architecture.md) · [.claude/rules/unity-gameplay-state.md](../../.claude/rules/unity-gameplay-state.md)

## ✅ 완료 요약 (2026-07-09)

- **P0-1** `PlayerController.controller` 모션 6슬롯 재지정(1hMelee in-place) + 고아 `MM_*` 4개 삭제. 파라미터 9개, NULL 모션 0, 고아 0.
- **P0-2** `PlayerCharacter.prefab` 자식으로 `SK_Protof-Actor` + Animator(controller=PlayerController, avatar=null Generic, rootMotion=false) 추가, 캡슐 시각 MeshRenderer 비활성.
- **P1** 트리거 `Dead`/`Dodge` 파라미터·상태·전이 추가(Dead=AnyState 홀드, Dodge=AnyState→exit). 프리팹 `m_animationDeathTrigger="Dead"`, `m_animationDodgeTrigger="Dodge"`.
- **검증**: `AttackA` 클립이 팔 본 38.6° 이동(avatar=null 경로매칭 재생 OK) · Animator를 `GetComponentInChildren`가 잡음 · 실물 스크린샷(휴머노이드 공격 포즈) · EditMode 152/152 그린.

## ✅ 2차 패스 (2026-07-09) — LoopTime · 무기 프롭 · 히트박스

- **LoopTime**: 순환 클립 `Idle/Walk/Run/Falling1hMelee` 4개 loopTime ON(멈칫 제거). 원샷(Jump/Land/Attack/Draw/Death/Dodge) 6개는 OFF 유지(Death=마지막 프레임 홀드).
- **무기 프롭**: `SM_BludgeonProp`(둔기)를 `SK_Protof-Actor/…/humanoid_ R Hand/WeaponProp`에 부착. `localPos=0/localRot=identity`(손 본 로컬 공간 정합, 애니메이션 시 손 추종). Idle·Attack 스크린샷 확인.
- **히트박스 무기 리치 저작(A안)**: `Skill_BasicSwing`/`Skill_HeavySwing` SO의 hitboxOffset/HalfExtents를 무기 궤적(측정: y 0.4~1.9)에 맞춰 수직 확장 → `SkillCatalogExporter.BakeAll` → `skills.json`(서버·클라 공유 단일소스). **XZ 정면 리치는 불변**(판정 밸런스·테스트 안전).
  - basic_swing: offset (0,0,1.0)→(0,0.5,1.0), half (0.6,0.6,0.7)→(0.6,1.0,0.7)
  - heavy_swing: offset (0,0,1.3)→(0,0.5,1.2), half (1.1,0.8,1.0)→(1.1,1.1,1.0)
  - **왜**: 무기는 몸통~머리 높이로 내려찍는데 기존 박스는 발 높이(y[-0.6,0.6])라 무기와 수직 불일치 → 무기 궤적 커버로 "무기 기준 판정" 실현. 서버 권위·단일소스 유지(옵션 B/C 대신 A).
  - **검증**: skills.json 파일 확인 · 서버 전투 테스트 `SocketServer.Tests/Combat` **28/28** · 에디터측 `HitboxMath.Overlaps` 재검증(E2E/정면 HIT·뒤/먼 MISS·**무기높이(0,1.3,1) HIT**=개선 실증) · 클라 EditMode **152/152**. ⚠️ 던전 전 경로 Docker E2E는 서버 이미지 리빌드 시 확인 예정.
  - ⚠️ 교훈: bake는 다이얼로그 뜨는 메뉴(`Export()`)가 아니라 `BakeAll()`을 직접 호출(ExecuteMenuItem이 모달로 Unity 메인스레드 프리즈).

### ⚠️ 남은 갭 / 다음 작업 (발견 보고)

1. **부활 복귀 애니 미배선(실질 버그)** — `Revive`/`ReviveInPlace`는 `ResetTrigger(Dead)`만 부르고 **양성 부활 신호가 없어**, 부활해도 Animator가 Dead 홀드에 갇힌다. 해결: 컨트랙트에 `Alive`(또는 `Revive`) 트리거 추가 → 부활 시 발화 + `Dead→Locomotion` 전이. (코드 소량 변경 필요 → 이번 스코프 밖)
2. **무기 프롭 미부착** — Protof-Actor는 맨몸 메시. 1hMelee 스윙에 검이 안 보인다. `SM_BludgeonProp.fbx` 등을 손 본에 부착하는 별도 작업 필요.
3. **Interact = `DrawWeapon1hMelee` 플레이스홀더** — 전용 줍기 클립 없어 임시.
4. **RemotePlayer/NPC 미적용** — `RemotePlayerCharacter.prefab`, `NPCController.controller`는 다음 패스.
5. **Attack 콤보 A→B→C** — 콤보 카운터/입력버퍼/active-window 코드 신설 필요, 별도 패스.

## 1. 배경 — 왜 이 작업인가

"지금까지 작업한 것들"의 애니메이션을 Unity MCP + PROTOFACTOR *Ultimate Animation Collection* 으로 설정하려 한다.
조사 결과 **코드는 다 있는데 에셋/프리팹이 비어서 현재 캐릭터 애니메이션이 하나도 재생되지 않는 상태**였다.

| 계층 | 상태 |
|------|------|
| `CharacterAgentAnimations` (계약) | ✅ Speed/Grounded/Jump/Fall/Land/Interact/Attack/Dead/Dodge 전부 정의 |
| `DodgeDriver` / `PlayerCharacterAgent`(HP≤0) / `LocalCombat` | ✅ `SetTrigger(Dodge)`·`SetTrigger(Dead)`·공격 판정 모두 호출 중 |
| `PlayerCharacter.prefab` | ❌ **회색 캡슐뿐 — 리깅 모델도 Animator도 없음** |
| `PlayerController.controller` | ❌ Idle/Walk/Run 블렌드 클립 참조 **깨짐**(guid 미해결) + 고아 `MM_*` 상태 4개 |
| 프리팹 `m_animationDeathTrigger`/`m_animationDodgeTrigger` | ❌ **빈 문자열** |

→ `CharacterAgentAnimations.Awake` 의 `GetComponentInChildren<Animator>()` 가 **null** 이라 모든 `SetTrigger/SetFloat` 가 no-op.
**Speed·Attack 을 포함해 지금 아무 애니메이션도 안 나온다** (사망/회피만의 문제가 아님).

### 에셋 현실 (좋은 소식)

- 베이스 캐릭터 `SK_Protof-Actor.fbx` (SkinnedMesh + Generic 스켈레톤) 와 6천여 개 `Humanoid@*.fbx` 클립이 **동일 스켈레톤**.
- 프로젝트에 Humanoid Avatar 에셋 **0개** = **전부 Generic**. 리타겟/아바타 셋업 불필요 — 클립을 모델에 바로 물리면 재생된다.
- 이동은 코드(`CharacterMotor.Move`)가 하고 루트모션 미사용(`OnAnimatorMove`/`applyRootMotion` 없음) → **모든 클립은 `_RM` 없는 in-place 버전**을 쓴다.

### 의도한 결과

로컬 플레이어가 실제 3D 캐릭터로 **Idle/Walk/Run · Jump/Fall/Land · Interact · Attack · Dead · Dodge** 애니메이션이 보이게 만든다.

### 결정된 범위

| 항목 | 결정 |
|------|------|
| Primary 애님셋(무기 정체성) | **1Handed Melee** (Dodge만 Combat Bare Fists 차용 — 같은 스켈레톤) |
| 이번 패스 범위 | **로컬 플레이어만** (RemotePlayer/NPC는 다음) |
| Attack 콤보(A→B→C) | **이번 제외** (코드 신설 필요 → 별도 패스) |

## 2. 목표 컴포넌트 배치

```
PlayerCharacter.prefab (root: CharacterAgentAnimations, Motor, Agent, CharacterController…)
 ├── CameraFollowTarget (그대로)
 ├── Capsule (기존 — 물리/콜라이더면 유지, 시각 MeshRenderer만 비활성)
 └── SK_Protof-Actor (신규 자식: SkinnedMesh + Animator)   ← GetComponentInChildren<Animator>() 가 이걸 잡음
        Animator.controller = PlayerController.controller
        Animator.avatar     = SK_Protof-Actor 의 Generic Avatar

PlayerController.controller (repair-in-place)
  params: Speed(f) Grounded(b) Jump Fall Land Interact Attack  +  [신규] Dead, Dodge
  Base Layer
   ├── Locomotion(sub-SM): BlendTree Speed 0/2/6 = Idle / WalkForwardCombat / RunForward (1hMelee)
   │        + JumpToApex → Falling → LandingMedium
   ├── Interact 상태
   ├── RightHand1Combat 상태 (Attack → AttackA1hMelee)
   ├── [신규] Dodge 상태 (Dodge 트리거 → DodgeForwardCombat, in-place) → Exit
   ├── [신규] Dead 상태 (AnyState → Dead 트리거 → DeathFront1hMelee, HasExitTime=false, 마지막 프레임 홀드)
   └── [삭제] MM_WalkUTurnLeft/Right, MM_RunUTurnLeft/Right  (고아 4개)
```

**왜 repair-in-place 인가**: 기존 컨트롤러의 파라미터 세트·전이 그래프(튜닝된 ExitTime/Duration)는 살아있고 클립 참조만 깨졌다.
새로 만드는 대신 모션 재지정 + 고아 삭제 + Dead/Dodge 추가가 최소 변경(간결성 원칙). MM_ 정리는 이 단계에서 흡수된다.

## 3. 작업 항목

### Phase 0 — 기반 (선행 필수)

**P0-1. 컨트롤러 모션 재지정** — `PlayerController.controller` 각 상태의 깨진 `m_Motion` 을 1hMelee **non-`_RM`** 클립으로 교체:

| 슬롯 | 클립 |
|------|------|
| Blend Speed 0 / 2 / 6 | `Humanoid@Idle1hMelee` / `Humanoid@WalkForwardCombat1hMelee` / `Humanoid@RunForward1hMelee` |
| Jump / Fall / Land | `Humanoid@JumpToApex1hMelee` / `Humanoid@Falling1hMelee` / `Humanoid@LandingMedium1hMelee` |
| Attack (RightHand1Combat) | `Humanoid@AttackA1hMelee` |
| Interact | `Humanoid@DrawWeapon1hMelee` (플레이스홀더 — 전용 줍기 클립 없음) |

+ 고아 `MM_*` 상태 4개 삭제.

**P0-2. 프리팹에 모델+Animator** — `PlayerCharacter.prefab` 자식으로 `SK_Protof-Actor` 인스턴스 추가, Animator 부착
(controller=PlayerController, avatar=SK_Protof-Actor Generic Avatar), 발이 원점·캡슐 높이에 맞게 정렬, 기존 Capsule 시각 MeshRenderer 비활성.
**콜라이더/CharacterController/Motor 는 건드리지 않는다.**

### Phase 1 — Dead + Dodge 배선

**P1-1. 파라미터+상태 추가** — 컨트롤러에 트리거 `Dead`, `Dodge` 추가.
`AnyState→Dead`(HasExitTime=false, 즉시), `Dodge` 상태(진입 트리거 → 재생 후 Exit).

| 트리거 | 클립 |
|--------|------|
| Dead | `Humanoid@DeathFront1hMelee` (죽는 모션, 마지막 프레임 홀드) |
| Dodge | `Humanoid@DodgeForwardCombat` (Combat 셋, in-place) |

**P1-2. 프리팹 문자열 채움** — `CharacterAgentAnimations` 의 `m_animationDeathTrigger="Dead"`, `m_animationDodgeTrigger="Dodge"`.
(코드 변경 없음 — 이미 `SetTrigger(Dead/Dodge)` 호출 중이라 문자열만 채우면 발화된다.)

### (다음 패스 — 이번 제외) Attack 콤보 A→B→C

콤보 카운터·입력 버퍼·active-window 를 `PlayerCharacterAgent.FireSkill/HandleAttackInput` 에 신설
+ 애니 파라미터(콤보 index)/상태 체인 + (선택)Animation Event 히트. 코드 변경 큼 → 별도.

## 4. 손대는 파일

- `Client/Assets/GameResources/Animations/Player/PlayerController.controller` — 모션 재지정·MM_ 삭제·Dead/Dodge 상태/파라미터
- `Client/Assets/Prefabs/Character/PlayerCharacter.prefab` — 모델+Animator 추가, 2개 트리거 문자열
- **코드 변경 없음** (Phase 0/1 은 순수 에셋/데이터). `CharacterAgentAnimations.cs` 계약 그대로 사용.

## 5. 실행 메커니즘 (MCP)

- 컨트롤러 편집(상태·전이·블렌드·파라미터) = `manage_animation`(`controller_*`) 우선, 복잡부(AnyState, 블렌드 재지정, 서브에셋 클립 로드)는 `execute_code` 로 `UnityEditor.Animations.AnimatorController` API 직접 사용.
- 프리팹 편집 = `execute_code` + `PrefabUtility`. 각 `Humanoid@X.fbx` 의 AnimationClip 서브에셋은 `AssetDatabase.LoadAllAssetsAtPath` 로 로드.
- ⚠️ 새 에셋을 만들 경우 `.meta` 는 gitignore되므로 `git add -f` 필수 (이번엔 기존 파일 수정이라 원칙적으로 없음).

## 6. 검증

1. `read_console` — 컴파일/임포트 에러 0.
2. `execute_code` 무결성 점검 — 모든 상태 `m_Motion != null`, 파라미터에 Dead/Dodge 존재, MM_ 상태 0개.
3. **플레이모드 실물 확인** — 테스트 씬 인스턴스화 → `manage_editor play` → `Animator.SetFloat("Speed",6)` / `SetTrigger("Attack")` / `SetTrigger("Dodge")` / `SetTrigger("Dead")` 순차 구동 → `manage_camera screenshot` 육안 확인.
4. 회귀 — `run_tests(EditMode)` (152) 그린 (코드 변경 없어 통과 예상, 안전차원).

## 7. 리스크 / 확인 지점

- SK_Protof-Actor 스케일/키가 캡슐과 다를 수 있음 → 정렬 단계에서 발-원점·높이 맞춤.
- Interact 전용 클립이 1hMelee 셋에 없어 `DrawWeapon` 플레이스홀더 사용(추후 교체 가능).
- `manage_animation` 이 블렌드/AnyState 를 완전 지원 못하면 즉시 `execute_code` 로 폴백(리스크 낮음).

---

## 후속 작업 — LoopTime · 무기 프롭 · 무기 콜라이더 판정 (2026-07-09)

### ✅ LoopTime 정정
순환 클립 `Idle/Walk/Run/Falling1hMelee` 4개 LoopTime **ON**(멈칫 방지). 원샷 6개(Jump/Land/Attack/Draw/Death/Dodge)는 OFF 유지(Death는 마지막 프레임 홀드).

### ✅ 무기 프롭 부착
`SM_BludgeonProp`(둔기)를 `SK_Protof-Actor/…/humanoid_ R Hand/WeaponProp`에 `localPos=0/localRot=identity`로 부착(로컬 고정 → 애니 시 손 추종). Idle·Attack 스크린샷 확인.

### ✅ #3-A 히트박스를 무기 리치에 맞춰 저작 (서버·클라 공유)
`SkillDefinition` SO → bake `skills.json`. 무기 스윙이 몸통~머리 높이(측정 y 0.4~1.9)인데 기존 박스가 발높이(y[-0.6,0.6])라 수직 어긋남 → **수직 확장**(XZ 정면 리치·판정밸런스는 유지해 테스트 안전).
- basic_swing: offset (0,0,1.0)→(0,0.5,1.0), half (0.6,0.6,0.7)→(0.6,1.0,0.7)
- heavy_swing: offset (0,0,1.3)→(0,0.5,1.2), half (1.1,0.8,1.0)→(1.1,1.1,1.0)
- **검증**: 서버 전투 단위테스트 28/28 · Docker **SocketE2E 27/27**(공격→서버권위 HitboxMath→S_MonsterDead 포함). ⚠️ bake는 다이얼로그 없는 `SkillCatalogExporter.BakeAll()`를 직접 호출할 것(메뉴 `Export()`는 모달 → MCP 프리즈).

### ✅ 무기 콜라이더 + 애니메이션 이벤트 판정 (Main 클라 권위 전용)
"휘두르는 순간 무기에 닿으면 데미지" — 입력순간 OverlapSphere 대신 **무기 콜라이더 스윕 판정**으로 전환.

```
AttackA1hMelee 클립 ─ Animation Event(AttackHitStart@0.35s / AttackHitEnd@0.62s)
   → WeaponAnimationEventRelay(SK_Protof-Actor)  ← 이벤트는 Animator GO 컴포넌트만 호출 가능
   → WeaponHitbox.ActivateWindow/DeactivateWindow (WeaponProp: CapsuleCollider(trigger)+Kinematic RB)
   → OnTriggerEnter(LocalMonster) & 활성중 → OnHit(스윙당 대상 1회)
   → LocalCombat.ApplyWeaponHit → LocalMonster.TakeDamage
```
- 신규: `WeaponHitbox.cs`(WeaponProp), `WeaponAnimationEventRelay.cs`(Animator GO). `LocalCombat` = 무기 있으면 콜라이더 판정, 없으면 기존 OverlapSphere 폴백.
- **던전(서버 권위)은 무관** — 서버는 클라 무기 콜라이더를 모르므로 `C_Attack`→서버 HitboxMath(skills.json) 유지. 무기 콜라이더는 Main 전용.
- **검증**: 플레이모드 격리 셋업에서 `Attack`→AttackA→이벤트→콜라이더 스윕→`OnHit` 발화 콘솔 마커 확인 ✅. EditMode 152/152 ✅.

### ⚠️ 커밋 시 주의
신규 `.cs` 2개의 `.meta`는 gitignore(`*.meta`) → `git add -f`로 함께 커밋해야 클론 시 컴포넌트 GUID가 깨지지 않음. 변경 파일: `WeaponHitbox.cs`(+meta), `WeaponAnimationEventRelay.cs`(+meta), `LocalCombat.cs`, `PlayerCharacter.prefab`, `PlayerController.controller`, `Skill_BasicSwing/HeavySwing.asset`, `skills.json`, 4개 FBX `.meta`(loopTime/이벤트).

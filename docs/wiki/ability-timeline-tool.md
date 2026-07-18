# 어빌리티 타임라인 툴 — 기능 백로그 (CA-5)

> 어빌리티(공격·스킬)의 **연출·판정 타이밍을 시각 편집**하는 에디터 툴의 기능 정의·진행 현황.
> 데이터/런타임 = codemap §2.81(Phase 1a), 편집 창 = §2.82(Phase 2, UI Toolkit).
> 참조 = [Fofanius/unity-tool-timeline-event-track](https://github.com/Fofanius/unity-tool-timeline-event-track) (Unity Timeline Event Track).

---

## 1. 참조 툴(Fofanius Event Track) 기능 해부

**정체**: Unity Timeline 에 붙는 커스텀 트랙. "재생 헤드가 클립 시작에 닿으면 대상 객체의 **public 메서드를 호출**"한다. 순수 연출/이벤트 디스패치 도구.

| # | 기능 | 구현 | 우리에 유용? |
|---|------|------|:---:|
| R1 | **Event Track / Event Clip** (Timeline 기반) | `EventTrack:TrackAsset`·`EventClip:PlayableAsset`·`EventBehaviour:PlayableBehaviour`·`EventTrackMixer` | 개념만 (기반은 다름 — §3) |
| R2 | **클립 시작에 발화** | `EventBehaviour.OnBehaviourPlay`/Mixer `ProcessFrame` | ✅ 이미 있음(timeMs 오프셋 발화) |
| R3 | **Target 선택** (GameObject→Component 드릴다운) | `ExposedReference<Object>` + GenericMenu(컴포넌트 목록) | ◐ Invoke 이벤트에 채택(P7) |
| R4 | **Method 드롭다운** (public·void·0/1 인자 리플렉션) | `MethodSignature.GetCallableMethods` + GenericMenu | ◐ Invoke 이벤트(P7) |
| R5 | **타입 인자 1개** (int/float/double/bool/string/Color/Vec2/Vec3/Object) | `SerializedArguments`(union) + 타입별 인스펙터 필드 | ◐ Invoke 이벤트(P7) |
| R6 | **리플렉션 직렬화** (타입 AQN+메서드명 저장→역직렬화 시 재해석) | `ISerializationCallbackReceiver` | Invoke 채택 시만 |
| R7 | **에디트모드 발화 + 로그 토글** (`TriggerInEditMode`/`NotifyInEditMode`) | 스크럽 중 메서드 실행/로그 | ✅✅ **스크럽 프리뷰의 핵심**(P5) |
| R8 | **클립 툴팁 = 메서드 라벨** | `ClipEditor.GetClipOptions.tooltip` | ✅ 마커 툴팁(P4) |
| R9 | **클립 변경 검증** (target 재해석) | `ClipEditor.OnClipChanged` | ◐ id 유효성(P3) |
| R10 | **런타임 Mixer 발화** | `EventTrackMixer.ProcessFrame` | ✅ `AbilityCuePlayer` 가 담당 |
| R11 | 패키지/asmdef 분리(Runtime+Editor), git-URL 설치 | package.json | 프로젝트 내장이라 불요 |

**참조 툴의 한계(우리가 더 나은 점 포함)**:
- Undo/Redo 없음(Target/Method 편집) → **우리는 SerializedObject 라 Undo 자동 ✅**.
- **클립 시작에만** 발화(구간·중간 이벤트 없음, 소스에 `TODO: start/end/intermediate` 존재) → 우리는 윈도우 이벤트로 개선 가능(P6).
- 메서드 1개·인자 1개·타입 제한.

---

## 2. 우리 Tool 기능 목록 — 있음 / 채택

### 이미 있음 (Phase 1a·2)
- 시간 오프셋(ms) 이벤트, 트랙 4행(Anim/판정창/VFX/SFX), 색 구분
- 마커 드래그(FPS 스냅)·**우클릭 추가(id 미정)**·우클릭 삭제·좌클릭 선택
- 판정창 엣지 드래그(startup/active), 스크럽, 인스펙터 편집
- **SerializedObject → Undo·dirty 자동** (참조툴보다 나음)
- **판정=서버 bake / 연출=클라 로컬** 2갈래 분리(서버 권위 보존)
- 런타임 재생기 `AbilityCuePlayer`(SFX/VFX ms 발화)

### 참조에서 채택할 것
- **A. Cue id 드롭다운** — free text 대신 `CueCatalog` 등록 id 중 선택(+새 id). = R4 의 우리 판(리소스 선택). **"뭘 쓸지 모름"을 목록으로 좁힘.**
- **B. 마커 툴팁·라벨** — hover 시 kind·id·time·socket. = R8.
- **C. 에디트모드 스크럽 프리뷰** — 스크럽이 이벤트 시각을 지날 때 그 SFX/VFX 를 **에디터에서 재생**(+로그 토글). = R7. 씬에 액터 있을 때.
- **D. 윈도우 이벤트(지속 구간)** — 점 이벤트 외 start~end 클립(지속 VFX·루프 SFX). `AbilityCueEvent.durationMs` 추가. = 참조 TODO 개선.
- **E. Invoke 이벤트 종류** — 시각 T 에 **액터의 메서드 호출**(타입 인자 1개). = R3~R6. **클라 전용 훅**(히트박스 개폐·커스텀 연출). ⚠ 서버 판정은 여기 태우지 않는다(§3).
- **F. QoL** — 이벤트 복제, 화살표 넛지, 다중 선택.

---

## 3. ⚠ 아키텍처 갈림길 (재분할 전 결정 필요)

참조 툴은 **Unity Timeline 기반**(TimelineAsset + PlayableDirector). 우리 툴은 **커스텀 창이 AbilityDefinition(SO) 데이터를 편집**. 둘은 기반이 다르다. 이유 = 우리의 하드 제약:

```
판정창(hit-detection) 은 헤드리스 .NET 서버가 읽어야 한다(서버 권위·치팅 방지)
        → Unity Timeline 은 서버에서 못 돈다
        → 판정 타이밍은 반드시 plain 데이터(abilities.json)로 bake 돼야 한다
```

- **A안(권장) — 커스텀 창 강화**: AbilityDefinition 을 단일 진실원으로 두고 참조 기능(A~F)을 우리 창에 흡수. 판정=bake, 연출=SO. 지금까지(Phase 1a·2)를 그대로 잇고 서버 권위·단일소스 보존.
- **B안 — Unity Timeline 채택**: 연출을 TimelineAsset+EventTrack 으로 저작(폴리시 UI 무료). 단 데이터가 TimelineAsset(연출)+AbilityDefinition(판정)으로 쪼개지고, 런타임이 PlayableDirector 에 결합. 판정은 여전히 별도 bake 필요(이중 소스).

→ **A안 권장**: 서버 권위(판정 bake) 제약을 Timeline 이 못 풀고, 이미 만든 자산을 잇는다. 참조의 값진 기능(스크럽 프리뷰·메서드 invoke·타입 인자)은 A안 창에 전부 이식 가능.

---

## 4. 재분할 Phase (기능 하나씩)

| Phase | 기능 | 상태 | 규모 |
|---|------|:---:|:---:|
| P0 | 데이터 모델(AbilityCueEvent/Plan/CueCatalog) | ✅ §2.81 | — |
| P1 | 런타임 재생(AbilityCuePlayer + 3자리 배선) | ✅ §2.81 | — |
| P2 | 편집 창 기본(UI Toolkit 룰러/트랙/마커/드래그/우클릭/스크럽/인스펙터/판정창) | ✅ §2.82 | — |
| **P3** | **Cue id 드롭다운** (CueCatalog 등록 id 선택 + 새 id) — 채택 A | ✅ §2.83 | 소 |
| **P4** | **마커 툴팁·라벨** (kind·id·time·socket) — 채택 B | ✅ §2.84 | 소 |
| **P4+** | **판정창 바 리사이즈·이동 부드럽게** (드래그 중 리빌드 제거→캡처 유지, 그립 가시화, 본체=이동) | ✅ §2.84 | 소 |
| **P5** | **에디트모드 스크럽 프리뷰** (▶Preview 가 스크럽 굴리며 통과 이벤트 SFX/VFX 재생+Notify 로그, 참조 R7) | ✅ §2.85 | 중 |
| **P6** | **윈도우 이벤트(durationMs)** — 모든 이벤트가 리사이즈 클립(본체=이동·우그립=길이·좌그립=시작), 지속 VFX 수명 | ✅ §2.85 | 중 |
| **P7** | **Invoke 이벤트 종류** (액터 메서드 호출·타입 인자, 클라 전용) — 채택 E | ✅ §2.86·§2.87 | 대 |
| ↳ 첫 증분 | `ECueKind.Event`+`invokeMethod`(리플렉션 호출)+5행 Event 트랙+인스펙터 | ✅ §2.86 | — |
| ↳ 잔여 | **타입 인자**(None/Float/Int/Bool/String·참조 R5) + **메서드 드롭다운**(Actor 프리팹 컴포넌트 나열·참조 R4) + **판정창→Event 헬퍼**(ActivateWindow/DeactivateWindow 생성=Main 판정 통일 코드조각). 잔여=애니이벤트 실제 제거·플레이검증(사용자) | ✅ §2.87 | — |
| **직접 리소스** | Cue = 카탈로그 선택이 아니라 **AudioClip/프리팹 직접 드래그**(사용자 요청). id+카탈로그는 폴백 | ✅ §2.86 | 소 |
| **P8** | **QoL** (복제 Ctrl+D·화살표 넛지·다중 선택 Ctrl+클릭 → 그룹 delete/nudge/duplicate) — 채택 F | ✅ §2.88 | 소 |

각 Phase 완료 시 codemap §2.8x 추가 + 이 표 갱신.

**→ P0~P8 코드 전부 완료(2026-07-18).** 잔여 = 사용자 배선(Phase 1b: 이벤트에 실 SFX/VFX 드래그·프리팹에 `AbilityCuePlayer`·Event용 Actor 프리팹) + Main 판정 통일의 애니이벤트 실제 제거·플레이검증.

> **편집 불능 버그 수정(2026-07-18, 커밋 `459b1016`)**: 인스펙터 숫자/메서드 필드가 키 입력마다 `RebuildAll` 로 파괴돼 포커스 상실 → 사실상 편집 불가였다. `panel.Pick` 진단으로 클릭 라우팅은 정상(클립 Pick=SELF) 확인 → `isDelayed=true`(Enter/blur 커밋)로 응급 수정. **근본 해결 = W1 인스펙터 바인딩**(아래).

---

## 5. 다음 우선순위 (사용자 지정, 2026-07-18) — **구현 대상**

지금 창의 두 구조적 한계를 사용자가 지목. 이게 다음 실작업이다.

### W-A. 오른쪽 상세 패널 + 전 종류 이벤트 인라인 편집
- **문제**: 현재 인스펙터가 **창 하단**(root flexDirection=column: 툴바→타임라인→하단 인스펙터). 레퍼런스(Unreal Montage details / Unity Timeline inspector)처럼 **오른쪽 세로 패널**이라야 타임라인을 넓게 쓰며 편집한다.
- **해야 할 것**: 루트를 `가로 분할`(왼쪽=툴바+타임라인 스크롤 / 오른쪽=details 패널, 고정폭 ~320). 이벤트(SFX/VFX/Anim/Event/판정창) **어느 것을 클릭해도** 오른쪽에서 그 이벤트의 전 필드를 편집. **모든 종류가 편집돼야 함**(현재 Anim 은 "재생 없음" 노트만 — 최소한 kind/time/duration 은 편집 가능해야).
- **연결**: `RefreshInspector` → `RefreshDetails`(오른쪽 패널 채우기). 선택 변경 시 패널만 갱신(타임라인 리빌드 분리).
- 규모: 중(레이아웃 재구성 + 인스펙터 이동).

### W-B. 트랙 동적 추가/삭제
- **문제**: 현재 트랙이 **kind 에 1:1 고정 5행**(Anim/판정창/VFX/SFX/Event). 사용자는 트랙을 **자유롭게 추가·삭제**하고 싶어 함(겹치는 마커를 여러 레인으로 분산 = Unreal Notify 다중행).
- **설계 결정 필요 (착수 전)**:
  - **모델 ①(레인, 권장)**: 트랙은 여전히 kind 타입을 갖되 **같은 kind 를 여러 개**(레인) 추가 가능. 이벤트에 `lane`(int) 추가 → 같은 kind·다른 lane 은 다른 행. 판정창(게임플레이)은 단일 유지. 데이터 최소 변경(`AbilityCueEvent.lane`), 런타임 무관(연출 재생은 lane 무시).
  - **모델 ②(자유 트랙)**: 트랙이 이름·타입 자유. 이벤트가 `trackId` 로 소속. Unity Timeline 식. 데이터·UI 대공사.
  - → **① 권장**(YAGNI: 목적은 "겹침 분산"이지 임의 트랙 아님). 판정창/Anim 앵커는 고정, VFX/SFX/Event 는 레인 추가/삭제.
- **해야 할 것**: 트랙 헤더에 `+`(레인 추가)·`×`(빈 레인 삭제) + `AbilityCueEvent.lane` + 행 계산이 kind×lane. 
- 규모: 중~대(행 레이아웃 동적화).

---

## 6. 개선 백로그 (레퍼런스 분석 → 지금 구현 X, 문서화만)

Unreal Animation Montage + Unity Timeline Editor 분석에서 나온 개선점. **우선순위 W-A/W-B 이후** 필요 시 착수.

| # | 개선 | 레퍼런스 근거 | 규모 |
|---|------|------|:---:|
| **W1** | **인스펙터 정식 바인딩**(`rootVisualElement.Bind(so)`) — 값 변경 시 RebuildAll 제거, 자동 동기화. `isDelayed` 응급처치의 근본 해결 | 두 툴 다 "안정적 details, 편집 중 타임라인 안 흔들림" | 중 |
| **W2** | **라이브 메시 프리뷰** — ▶Preview 가 로그/스폰 대신 액터 프리팹을 뷰포트에 재생하며 스크럽 동조 | Unreal 프리뷰 뷰포트 · Unity PlayableDirector 스크럽 | 대 |
| **W3** | **Sections/loop 구간** — 이름 붙은 시간 구간(콤보 단계·루프) 저작·점프 | Unreal Montage Sections | 중 |
| **W4** | **프레임/초 룰러 토글** — ms 외 프레임 눈금 | Unity Timeline 룰러 토글 | 소 |
| **W5** | **점 vs 구간 시각 구분** — duration 0=diamond, >0=bar | Unreal Point Notify vs Notify State | 소 |
| **W6** | **트랙 mute/lock/접기** — 트랙 헤더 토글 | Unity Timeline 트랙 헤더 | 소 |
| **W7** | **Anim 이벤트 실재생** — 지연 애니 트리거(현재 재생 없음) 구현 | — | 소 |
| **W8** | **커브 트랙**(float 커브) — 필요 시(연출엔 대체로 YAGNI) | Unreal/Unity Curves | 대 |

> 착수 순서 제안: **W-A(오른쪽 패널) → W1(바인딩, W-A 와 합치면 근본적) → W-B(트랙 동적) → 나머지 W2~W8 선택**.

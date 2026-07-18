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

# 챕터 22 학습 로그 — HUD 창 MVI 확장 (스탯창 · 퀘스트 추적 · 판매)

> 7.3 스탯창 · 7.4 퀘스트 추적 HUD · 7.6 인벤토리 판매(Sell) — 세 UI를 연달아 붙이며 같은 두 함정을 반복해 만났고, 같은 두 패턴으로 풀었다.
> ① **GameHud는 Main·던전 공용**이라, Main 전용 Model을 하드 주입하면 던전 HUD가 깨진다 → **선택 주입(`TryResolve`)**.
> ② **GUI는 `Game.System`을 참조하지 못한다**(MVI 레이어 규칙) → System 타입을 **Presentation이 View용 표현으로 변환**해 노출.
> 세 창 모두 `ToggleX 인텐트 → ViewController → Addressable 프리팹 토글`이라는 검증된 한 경로를 재사용한다.

---

## 공통 골격 — 한 경로로 창을 연다

```
GameHud (버튼 / 단축키)
   → InGameModel.Accept(ToggleX)        ← 즉발 신호(처리중 가드 무관)
   → OnToggleX (Observable)
   → XViewController (POCO, IInitializable)
        최초 1회 X.prefab Addressable 로드 + InjectGameObject → 이후 SetActive 토글
   → X (View): 전용 Model 주입 → State 구독 → 렌더
```

스탯창(Ability버튼·G키), 퀘스트창(Q키), 상점·인벤토리가 전부 이 틀을 공유한다. 새 창은 "인텐트 1개 + 컨트롤러 1개 + 프리팹 1개"면 끝 — 검증된 funnel을 늘리는 식이라 회귀 위험이 낮다.

---

## 함정 ① 공용 GameHud + Main 전용 의존 → 던전에서 DI가 깨진다

`GameHud`는 Lobby(Main)에서도 던전에서도 뜬다(생명주기 비종속이 설계 원칙). 그런데 `QuestModel`·`ProgressionModel`은 **MainLifetimeScope에만** 등록돼 있다(게다가 `QuestModel` ctor는 Main 전용 `QuestNotifier`를 요구 → 던전에 등록하려면 cascade).

여기서 순진한 선택 — 트래커/창 컴포넌트에 `[Inject] QuestModel`을 박는 것 — 은 **던전 GameHud를 통째로 깨뜨린다**(VContainer가 던전 스코프에서 QuestModel을 못 풀어 InjectGameObject가 throw). 이건 이 프로젝트에서 반복된 패턴이다(이전 `PlayerStatApplier`, `IInputContext`도 같은 사고).

### 해법 — `IObjectResolver.TryResolve`로 선택 주입

```csharp
[Inject] private IObjectResolver _resolver;   // 항상 해소됨

void Start() {
    if (_resolver.TryResolve(typeof(QuestModel), out var m) && m is QuestModel model) {
        // Main: 구독·렌더
    } else {
        root.SetActive(false);   // 던전: QuestModel 미등록 → 조용히 숨김
    }
}
```

`IObjectResolver`는 어느 스코프에서나 주입되므로 컴포넌트는 안 깨진다. 대상 Model의 존재 여부로 "이 씬에서 이 창이 의미 있나"를 런타임에 판정 → Main에선 동작, 던전에선 무해하게 비활성.

> 핵심 교훈: **asmdef/scope 방향은 코드 작성 후 검사가 아니라 작성 전 제약**이다. "공용 컴포넌트가 특정 스코프 전용 의존을 하드로 잡는다"는 신호가 보이면, 그 자리에서 선택 주입으로 바꾼다.

---

## 함정 ② GUI는 System을 모른다 → Presentation이 View용으로 변환

MVI 레이어 규칙: `Game.GUI → Game.Presentation → Game.System`. **GUI는 `Game.System`을 참조하지 않는다.** 그래서:

- 퀘스트 진행중 판정에 `QuestProgressState`(System enum)를 GUI에서 쓸 수 없다 → `QuestEntryModel`의 **bool 헬퍼**(`!CanAccept && !IsClaimed`)로 판정.
- 스탯창이 `ProgressionData`/`ProgressionStats`(System struct)를 GUI에서 읽을 수 없다 → `ProgressionViewState`가 **`StatLine`(라벨/값 문자열) 목록**으로 변환해 노출. GUI는 문자열만 렌더, 색상만 입힌다.

```
System            Presentation 변환물            GUI(렌더)
QuestProgressState → QuestEntryModel.CanAccept/IsClaimed(bool) → 진행중 필터
ProgressionData    → ProgressionViewState.Lines(StatLine[])     → 행 복제 렌더
```

데이터 구동(행 템플릿 복제 + 문자열 라인)으로 만들면 프리팹 필드도 최소(컨테이너+템플릿)고, 포맷·라벨은 Presentation이 소유해 레이어가 깨끗하다.

---

## 함정 ③ 실시간 갱신 — "올바른 레이어에서 신호를 구독한다"

퀘스트 추적 HUD가 가장 까다로웠다. "수락하면 바로 뜨고, 진행도 바로 오르고, 보상 받으면 사라져야 한다"인데, 갱신원이 둘로 갈렸다:

```
수락/보상  : NPC 대화(DialogueModel)가 IQuestService 직접 호출 → QuestModel.State 미갱신
             → 트래커가 두 경로 공유 알림 QuestNotifier.OnNotice 구독 → 알림마다 Refresh
킬 진행도  : 진행(ReportKill)은 서버 내부 → 클라 신호 없음
             → 킬 직후 PlayerProgressionHolder.OnChanged(exp 갱신) 발화
             → QuestModel이 이를 구독해 self-Refresh
```

여기서 레이어가 또 갈렸다: `PlayerProgressionHolder`(킬 신호)는 **`Game.System`**이라 GUI 트래커가 직접 구독 못 한다. 하지만 **Presentation은 System을 참조할 수 있으므로** 구독은 `QuestModel`(Presentation)에 둔다. "신호는 받아야 하는데 받을 수 있는 레이어가 어디인가"를 먼저 답하고 위치를 정한 것 — 챕터 20의 ASC 적용과 같은 "마지막 1마일을 올바른 곳에 꽂기".

---

## 함정 ④ 판매(Sell) — 서버 권위 + "프리팹에 버튼이 아예 없었다"

상점 판매는 서버(인벤 차감→골드 적립)가 이미 완비돼 클라 배선만 하면 됐다. 두 가지가 포인트:

- **가격 표시 vs 권위**: 확인 팝업의 가격은 서버 `GetShop.sell_price`(표시용), 최종 권위는 `SellResponse.gold`. 클라는 표시만, 결과는 서버 응답이 진실.
- **장착품 제외는 공짜였다**: 인벤토리 Model이 이미 착용 itemId를 표시에서 제외하므로, 판매 버튼은 비장착에만 자동으로 뜬다(추가 로직 0).
- **함정**: "판매 버튼이 안 뜬다"의 원인은 코드가 아니라 **프리팹에 SellButton GameObject 자체가 없었다**(필드 NULL). `WireButton`이 null이면 조용히 return → 코드는 옳은데 안 보임. UI 작업은 "코드 경로"와 "프리팹 배선"이 둘 다 맞아야 산다는 흔한 함정.

---

## 처음 생각한 것 → 피드백으로 수정된 것

- **"공용 컴포넌트에 Model을 그냥 [Inject]하면 되겠지"** → 던전 스코프에서 DI가 깨진다. 공용 + 특정 스코프 전용 의존 = `TryResolve` 선택 주입.
- **"State 구독만 하면 다 실시간이겠지"** → 대화 수락은 다른 경로(IQuestService 직접)라 State를 안 건드린다 → 공유 알림(QuestNotifier)·킬 신호(holder.OnChanged)를 *올바른 레이어*에서 추가 구독해야 했다.
- **"판매 버튼이 안 뜨네, 코드 버그인가"** → 프리팹에 버튼 오브젝트가 없었다. 코드/프리팹 둘 다 봐야 한다.

## 핵심 키워드

- **선택 주입(`IObjectResolver.TryResolve`)**: 공용 컴포넌트가 특정 스코프 전용 의존을 가질 때, 하드 주입 대신 런타임 존재 판정.
- **레이어 변환물**: GUI가 System을 못 보므로 Presentation이 bool 헬퍼/문자열 라인(StatLine)으로 변환해 노출.
- **신호 구독은 받을 수 있는 레이어에**: 킬 신호(System)는 GUI 불가 → Presentation(QuestModel)이 구독.
- **단일 토글 funnel**: ToggleX 인텐트 → ViewController → Addressable 프리팹 토글. 새 창 = funnel 확장.
- **표시 vs 권위**: 가격은 표시(sell_price), 결과는 서버 응답(gold)이 진실.
- **코드 경로 ≠ 프리팹 배선**: UI는 둘 다 맞아야 동작.

# 22. HUD 창을 세 개 붙이며 — 같은 함정을 세 번 만나면 패턴이다

> **한 줄** — 스탯창·퀘스트 추적기·판매를 연달아 붙였는데, 세 번 모두 **같은 두 가지 벽**에 부딪혔다. 하나는 "공용 컴포넌트가 특정 씬에만 있는 의존을 잡으면 다른 씬이 깨진다", 다른 하나는 "GUI가 볼 수 없는 타입을 View에 노출하려 했다"였다. 세 번째쯤 되면 그건 실수가 아니라 **구조가 알려주는 신호**다.
>
> **범위** 선택 주입 · 레이어 변환 · 실시간 갱신의 구독 위치 · 표시 vs 권위
> **결과** 새 창 = "인텐트 1 + 컨트롤러 1 + 프리팹 1" — 검증된 경로를 늘리는 방식

---

## 1. 창 하나를 여는 경로는 하나다

```
GameHud (버튼 / 단축키)
   → InGameModel.Accept(ToggleX)          즉발 신호
   → OnToggleX (Observable)
   → XViewController (POCO, IInitializable)
        최초 1회 X.prefab Addressable 로드 + Inject → 이후 SetActive 토글
   → X (View): 전용 Model 주입 → State 구독 → 렌더
```

스탯창(G키)·퀘스트창(Q키)·인벤토리·상점이 전부 이 틀을 공유한다.

**새 창을 만드는 일이 "새 방식을 고안하는 일"이 아니라 "검증된 funnel에 하나 더 얹는 일"** 이 되면 회귀 위험이 급감한다. 실제로 이 챕터에서 붙인 세 UI 중 **토글 경로에서 난 문제는 하나도 없었다** — 문제는 전부 그 바깥(DI 스코프·레이어·프리팹)에서 났다.

## 2. 함정 ① — 공용 컴포넌트가 특정 씬 전용 의존을 잡으면 다른 씬이 죽는다

`GameHud`는 **Main에서도 던전에서도 뜬다**(씬 생명주기에 묶지 않는 것이 설계 원칙, [09](./chapter-09-unity-client.md) 3절). 그런데 `QuestModel`·`ProgressionModel`은 **`MainLifetimeScope`에만** 등록돼 있다.

순진한 선택은 트래커 컴포넌트에 `[Inject] QuestModel`을 박는 것이다. 결과는:

```
던전 씬 → VContainer 가 QuestModel 을 해석 못 함
        → InjectGameObject 가 throw
        → GameHud 전체가 죽는다      ← 퀘스트 추적기 하나 때문에 HUD 전부
```

**부분 기능의 의존성이 전체 컴포넌트를 인질로 잡는다.**

### 해법 — 존재를 런타임에 묻는다

```csharp
[Inject] private IObjectResolver _resolver;    // 이건 어느 스코프에서나 해소된다

if (_resolver.TryResolve(typeof(QuestModel), out var m) && m is QuestModel model)
    { /* Main: 구독·렌더 */ }
else
    root.SetActive(false);                     // 던전: 조용히 숨김
```

`IObjectResolver`는 항상 주입되므로 컴포넌트가 깨지지 않는다. **대상 Model의 존재 여부가 곧 "이 씬에서 이 창이 의미 있는가"의 답**이 된다.

의도가 코드에 박제돼 있다 — `QuestTrackerView.cs:15` *"하드 [Inject] 시 던전 GameHud 가 깨진다 → IObjectResolver.TryResolve 로 선택 주입(없으면 추적기 숨김)"*.

### 세 번째 반복이었다

같은 사고를 이전에도 두 번 냈다 — `PlayerStatApplier`, `IInputContext`. 실제로 `CharacterSpawner.cs:117`이 `PlayerProgressionHolder`를 같은 방식(`TryResolve`)으로 잡고 있다.

> **같은 함정을 세 번 만나면 그건 실수가 아니라 구조적 신호다.** 이 프로젝트에는 "여러 씬에서 살아야 하는 공용 컴포넌트"와 "씬 전용 스코프"라는 두 축이 있고, 그 교차점에서 항상 이 문제가 난다. 그래서 규칙으로 승격했다 — **공용 컴포넌트가 특정 스코프 전용 의존을 하드로 잡으려는 순간, 그 자리에서 선택 주입으로 바꾼다.**
>
> asmdef·스코프 방향은 **작성 후 검사가 아니라 작성 전 제약**이다([레이어 규칙](../wiki/unity-layer-separation.md)).

## 3. 함정 ② — GUI가 볼 수 없는 타입을 View에 노출하려 했다

레이어 규칙상 **`Game.GUI`는 `Game.System`을 참조하지 않는다.** 그래서 System 타입을 그대로 노출하면 View가 그걸 읽는 순간 위반이다.

```
System 타입                  Presentation 변환물                   GUI
QuestProgressState(enum)  →  CanAccept / IsClaimed (bool)      →  진행중 필터
ProgressionData(struct)   →  ProgressionViewState.Lines        →  행 템플릿 복제 렌더
                                (StatLine = 라벨/값 문자열)
```

**변환의 방향이 중요하다** — GUI가 쓰기 편한 형태(bool, string)로 낮춘다. 그러면 부수 효과가 따라온다:

- **포맷과 라벨을 Presentation이 소유**한다 — "공격력 120"의 문자열 조립이 View가 아니라 Model에 있다.
- **프리팹 필드가 최소**가 된다 — 스탯 항목마다 텍스트 필드를 두는 대신 컨테이너 + 행 템플릿 하나면 된다.
- **스탯이 늘어도 View 코드가 안 바뀐다** — 데이터 구동이므로 라인이 하나 더 생길 뿐이다.

> 레이어 규칙을 지키려다 만든 변환이 **결과적으로 더 나은 UI 구조**를 만들었다. 제약이 설계를 밀어준 사례다.

## 4. 함정 ③ — 신호를 "받을 수 있는 레이어"가 어디인가

퀘스트 추적기가 가장 까다로웠다. 요구는 단순했다 — *수락하면 바로 뜨고, 진행도가 바로 오르고, 보상을 받으면 사라진다.* 문제는 **갱신을 알리는 신호가 두 갈래**였고, 둘 다 `QuestModel.State`를 거치지 않았다는 것이다.

```
[수락 · 보상 수령]
   NPC 대화(DialogueModel)가 IQuestService 를 직접 호출
      → QuestModel.State 는 그대로 → 트래커가 모른다
   해법: 두 경로가 공유하는 알림 소스 QuestNotifier 를 구독 → 알림마다 Refresh

[킬 진행도]
   진행(ReportKill)은 서버 내부 호출 → 클라로 오는 신호가 아예 없다
   해법: 킬 직후 발화하는 PlayerProgressionHolder.OnChanged(exp 갱신)에 편승
```

두 번째에서 레이어가 갈렸다.

```
PlayerProgressionHolder  = Game.System     ← GUI 는 구독할 수 없다
QuestModel               = Game.Presentation ← System 을 참조할 수 있다
   ⇒ 구독은 QuestModel 에 둔다. 트래커는 Model 만 본다.
```

**"어디에 코드를 둘까"가 아니라 "이 신호를 받을 수 있는 레이어가 어디인가"를 먼저 물었다.** 그러면 위치가 저절로 정해진다. 같은 판단을 [21](./chapter-21-connection-liveness-hp-authority.md) 5절(값이 이미 있는 곳에서 ASC로 꽂기)에서도 했다 — **마지막 1마일을 올바른 곳에 두는 문제.**

> 다만 **`OnChanged`(exp 갱신)에 편승한 것은 우아하지 않다** — 퀘스트 진행이 경험치 변화를 통해 간접 감지된다. 서버가 진행 변화를 직접 알리는 신호가 생기면 그쪽이 맞다. 지금은 킬과 exp가 항상 함께 오므로 성립한다.

## 5. 함정 ④ — 코드는 맞는데 프리팹에 버튼이 없었다

판매 기능은 서버가 이미 완비돼 있어(인벤 차감 → 골드 적립, [18](./chapter-18-wallet-shop.md)) 클라 배선만 하면 됐다. 그런데 **판매 버튼이 안 떴다.**

원인은 코드가 아니었다 — **프리팹에 SellButton GameObject 자체가 없었다.** 필드가 null이고, `WireButton`은 null이면 조용히 return한다.

```
코드 경로  ✅ 정상
프리팹 배선 ❌ 없음
결과       아무 에러 없이 "그냥 안 보임"
```

> **UI는 코드와 프리팹이 둘 다 맞아야 산다.** 그리고 이런 실패는 **예외가 아니라 침묵**으로 나타난다 — null 체크가 방어적으로 짜여 있을수록 더 조용하다. (이 주제가 [27](./chapter-27-silent-failure.md)에서 정면으로 다뤄진다.)

### 공짜로 얻은 것 하나

장착 중인 아이템은 판매 목록에 안 떠야 하는데, **추가 로직이 필요 없었다.** 인벤토리 Model이 이미 착용분을 표시에서 제외하고 있었기 때문이다([17](./chapter-17-equipment-system.md) 4절).

> "표시 필터"로 만들어 둔 결정이 나중 기능에 그대로 상속됐다. DB에서 뺐다면 여기서 다시 판단해야 했을 것이다.

## 6. 표시 값과 권위 값을 구분한다

```
확인 팝업의 가격  = GetShop.sell_price     (표시용)
실제 지급 결과    = SellResponse.gold      (권위 — 이것이 진실)
```

클라가 미리 보여주는 값과 서버가 확정하는 값은 **다를 수 있다**(가격 변경, 수량 조정 등). 표시용 값으로 UI를 갱신하면 서버와 어긋나는 순간이 생긴다. **클라는 예상을 보여주고, 결과는 응답으로 덮는다.**

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 공용 컴포넌트는 선택 주입 | 씬을 넘나드는 HUD 확장의 기본 규칙 |
| Presentation이 View용으로 변환 | 데이터 구동 UI(행 템플릿) 패턴 |
| 신호는 받을 수 있는 레이어에서 구독 | 레이어 경계를 넘는 갱신의 표준 대응 |
| 코드 경로 ≠ 프리팹 배선 | 조용한 실패 사냥([27](./chapter-27-silent-failure.md)·[29](./chapter-29-multiplayer-sync-invisible-failures.md) 2절) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-22-hud-windows-mvi.md](../learning-log/chapter-22-hud-windows-mvi.md)

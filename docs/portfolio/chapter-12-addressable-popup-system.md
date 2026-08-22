# 12. Addressable 소유권 — "null이면 해제됐다"를 불변으로 만들기

> **한 줄** — Addressable은 **참조 카운트**로 동작하는데, 핸들과 인스턴스를 따로 들고 있으면 한쪽만 정리해도 컴파일은 통과한다. 누수는 조용하고, 이중 해제는 크래시다. 두 개를 **한 객체가 함께 소유**하게 만들어 "필드가 null이면 완전히 해제된 것"이라는 **불변식 하나로 줄였다**.
>
> **범위** 리소스 소유권 · 소유권 이전 · 프리팹 무수정 이벤트 배선 · MVI와 팝업
> **현재 규모** 도입 시 3개 컨트롤러 → **18개 파일**에서 사용 중

---

## 1. 문제 — 손으로 맞춰야 하는 두 개의 필드

같은 패턴이 세 컨트롤러에 복사돼 있었다.

```csharp
private AsyncOperationHandle<GameObject> _handle;   // 리소스
private GameObject _instance;                       // 인스턴스   ← 두 개를 사람이 맞춰야 한다

// 해제
if (_instance != null) Destroy(_instance);
if (_handle.IsValid()) Addressables.Release(_handle);
```

Addressables는 **ref count** 기반이다.

```
LoadAssetAsync("A.prefab")  → +1
Release(handle)             → -1   (0이 되면 실제 언로드)
```

여기서 두 가지가 갈린다.

| 실수 | 결과 | 발견 시점 |
|---|---|---|
| `Destroy`만 하고 `Release` 누락 | ref count가 안 내려감 → **에셋이 메모리에 영구 잔류** | 안 남 (조용한 누수) |
| `Release` 두 번 | ref count 음수 → **크래시** | 런타임, 재현 어려움 |

**둘 다 컴파일러가 잡아주지 않는다.** 그리고 문제의 본질은 "실수했다"가 아니라 **실수할 수 있는 구조**였다는 것이다. 필드가 두 개면 정리 코드도 두 줄이고, 한 줄만 지우면 절반만 해제된다.

## 2. 해법 — 소유권을 한 객체로

```csharp
public sealed class AddressableInstance : IDisposable
{
    public GameObject GameObject { get; }
    private readonly AsyncOperationHandle<GameObject> _handle;
    private bool _disposed;                      // ← 이중 해제 차단

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (GameObject != null) Object.Destroy(GameObject);
        if (_handle.IsValid()) Addressables.Release(_handle);
    }
}
```

호출부에서 남는 것은 필드 하나다.

```csharp
private AddressableInstance? _inst;

_inst = await AddressableLoader.LoadAndInstantiateAsync(key, parent, ct);
...
_inst?.Dispose();
_inst = null;
```

이걸로 **불변식이 하나로 줄었다**.

```
_inst != null  ⇔  열려 있고 리소스를 점유 중
_inst == null  ⇔  Destroy + Release 둘 다 끝남
```

"둘 다 정리했나?"를 매번 확인할 필요가 없어진다. C++의 RAII, C#의 `IDisposable`이 원래 하는 일이지만, **Unity의 Addressables는 그 짝을 강제하지 않으므로 직접 만들어야 한다.**

## 3. 로드 쪽 — 실패를 null로 정규화

```csharp
public static async UniTask<AddressableInstance?> LoadAndInstantiateAsync(
    string key, Transform parent, CancellationToken ct)
{
    var handle = Addressables.LoadAssetAsync<GameObject>(key);
    try { await handle.Task.AsUniTask().AttachExternalCancellation(ct); }
    catch (OperationCanceledException)
    {
        if (handle.IsValid()) Addressables.Release(handle);   // ← 취소도 누수 경로다
        return null;
    }
    return new AddressableInstance(Object.Instantiate(handle.Result, parent), handle);
}
```

**취소는 정상 흐름인데 리소스 관점에서는 누수 지점**이다. 로딩이 시작된 뒤 취소되면 핸들은 이미 발급돼 있다. 이걸 호출부마다 처리하게 두면 언젠가 빠진다.

유틸이 안에서 정리하고 `null`을 돌려주므로, 호출자는 **`try/catch` 없이 null 검사 한 줄**로 끝난다. 실패의 종류(취소/키 없음/로드 실패)를 호출자가 구분할 필요가 없다면 **하나의 값으로 정규화**하는 편이 낫다.

## 4. 소유권 이전 — 팝업은 자기가 어떻게 로드됐는지 모른다

팝업 스크립트가 Addressables를 알면, **Addressable이 아닌 방식으로는 못 쓰는 컴포넌트**가 된다(테스트 씬에 직접 배치 등).

```csharp
// BasePopup — 소유권을 "받는다". 스스로 로드하지 않는다.
public void SetAddressableOwner(AddressableInstance inst) => _owner = inst;

public virtual void Close()
{
    if (_owner != null) { _owner.Dispose(); _owner = null; }  // Destroy + Release 동시
    else Destroy(gameObject);                                 // 직접 배치된 경우 폴백
}
```

```csharp
// 호출부: 로드한 쪽이 소유권을 넘긴다
var inst  = await AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.AlertPopup, root, ct);
var popup = inst.GameObject.GetComponent<AlertPopup>();
popup.SetAddressableOwner(inst);        // 이후 popup.Close() 하나로 핸들까지 정리된다
popup.Setup("오류", message, glow: PopupGlowType.Danger);
```

**닫는 주체(팝업 자신)와 해제 주체가 일치**하는 것이 요점이다. 팝업은 "닫을 때 내 소유물을 정리한다"만 알면 되고, 그게 Addressable인지 아닌지는 몰라도 된다.

> 이건 [11](./chapter-11-socket-session-entry.md) 7절에서 겪은 문제의 예방책이기도 하다 — 거기서는 **로딩 중인 핸들을 제3자가 해제**해서 예외가 났다. 소유권이 명시되면 "누가 해제해도 되는가"에 답이 생긴다.

## 5. 프리팹을 고치지 않고 동작을 붙인다

딤 배경 클릭으로 닫기 위해 `Button`을 프리팹에 추가하는 대신, 코드로 붙였다.

```csharp
protected override void Awake()
{
    var trigger = backgroundImage.gameObject.AddComponent<EventTrigger>();
    var entry   = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
    entry.callback.AddListener(_ => Close());
    trigger.triggers.Add(entry);
}
```

프리팹은 **바이너리에 가까운 YAML**이라 변경이 diff로 잘 읽히지 않고 병합 충돌도 잦다. 배경 Image는 이미 `raycastTarget`이 켜져 있어 클릭을 받을 수 있었으므로, **동작만 코드로 얹는 쪽이 추적 가능**했다. 창(Window)이 배경 위에 있어서 창 클릭은 배경으로 내려가지 않는다.

## 6. 상태를 색으로 — 기본값을 팝업 종류마다 다르게

```csharp
public enum PopupGlowType { Info, Success, Warning, Danger }   // Blue / Green / Yellow / Red
```

`BasePopup`이 `Sprite[4]`를 들고 인덱스로 교체한다. 새 색을 넣으려면 enum 값 하나 + 인스펙터 연결이면 된다.

기본값이 팝업 종류마다 다르다 — `Alert`=Info, `Confirm`=Warning, `Warning`=Danger. **호출자가 아무것도 지정하지 않아도 의미에 맞는 색이 나오고**, 필요할 때만 덮어쓴다. "기본값을 안전한 쪽으로" 두면 호출부가 조용해진다.

## 7. MVI와 팝업 — Model은 '무엇', View는 '어떻게'

```
LobbyModel   →  State.ErrorMessage 에 문자열을 담기만 한다 (팝업을 모른다)
LobbyViewController  →  State 를 구독하다가 에러가 바뀌면 AlertPopup 을 띄운다
```

Model이 팝업을 직접 띄우면 **표현 방식이 도메인 로직에 박힌다** — 나중에 토스트로 바꾸거나, 테스트에서 UI 없이 돌릴 수 없다.

여기에 상태 기반 구독의 함정이 하나 있다.

```csharp
var prevError = (string)null;
_model.State.Subscribe(s => {
    if (s.ErrorMessage != null && s.ErrorMessage != prevError)   // ← 직전 값과 비교
        ShowErrorPopupAsync(s.ErrorMessage).Forget();
    prevError = s.ErrorMessage;
});
```

**상태는 이벤트가 아니다.** 다른 필드가 바뀌어도 State는 다시 흐르고, 그때마다 같은 `ErrorMessage`가 들어 있다. 비교 없이 구독하면 **팝업이 반복해서 뜬다.** 상태를 이벤트처럼 쓰려면 "무엇이 바뀌었는가"를 직접 판정해야 한다.

같은 원리로 확인 팝업도 Intent 전송 앞단에만 끼웠다 — 버튼 → 확인 팝업 → `onConfirm`에서 기존 Intent 발행. **Intent 코드 자체는 바뀌지 않는다.**

## 8. 그 이후

| 당시 | 현재 |
|---|---|
| 컨트롤러 3곳에서 사용 | **18개 파일**에서 사용 |
| `Dispose()`만 존재 | `SetOnDisposed(Action)` 콜백 추가 — 소유자가 해제 시점을 알 수 있게 |
| UI 프리팹 로딩 하나 | **자산 성격별로 세 갈래**로 분화 |

```
UI 프리팹        AddressableLoader.LoadAndInstantiateAsync   (비동기 + 소유권 객체)
SO·카탈로그      LoadAssetAsync(...).WaitForCompletion()     (LifetimeScope 동기 등록, 로컬 번들)
맵·씬            MapLoader / GameSceneManager                (씬 수명에 위임)
```

"전부 같은 방식으로 로드한다"가 아니라 **수명과 시점이 다르면 도구도 다르다**로 정리됐다. 자세한 경위는 [20](./chapter-20-content-pipeline-addressables.md).

## 9. 남은 것

- **팝업 호출 보일러플레이트가 8곳에 반복된다** — `로드 → GetComponent → SetAddressableOwner → Setup` 4단계가 매번 똑같다. 이 챕터가 Addressable 계층에서 없앤 중복이 **팝업 계층에서 다시 생겼다.** `ShowAlertAsync(title, msg, glow)` 같은 헬퍼 하나면 흡수된다. (기능 결함은 아니고 정리 대상)

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 리소스는 소유 객체가 정리한다 | 로딩 중 제3자 해제 문제의 구조적 예방([11](./chapter-11-socket-session-entry.md) 7절) |
| 실패/취소를 null로 정규화 | 호출부가 예외 처리를 갖지 않는 로딩 규약 |
| Model은 상태만, View가 표현 | HUD 창 MVI 확장 전반([22](./chapter-22-hud-windows-mvi.md)) |
| 프리팹 대신 코드로 배선 | 프리팹 미배선이 만든 조용한 실패를 겪은 뒤 더 강해짐([29](./chapter-29-multiplayer-sync-invisible-failures.md) 2절) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-12-addressable-popup-system.md](../learning-log/chapter-12-addressable-popup-system.md)

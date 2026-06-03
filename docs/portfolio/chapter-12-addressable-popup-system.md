# 챕터 12 — Addressable 리소스 관리 & 공통 팝업 시스템

## 이번 챕터에서 한 일

챕터 11까지 소켓 세션 진입 흐름을 완성했다.  
이번 챕터에서는 UI 리소스 관리 구조를 정리하고, 게임 전반에서 재사용 가능한 공통 팝업 시스템을 구축했다.

구체적으로:

1. `AddressableInstance` / `AddressableLoader` — Addressable 핸들 + 인스턴스 소유권 일원화
2. 기존 3개 컨트롤러의 중복 Addressable 로드 패턴 교체
3. `AlertPopup` / `ConfirmPopup` / `WarningPopup` 완성 (TMP, Glow 상태, BackGround 클릭 닫기)
4. `LobbyViewController` — MVI 에러 상태 → `AlertPopup` 자동 표시
5. `DungeonRoomDetailView` — 방 나가기 → `ConfirmPopup` 확인 흐름

---

## 문제 1 — Addressable 핸들 누수 위험

### 기존 코드의 구조

`LobbyViewController`, `GameHudController`, `DungeonRoomLobbyView`는 각자 같은 패턴을 반복하고 있었다.

```csharp
// 필드가 두 개 필요
private AsyncOperationHandle<GameObject> _lobbyHandle;
private GameObject _lobbyInstance;

// 로드 시 — 10줄 이상
_lobbyHandle = Addressables.LoadAssetAsync<GameObject>(key);
await _lobbyHandle.Task.AsUniTask().AttachExternalCancellation(_cts.Token);
if (_cts.IsCancellationRequested)
{
    if (_lobbyHandle.IsValid()) Addressables.Release(_lobbyHandle);
    return;
}
_lobbyInstance = Instantiate(_lobbyHandle.Result, parent);

// 해제 시
if (_lobbyInstance != null) Destroy(_lobbyInstance);
if (_lobbyHandle.IsValid()) Addressables.Release(_lobbyHandle);
```

세 곳 모두 이 패턴이 반복됐고, 두 필드가 분리돼 있다 보니 한쪽만 해제하면 누수가 생기는 구조였다.

### 왜 위험한가

Unity Addressables는 **Ref Count 기반**으로 동작한다.

```
LoadAssetAsync("A.prefab")  → ref count +1
Release(handle)             → ref count -1 → 0이 되면 실제 언로드
```

`AsyncOperationHandle`과 `GameObject`를 따로 관리하면:
- Destroy만 하고 Release를 빠뜨리면 ref count가 내려가지 않아 메모리에 남는다.
- Double-release하면 ref count 음수 → 크래시.

두 필드를 **하나의 객체가 함께 소유**하도록 만들면 이 문제가 해결된다.

### 해결 — AddressableInstance

```csharp
public sealed class AddressableInstance : IDisposable
{
    public GameObject GameObject { get; }
    private readonly AsyncOperationHandle<GameObject> _handle;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (GameObject != null) Object.Destroy(GameObject);
        if (_handle.IsValid()) Addressables.Release(_handle);
    }
}
```

`_disposed` 가드로 double-release를 방지하고,  
`Dispose()` 한 번으로 Destroy + Release가 원자적으로 처리된다.

### AddressableLoader — 로드 쪽 보일러플레이트 제거

```csharp
public static async UniTask<AddressableInstance?> LoadAndInstantiateAsync(
    string key, Transform parent, CancellationToken ct)
{
    var handle = Addressables.LoadAssetAsync<GameObject>(key);
    try
    {
        await handle.Task.AsUniTask().AttachExternalCancellation(ct);
    }
    catch (OperationCanceledException)
    {
        if (handle.IsValid()) Addressables.Release(handle);
        return null;
    }
    // ...
    var go = Object.Instantiate(handle.Result, parent);
    return new AddressableInstance(go, handle);
}
```

취소 / 실패 시 핸들을 내부에서 정리하고 `null` 반환.  
호출자는 예외 처리 없이 null 체크 한 줄로 끝난다.

### 교체 후 코드

```csharp
// Before: 필드 2개 + 10줄 이상
// After:
private AddressableInstance? _lobbyInst;

_lobbyInst = await AddressableLoader.LoadAndInstantiateAsync(key, parent, ct);
if (_lobbyInst != null)
    _resolver.InjectGameObject(_lobbyInst.GameObject);

// 해제
_lobbyInst?.Dispose();
_lobbyInst = null;
```

**"null = 완전 해제"** 라는 단일 불변이 성립한다.  
`_inst != null`이면 열려 있고, `null`이면 닫혀 있다.

---

## 문제 2 — 공통 팝업 시스템 설계

### 요구 사항

- Alert(1버튼), Confirm(2버튼), Warning(2버튼 경고) 세 종류
- 팝업 종류와 상황에 따라 **배경 Glow 색상**이 달라져야 한다
- BackGround(딤 오버레이) 클릭 시 닫혀야 한다
- 팝업 자체가 Addressable로 로드되므로, **닫힐 때 핸들도 함께 해제**되어야 한다

### SetAddressableOwner 패턴

팝업은 자신이 Addressable로 로드됐는지 모른다.  
호출자가 로드 후 소유권을 넘겨주는 방식으로 해결했다.

```csharp
public class BasePopup : UIBehaviour
{
    private AddressableInstance _owner;

    public void SetAddressableOwner(AddressableInstance inst) => _owner = inst;

    public virtual void Close()
    {
        if (_owner != null)
        {
            _owner.Dispose(); // Destroy + Release 동시 처리
            _owner = null;
        }
        else
        {
            Destroy(gameObject); // Addressable 아닌 경우 fallback
        }
    }
}
```

호출 측:
```csharp
var inst = await AddressableLoader.LoadAndInstantiateAsync(key, parent, ct);
var popup = inst.GameObject.GetComponent<AlertPopup>();
popup.SetAddressableOwner(inst); // 소유권 이전
popup.Setup("오류", message);
// 이제 popup.Close() 시 핸들까지 자동 해제
```

### BackGround 클릭 닫기 — EventTrigger 동적 추가

Button 컴포넌트를 프리팹에 추가하는 대신, `Awake`에서 `EventTrigger`를 코드로 붙였다.  
프리팹 YAML을 건드리지 않아도 되고, BackGround는 이미 `m_RaycastTarget: 1`이라 클릭이 통한다.

```csharp
protected override void Awake()
{
    base.Awake();
    if (backgroundImage != null)
    {
        var trigger = backgroundImage.gameObject.AddComponent<EventTrigger>();
        var entry   = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => Close());
        trigger.triggers.Add(entry);
    }
}
```

Window가 BackGround 위에 있으므로 Window 클릭은 BackGround에 전달되지 않는다.  
딤 영역 클릭 시에만 닫힌다.

### PopupGlowType — 상태별 Glow 색상

팝업 배경 Image의 Sprite를 런타임에 교체하는 방식으로 색상을 제어한다.

```csharp
public enum PopupGlowType
{
    Info    = 0, // Blue  — 일반 알림
    Success = 1, // Green — 성공
    Warning = 2, // Yellow — 주의/확인
    Danger  = 3, // Red   — 오류/위험
}
```

`BasePopup`의 `glowSprites[4]` 배열은 프리팹에서 미리 연결.  
`Setup()` 호출 시 `ApplyGlow(type)`로 sprite를 교체한다.

```csharp
// AlertPopup
public void Setup(string titleText, string messageText,
    Action onOk = null, PopupGlowType glow = PopupGlowType.Info)
{
    title.text   = titleText;
    message.text = messageText;
    okButton.onClick.AddListener(OnOkClicked);
    ApplyGlow(glow);
}
```

기본값이 팝업 종류별로 다르다:
- `AlertPopup` → `Info(Blue)`
- `ConfirmPopup` → `Warning(Yellow)`
- `WarningPopup` → `Danger(Red)`

호출자가 기본값을 따르면 자연스럽고, 필요하면 오버라이드할 수 있다.

---

## 문제 3 — MVI와 팝업 연동

### 에러 팝업 — LobbyViewController

`LobbyModel`은 실패 시 `LobbyState.ErrorMessage`에 문자열을 세팅한다.  
View(컨트롤러)가 이 상태를 구독해서 팝업을 띄운다.

```csharp
// LobbyViewController.Initialize()
var prevError = (string)null;
_model.State
    .Subscribe(s =>
    {
        if (s.ErrorMessage != null && s.ErrorMessage != prevError)
            ShowErrorPopupAsync(s.ErrorMessage).Forget();
        prevError = s.ErrorMessage;
    })
    .AddTo(_cts.Token);

private async UniTaskVoid ShowErrorPopupAsync(string error)
{
    var inst = await AddressableLoader.LoadAndInstantiateAsync(
        AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, _cts.Token);
    if (inst == null) return;

    var popup = inst.GameObject.GetComponent<AlertPopup>();
    popup.SetAddressableOwner(inst);
    popup.Setup("오류", error, glow: PopupGlowType.Danger);
}
```

Model은 에러를 `ErrorMessage`에 담기만 하고, 팝업을 어떻게 표시할지는 모른다.  
View(컨트롤러)가 상태 변화를 관찰해서 팝업을 띄우는 MVI 책임 분리가 유지된다.

### 방 나가기 확인 — DungeonRoomDetailView

기존 코드는 버튼 클릭 → 즉시 `LeaveRoom` Intent를 보냈다.  
실수로 방을 나가는 경우를 막기 위해 `ConfirmPopup`을 사이에 끼웠다.

```csharp
// Before
m_backButton.onClick.AddListener(() =>
    _model.Accept(LobbyIntent.LeaveRoom.Instance));

// After
m_backButton.onClick.AddListener(() => ShowLeaveConfirmAsync().Forget());

private async UniTaskVoid ShowLeaveConfirmAsync()
{
    var inst = await AddressableLoader.LoadAndInstantiateAsync(
        AddressKeys.UI.ConfirmPopup, transform.root, destroyCancellationToken);
    if (inst == null) return;

    var popup = inst.GameObject.GetComponent<ConfirmPopup>();
    popup.SetAddressableOwner(inst);
    popup.Setup("방 나가기", "정말 방을 나가시겠습니까?",
        onConfirm: () => _model.Accept(LobbyIntent.LeaveRoom.Instance));
}
```

View가 직접 LeaveRoom을 보내는 것은 그대로지만,  
확인 단계를 추가해도 Intent 전송 코드 자체는 변하지 않는다.

---

## 설계 결정 요약

| 결정 | 이유 |
|------|------|
| 핸들 + 인스턴스를 `AddressableInstance` 하나로 소유 | "null = 완전 해제" 불변 성립. 분리 시 누수 위험 |
| `SetAddressableOwner` — 팝업이 소유권을 받는 구조 | 팝업 스크립트가 Addressable를 알 필요 없음. 단독 사용도 가능 |
| EventTrigger 동적 추가 (BackGround 클릭) | 프리팹에 Button 컴포넌트 추가 없이 동작. YAML 변경 최소화 |
| `PopupGlowType` enum + `Sprite[]` 배열 | 새 색상 추가 시 enum 값 하나 + Inspector 연결만 하면 됨 |
| 에러 팝업을 `LobbyViewController`에서 관찰 | Model은 상태만 발행. View가 팝업 방식을 결정하는 MVI 원칙 유지 |

---

## 파일 위치

| 파일 | 역할 |
|------|------|
| `GUI/Util/AddressableInstance.cs` | 핸들 + 인스턴스 소유권 |
| `GUI/Util/AddressableLoader.cs` | 로드 + 인스턴스화 유틸 |
| `GUI/Common/Popups/BasePopup.cs` | BackGround 클릭 닫기, Glow 교체, SetAddressableOwner |
| `GUI/Common/Popups/PopupGlowType.cs` | 상태별 색상 enum |
| `GUI/Common/Popups/AlertPopup.cs` | 1버튼 알림 팝업 |
| `GUI/Common/Popups/ConfirmPopup.cs` | 2버튼 확인 팝업 |
| `GUI/Common/Popups/WarningPopup.cs` | 2버튼 경고 팝업 |
| `GUI/LobbyViewController.cs` | 에러 상태 → AlertPopup 자동 표시 |
| `GUI/DungeonLobby/DungeonRoomDetail/DungeonRoomDetailView.cs` | 방 나가기 → ConfirmPopup |

---

## 핵심 키워드

- **Addressable Ref Count** — `LoadAssetAsync` N번 = `Release` N번 필요. 한 번이라도 빠지면 누수
- **AddressableInstance** — 핸들 + 인스턴스를 하나로 소유. `_disposed` 가드로 double-release 방지
- **SetAddressableOwner** — 팝업이 소유권을 받는 패턴. 닫힐 때 Destroy + Release 원자 처리
- **EventTrigger 동적 추가** — `Awake`에서 `AddComponent<EventTrigger>()`. 프리팹 YAML 무수정
- **PopupGlowType** — 팝업 상태를 enum으로 표현. `Sprite[]` 배열 인덱스로 Glow sprite 교체
- **MVI 에러 관찰** — Model은 `ErrorMessage` 상태만 발행. Controller가 변화를 감지해 팝업 띄움

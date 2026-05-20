# 챕터 10 학습 로그 — MVI 아키텍처 (던전 로비 OutGame UI)

## 왜 MVI를 선택했는가

챕터 9에서 Unity gRPC 통신 계층과 VContainer DI 구조가 완성됐다.  
다음 문제는 **UI 계층을 어떻게 설계할 것인가**였다.

던전 로비 화면의 요구사항은 이랬다.

- 방 목록이 실시간으로 바뀐다 (SubscribeRoom 스트림 수신)
- 방 생성 / 방 입장 / 새로고침이 동시에 눌릴 수 있다
- 네트워크 요청 중에는 로딩 상태를 표시해야 한다
- 입장 성공 시 화면 전환이 이루어진다

처음에는 MonoBehaviour에서 직접 `IDungeonLobbyService`를 호출하는 방식을 생각했다.  
문제는 "비동기 요청이 겹치면 어떻게 막는가"와 "어느 조건에서 로딩을 표시하고 언제 끄는가"를 MonoBehaviour 안에서 모두 관리하면 금방 복잡해진다는 점이었다.

MVI를 선택한 이유:

- **단방향 데이터 흐름** — 상태가 어디서 왔는지 항상 추적 가능
- **불변 상태** — 현재 UI가 어떤 상태를 보여주고 있는지 언제나 하나의 객체로 설명 가능
- **순수 Reducer** — 상태 전이 로직을 테스트 가능한 함수로 분리
- **View의 역할 최소화** — View는 상태를 렌더링하고 Intent를 만드는 것 외에 아무것도 하지 않음

---

## MVI 흐름 전체 구조

```
[ View ]
   │  사용자 입력을 Intent로 변환
   │  model.Accept(intent)
   ▼
[ Model — Accept ]
   │  중복 처리 방지 (_isProcessing 확인)
   │  Effect(비동기 메서드) 실행
   ▼
[ Effect ]
   │  Repository를 통해 네트워크 호출
   │  성공/실패 결과를 Result로 포장
   │  Dispatch(result) 호출
   ▼
[ Dispatch → Reducer ]
   │  LobbyReducer.Reduce(현재State, Result) → 새 State
   │  _state.Value = newState
   ▼
[ ReactiveProperty<LobbyState> ]
   │  값이 바뀌면 자동으로 구독자에게 알림
   ▼
[ View — Render(state) ]
   │  State를 받아 UI를 그린다
   │  직접 판단하지 않는다 — State가 시키는 대로만 표시
```

---

## 구성 요소별 역할과 구현

### Intent — 사용자 의도의 닫힌 집합

```csharp
public abstract class LobbyIntent
{
    private LobbyIntent() { }   // 외부 상속 차단 (Discriminated Union 패턴)

    public sealed class LoadRooms : LobbyIntent
    {
        public static readonly LoadRooms Instance = new LoadRooms();
        private LoadRooms() { }
    }

    public sealed class CreateRoom : LobbyIntent
    {
        public readonly string Name;
        public readonly int MaxPlayers;
        public CreateRoom(string name, int maxPlayers) { ... }
    }

    public sealed class JoinRoom : LobbyIntent
    {
        public readonly long RoomId;
        public JoinRoom(long roomId) { ... }
    }
}
```

**설계 의도:**

- `private LobbyIntent()` — 이 파일 밖에서 새로운 Intent를 만들 수 없다.
  View가 "정해진 것" 외의 의도를 만들지 못하게 막는다.
- `LoadRooms.Instance` — 데이터가 없는 Intent는 싱글톤으로 만들어 매번 할당을 피한다.
- View는 오직 Intent를 만들 뿐, 처리 방법은 전혀 모른다.

---

### Result — Effect 출력의 닫힌 집합

```csharp
public abstract class LobbyResult
{
    private LobbyResult() { }

    public sealed class Loading : LobbyResult { ... }       // 요청 시작
    public sealed class RoomsLoaded : LobbyResult { ... }  // 목록 조회 성공
    public sealed class RoomCreated : LobbyResult { ... }  // 방 생성 성공
    public sealed class RoomJoined  : LobbyResult { ... }  // 방 입장 성공
    public sealed class Failed      : LobbyResult { ... }  // 실패 (공통)
}
```

**설계 의도:**

- Intent와 동일한 Discriminated Union 패턴.
- Result는 Reducer만 받는다 — View에 직접 노출되지 않는다.
- `Loading`도 Result다. "요청을 시작했다"는 사실도 상태 전이의 입력이다.

---

### Reducer — 순수 함수

```csharp
public static class LobbyReducer
{
    public static LobbyState Reduce(LobbyState state, LobbyResult result)
    {
        if (result is LobbyResult.Loading)
            return state.WithLoading();

        if (result is LobbyResult.RoomsLoaded loaded)
            return state.WithRoomsLoaded(loaded.Rooms);

        if (result is LobbyResult.RoomCreated created)
            return state.WithRoomAdded(created.Room);

        if (result is LobbyResult.RoomJoined joined)
            return state.WithRoomUpdated(joined.Room);

        if (result is LobbyResult.Failed failed)
            return state.WithError(failed.Message);

        return state;
    }
}
```

**설계 의도:**

- `static` — 인스턴스 없음. 의존성 없음.
- 입력은 (OldState, Result), 출력은 NewState뿐. 이 함수 안에서 네트워크 호출, `Time.time`, `Random` 금지.
- 순수 함수이기 때문에 인자만 있으면 어디서든 단위 테스트 가능.

```csharp
// 테스트 예시
var state = LobbyState.Initial;
var result = new LobbyResult.Failed("방이 가득 찼습니다");
var next = LobbyReducer.Reduce(state, result);
Assert.AreEqual("방이 가득 찼습니다", next.ErrorMessage);
```

---

### State — 불변 스냅샷

```csharp
public sealed class LobbyState
{
    public readonly IReadOnlyList<DungeonRoomModel> Rooms;
    public readonly bool IsLoading;
    public readonly string ErrorMessage;   // null = 에러 없음

    public static readonly LobbyState Initial =
        new LobbyState(new DungeonRoomModel[0], false, null);

    // 새 State 생성은 반드시 WithXxx 메서드를 통해
    public LobbyState WithLoading()     => new LobbyState(Rooms, true, null);
    public LobbyState WithError(string message) => new LobbyState(Rooms, false, message);
    public LobbyState WithRoomsLoaded(...) => ...
    public LobbyState WithRoomAdded(...)   => ...
    public LobbyState WithRoomUpdated(...) => ...
}
```

**설계 의도:**

- `readonly` 필드 — 생성 후 변경 불가. 새 State가 필요하면 새 객체를 만든다.
- Unity 버전이 낮아 `record` 사용 불가 → `sealed class + readonly + WithXxx factory` 패턴으로 불변성 표현.
- View는 State 안의 값을 읽기만 한다. State 객체를 직접 수정하는 코드는 존재할 수 없다.

---

### Model — MVI의 중심

```csharp
public sealed class LobbyModel : IInitializable, IDisposable
{
    private readonly LobbyRepository _repository;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly ReactiveProperty<LobbyState> _state
        = new ReactiveProperty<LobbyState>(LobbyState.Initial);
    private readonly Subject<long> _navigateToRoom = new Subject<long>();
    private bool _isProcessing;

    public ReadOnlyReactiveProperty<LobbyState> State => _state.ToReadOnlyReactiveProperty();
    public Observable<long> NavigateToRoom => _navigateToRoom;
```

**View의 단일 진입점:**

```csharp
public void Accept(LobbyIntent intent)
{
    if (_isProcessing) return;   // 동시 요청 차단

    switch (intent)
    {
        case LobbyIntent.LoadRooms _:    LoadRoomsAsync().Forget();    break;
        case LobbyIntent.CreateRoom c:   CreateRoomAsync(c).Forget();  break;
        case LobbyIntent.JoinRoom j:     JoinRoomAsync(j).Forget();    break;
    }
}
```

**내부 상태 업데이트 (View에 노출하지 않음):**

```csharp
private void Dispatch(LobbyResult result)
{
    _state.Value = LobbyReducer.Reduce(_state.Value, result);
}
```

**설계 의도:**

- `Accept` — View가 부르는 메서드. 이름에서 "Model이 의도를 받아들인다"는 MVI 의미를 표현.
- `Dispatch` — Model 내부에서만 호출. View가 직접 Result를 만들어 넣을 수 없다.
- `_isProcessing` — 비동기 처리 중 중복 Intent 무시. 로딩 중에 새로고침 버튼을 연타해도 안전.
- `IInitializable` — VContainer가 씬 시작 시 `Initialize()`를 자동 호출. 초기 방 목록 로드가 여기서 시작.
- `IDisposable` — 씬 종료 시 CancellationToken 취소, ReactiveProperty Dispose.

---

### View — 렌더링과 Intent 발행만

```csharp
// State 구독
_model.State.Subscribe(Render, destroyCancellationToken);

// Intent 발행
refreshButton.onClick.AddListener(() =>
    _model.Accept(LobbyIntent.LoadRooms.Instance));

createRoomButton.onClick.AddListener(() =>
    _model.Accept(new LobbyIntent.CreateRoom(roomNameInput.text, 4)));
```

```csharp
private void Render(LobbyState state)
{
    loadingPanel.SetActive(state.IsLoading);
    errorPanel.SetActive(state.ErrorMessage != null);
    roomListPanel.SetActive(!state.IsLoading);

    if (state.ErrorMessage != null)
        errorText.text = state.ErrorMessage;

    if (!state.IsLoading)
        SyncRoomList(state.Rooms);
}
```

**설계 의도:**

- View는 `if (state.IsLoading)` 같은 조건 분기만 한다. 로딩 상태를 **결정하는** 코드는 없다.
- `destroyCancellationToken` — R3 NuGet 버전에는 `AddTo(MonoBehaviour)` 없음.
  Unity 6부터 기본 제공되는 `destroyCancellationToken`을 대신 사용.
- Diff 방식 목록 갱신: 방 전체를 지우고 다시 그리지 않고, 변경된 항목만 Update.

---

### DungeonRoomModel — proto 래퍼

```csharp
public sealed class DungeonRoomModel
{
    public readonly RoomInfo Info;
    public DungeonRoomModel(RoomInfo info) { Info = info; }
}
```

**설계 의도:**

- proto 타입(`RoomInfo`)을 직접 State에 넣지 않고 래핑.
- 파생값(인원수 텍스트, 상태 텍스트 등)은 View에서 계산. Model에서 미리 계산한 문자열을 State에 담으면 View의 언어/포맷 결정을 Model이 침범하게 된다.
- 나중에 서버 proto 스키마가 바뀌어도 이 래퍼 하나만 고치면 된다.

---

## MVI를 적용하면서 실제로 배운 점

### "불변이면 매번 새 객체를 만드는 거야?"

처음엔 이게 낭비처럼 보였다.

실제 이유:

```
기존 방식 (mutable):
  state.IsLoading = true   → 어디서 바꿨는지 추적 불가

불변 방식:
  Dispatch(LobbyResult.Loading.Instance)
  → Reducer가 새 State 생성
  → ReactiveProperty에 할당 → 구독자에 전파
```

"바뀌면 새 객체"가 가능한 이유는 ReactiveProperty가 **레퍼런스 비교**로 변경을 감지하기 때문이다.  
새 객체 = 이전과 다른 레퍼런스 = 즉시 구독자 알림.  
GC 비용은 State 객체 하나로 씬 하나에서 수십 번 수준이라 무시 가능하다.

### "Dispatch는 왜 View에 안 열어?"

처음에 `Dispatch`를 `public`으로 설계하려고 했다.

View가 직접 `model.Dispatch(new LobbyResult.RoomsLoaded(...))` 를 부를 수 있으면,  
View가 Result를 만든다는 뜻이다. 그러면 "Effect 없이 State를 직접 조작"하는 경로가 열린다.

- Intent → Effect → Result → Reducer → State 흐름이 깨진다
- 비동기 처리를 거치지 않은 State 변경이 발생할 수 있다

그래서 `Dispatch`는 `private`. View는 `Accept`만 부를 수 있다.

### "ViewModel이라고 부르면 안 되나?"

초기에 `LobbyViewModel`로 이름을 지었다가 수정했다.

- `ViewModel`은 MVVM의 용어 — 양방향 바인딩, Command 패턴이 핵심
- MVI의 Model은 단방향 흐름의 관리자 — 역할이 다르다
- 이름이 아키텍처 의도를 설명해야 한다

결론: `LobbyModel`.

---

## 다음 단계로 연결

이 MVI 구조 위에 챕터 10의 나머지 작업이 올라간다.

- SubscribeRoom 스트림 이벤트 → `OnRoomUpdated` → Model이 `Dispatch(new LobbyResult.RoomsLoaded(...))` 호출 → State 자동 갱신
- 방 입장 성공 → `NavigateToRoom` Observable → Router가 구독해서 InRoom 화면 전환
- InRoom 화면도 동일한 MVI 패턴으로 구성

---

## 참고 경로

| 역할 | 경로 |
|------|------|
| LobbyIntent | `Client/Assets/Script/OutGame/DungeonLobby/LobbyIntent.cs` |
| LobbyResult | `Client/Assets/Script/OutGame/DungeonLobby/LobbyResult.cs` |
| LobbyReducer | `Client/Assets/Script/OutGame/DungeonLobby/LobbyReducer.cs` |
| LobbyState | `Client/Assets/Script/OutGame/DungeonLobby/LobbyState.cs` |
| LobbyModel | `Client/Assets/Script/OutGame/DungeonLobby/LobbyModel.cs` |
| DungeonRoomModel | `Client/Assets/Script/OutGame/DungeonLobby/DungeonRoomModel.cs` |
| LobbyRepository | `Client/Assets/Script/OutGame/DungeonLobby/LobbyRepository.cs` |
| LobbyView | `Client/Assets/Script/GUI/OutGame/Lobby/LobbyView.cs` |
| RoomItemView | `Client/Assets/Script/GUI/OutGame/Lobby/RoomItemView.cs` |
| OutGameLifetimeScope | `Client/Assets/Script/VContainer/Installers/Scenes/OutGameLifetimeScope.cs` |

# 챕터 10 학습 로그 — Unity 클라이언트 레이어 분리

## 왜 레이어를 분리하는가

Unity 프로젝트에서 모든 코드를 하나의 어셈블리(`Assembly-CSharp`)에 넣으면 처음엔 빠르다.  
문제는 규모가 커질수록 이 세 가지가 무너진다.

1. **순환 참조** — UI가 네트워크를 직접 부르고, 네트워크 코드가 UI 타입을 알고 있는 상황
2. **테스트 불가** — 특정 클래스를 테스트하려면 Unity 엔진 전체를 올려야 함
3. **변경 전파** — proto 파일 하나가 바뀌면 UI 코드까지 컴파일 에러가 남

레이어를 Assembly Definition(`.asmdef`)으로 분리하면 이 문제를 **컴파일 타임에** 잡는다.  
잘못된 방향으로 참조하면 빌드 자체가 안 된다.

---

## 레이어 구조 전체

```
┌──────────────────────────────────────────────────────┐
│  Game.GUI                                            │
│  (LobbyView, RoomItemView)                           │
│  — UI 렌더링, Intent 발행만                           │
└───────────────┬──────────────────────────────────────┘
                │ 참조
┌───────────────▼──────────────────────────────────────┐
│  Game.OutGame                                        │
│  (LobbyModel, LobbyState, LobbyIntent, LobbyResult, │
│   LobbyReducer, LobbyRepository, DungeonRoomModel)   │
│  — MVI 흐름 전체 관리                                 │
└───────┬───────────────────┬──────────────────────────┘
        │ 참조               │ 참조
┌───────▼────────┐   ┌──────▼───────────────────────────┐
│  Game.System   │   │  Game.Network                    │
│  (DungeonLobby │   │  (DungeonLobbyGrpcService,        │
│   Service,     │   │   proto 생성 타입,                 │
│   Session,     │   │   GrpcChannelProvider)            │
│   IDungeon     │   │  — 서버 통신 전용                  │
│   LobbyService)│   └──────────────────────────────────┘
│  — 도메인 오케  │
│    스트레이션   │
└────────────────┘
        ▲
        │ 공통 등록
┌───────┴──────────────────────────────────────────────┐
│  Game.VContainer                                     │
│  (ProjectLifetimeScope, OutGameLifetimeScope)        │
│  — DI 구성만. 실제 로직 없음                           │
└──────────────────────────────────────────────────────┘
```

---

## 레이어별 상세 설명

### Game.Network — 서버 통신 전용

```json
{
    "name": "Game.Network",
    "autoReferenced": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "Google.Protobuf.dll",
        "Grpc.Core.dll",
        "Grpc.Core.Api.dll",
        ...
    ]
}
```

**포함하는 것:**
- `GrpcChannelProvider` — HTTP/2(h2c) 채널 생성
- `DungeonLobbyGrpcService` — proto stub 호출
- proto로 자동 생성된 타입 (`RoomInfo`, `RoomStatusType` 등)

**허용하는 참조:** VContainer, UniTask  
**금지하는 것:** UnityEngine.UI, MonoBehaviour, 게임 로직

**`autoReferenced: false`인 이유:**  
proto dll과 gRPC dll을 `precompiledReferences`로 명시하는 어셈블리는 자동 참조를 끄는 게 안전하다.  
필요한 어셈블리만 명시적으로 `Game.Network`를 참조하게 강제한다.

---

### Game.System — 도메인 오케스트레이션

```json
{
    "name": "Game.System",
    "rootNamespace": "Game.System"
}
```

**포함하는 것:**
- `IDungeonLobbyService` — 인터페이스 (Application 레이어 역할)
- `DungeonLobbyService` — gRPC 호출 결과를 `DungeonLobbyResult`로 변환, 스트림 구독 관리
- `DungeonLobbySession` — 현재 참가 방의 런타임 상태 보관
- `DungeonLobbyResult` — 도메인 결과 열거형

**허용하는 참조:** Game.Network (IDungeonLobbyGrpcService 주입받아 사용)  
**금지하는 것:** UI, VContainer(등록은 Game.VContainer 담당)

**설계 포인트:**  
`DungeonLobbyService`는 proto 타입(`RoomInfo`)을 그대로 반환한다.  
도메인 모델로 변환하는 책임은 `Game.OutGame`의 `LobbyRepository`가 맡는다.

---

### Game.OutGame — MVI 흐름 전체

```json
{
    "name": "Game.OutGame",
    "rootNamespace": "Game.OutGame",
    "references": ["Game.System", "Game.Network", "UniTask", "VContainer", "R3"],
    "autoReferenced": true
}
```

**포함하는 것:**
- `LobbyIntent`, `LobbyResult`, `LobbyReducer`, `LobbyState` — MVI 구성요소
- `LobbyModel` — Intent 수신, Effect 실행, State 발행
- `LobbyRepository` — `IDungeonLobbyService`를 래핑, 결과를 `(bool, Data, Error)`로 정규화
- `DungeonRoomModel` — `RoomInfo` proto 래퍼

**허용하는 참조:** Game.System, Game.Network (proto 타입 직접 사용)  
**금지하는 것:** UnityEngine.UI, MonoBehaviour 직접 조작

**`autoReferenced: true`인 이유:**  
`Game.GUI`와 `Game.VContainer` 두 곳에서 참조한다.  
자동 참조를 켜두면 이름 기반으로 양쪽에서 쉽게 접근 가능.

**LobbyRepository가 하는 일:**

```csharp
// IDungeonLobbyService 반환 타입: (DungeonLobbyResult, IReadOnlyList<RoomInfo>)
// LobbyRepository 반환 타입: (bool IsSuccess, IReadOnlyList<RoomInfo> Rooms, string Error)

public async UniTask<(bool IsSuccess, IReadOnlyList<RoomInfo> Rooms, string Error)>
    GetRoomsAsync(CancellationToken ct = default)
{
    var (result, rooms) = await _service.GetRoomsAsync(ct);
    return result == DungeonLobbyResult.Success
        ? (true, rooms, null)
        : (false, null, result.ToString());
}
```

LobbyModel이 `DungeonLobbyResult` 열거형을 직접 해석하지 않아도 된다.  
성공/실패 판단은 Repository에서 끝나고, Model은 `IsSuccess`만 보면 된다.

---

### Game.GUI — UI 렌더링과 Intent 발행

```json
{
    "name": "Game.GUI",
    "references": [
        "...기존 3개 GUID...",
        "Game.OutGame",
        "Game.Network"
    ]
}
```

**포함하는 것:**
- `LobbyView` — State를 받아 UI 렌더링, 버튼 이벤트를 Intent로 변환
- `RoomItemView` — 방 1개 UI 아이템

**허용하는 참조:** Game.OutGame (LobbyModel, LobbyState, LobbyIntent), Game.Network (RoomStatusType 직접 사용)  
**금지하는 것:** Game.System 직접 참조, IDungeonLobbyService 직접 호출

**Game.Network를 직접 참조하는 이유:**  
`RoomItemView`에서 `RoomStatusType`(proto enum)으로 표시 텍스트를 결정한다.  
이 파생값 계산은 View의 책임이기 때문에 proto 타입을 직접 참조한다.

```csharp
private static string ToStatusText(RoomStatusType status)
{
    switch (status)
    {
        case RoomStatusType.Waiting:  return "대기 중";
        case RoomStatusType.Playing:  return "게임 중";
        ...
    }
}
```

---

### Game.VContainer — DI 구성 전용

```json
{
    "name": "Game.VContainer",
    "references": [
        "...기존 5개 GUID...",
        "Game.OutGame"
    ]
}
```

**포함하는 것:**
- `ProjectLifetimeScope` — 전역 싱글톤 등록 (AuthSession, IDungeonLobbyService 등)
- `OutGameLifetimeScope` — OutGame 씬 전용 스코프

**OutGameLifetimeScope:**

```csharp
public class OutGameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<LobbyRepository>(Lifetime.Scoped);
        builder.RegisterEntryPoint<LobbyModel>(Lifetime.Scoped);
    }
}
```

**`RegisterEntryPoint`가 하는 일:**  
`IInitializable` 구현체를 등록하면 VContainer가 씬 시작 시 자동으로 `Initialize()`를 호출한다.  
`LobbyModel.Initialize()`에서 `Accept(LobbyIntent.LoadRooms.Instance)` → 씬 진입 시 방 목록 자동 로드.

**금지하는 것:** 실제 게임 로직, 네트워크 호출

---

## 의존성 방향 규칙

```
허용:   GUI → OutGame → System → Network
        VContainer → OutGame

금지:   Network → System     (하위 레이어가 상위 참조)
        System → OutGame     (도메인이 UI 레이어 참조)
        OutGame → GUI        (MVI Model이 View 참조)
        System → VContainer  (도메인이 DI 설정 참조)
```

이 규칙을 `.asmdef`로 강제했기 때문에 방향이 틀린 참조를 추가하면 **Unity 에디터에서 즉시 컴파일 에러**가 난다.

---

## 실제 데이터 흐름 예시 — 방 목록 조회

```
[Unity 씬 시작]
       │
       ▼
OutGameLifetimeScope.Configure()
  → builder.RegisterEntryPoint<LobbyModel>

       │ VContainer가 씬 시작 후 자동 호출
       ▼
LobbyModel.Initialize()
  → Accept(LobbyIntent.LoadRooms.Instance)

       │
       ▼
LoadRoomsAsync() [Effect]
  → LobbyRepository.GetRoomsAsync()
    → IDungeonLobbyService.GetRoomsAsync()   [Game.System]
      → DungeonLobbyGrpcService.GetRoomsAsync()  [Game.Network]
        → gRPC GameServer 호출

       │ 응답 수신
       ▼
Repository: (DungeonLobbyResult.Success, rooms)
  → (true, rooms, null) 반환

       │
       ▼
Model: Dispatch(new LobbyResult.RoomsLoaded(rooms))
  → LobbyReducer.Reduce(state, result)
    → state.WithRoomsLoaded(rooms) → new LobbyState(...)

       │
       ▼
ReactiveProperty<LobbyState>.Value = newState

       │ 자동 알림
       ▼
LobbyView.Render(state)
  → SyncRoomList(state.Rooms)
    → 각 DungeonRoomModel에 대해 RoomItemView.Setup() 호출
```

---

## 레이어 분리로 얻은 것

### 1. 잘못된 참조를 코드 리뷰 전에 잡는다

`DungeonLobbyService`에서 실수로 `LobbyModel`을 참조하면  
`Game.System`이 `Game.OutGame`을 참조하는 것 → 컴파일 에러.  
코드 리뷰까지 가지 않는다.

### 2. 테스트 범위가 명확해진다

- `LobbyReducer` → 단위 테스트. 네트워크 없음, Unity 없음.
- `LobbyRepository` → `IDungeonLobbyService` Mock으로 교체 가능.
- `LobbyModel` → Repository Mock + 테스트용 State 확인.
- E2E → Docker 서버 + 실제 흐름.

### 3. 레이어별 변경 격리

서버 proto 스키마가 바뀌면 `Game.Network`만 바뀐다.  
`Game.OutGame`의 `DungeonRoomModel`에서 `RoomInfo`를 래핑하는 방식이 바뀔 수 있지만,  
`LobbyState`, `LobbyIntent`는 변경 없다.

---

## asmdef 믹스 참조 방식

`Game.GUI.asmdef`는 기존 GUID 참조와 이름 기반 참조를 혼용한다.

```json
{
    "name": "Game.GUI",
    "references": [
        "GUID:b0214a6008ed146ff8f122a6a9c2f6cc",
        "GUID:b25f5ef11ad5ac74faa603bbafead339",
        "GUID:f51ebe6a0ceec4240a699833d6309b23",
        "Game.OutGame",
        "Game.Network"
    ]
}
```

Unity는 GUID와 이름 기반 참조를 같은 배열에 혼용할 수 있다.  
GUID는 파일 이동에도 안전하고, 이름 기반은 가독성이 높다.  
새로 추가하는 참조는 이름 기반으로 추가하는 게 관리하기 쉽다.

---

## 참고 경로

| 어셈블리 | asmdef 경로 |
|---------|------------|
| Game.Network | `Client/Assets/Script/Network/Game.Network.asmdef` |
| Game.System | `Client/Assets/Script/System/Game.System.asmdef` |
| Game.OutGame | `Client/Assets/Script/OutGame/Game.OutGame.asmdef` |
| Game.GUI | `Client/Assets/Script/GUI/Game.GUI.asmdef` |
| Game.VContainer | `Client/Assets/Script/VContainer/Game.VContainer.asmdef` |

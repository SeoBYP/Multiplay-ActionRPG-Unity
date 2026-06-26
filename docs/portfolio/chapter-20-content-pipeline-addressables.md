# 챕터 20 학습 로그 — 던전 메타 + Addressables 데이터 파이프라인

> 두 개의 데이터 결정과 두 개의 운영 폴리시. **① 던전을 식별하는 키**를 "게임 시작 순간 휘발하는 파라미터"에서 "방에 영속되는 속성"으로 승격하고, **② 게임 데이터 SO를 Resources에서 Addressables로** 옮긴다.
> 핵심: 식별자는 *하나*로 통일하고(MapId), 데이터 로딩 도구는 *자산의 성격*에 맞춰 다르게 고른다(동적=async Addressables / 씬 카탈로그=WaitForCompletion / 저작 전용=로드 안 함). 그리고 비동기 초기화 순서와 실패 피드백이라는, 코어가 끝난 뒤에야 보이는 두 결함을 닫는다.

---

## 설계 결정과 근거

### 던전을 식별하는 키 — `DungeonId`가 아니라 `MapId`

방을 만들 때 어떤 던전을 플레이할지가 **방의 속성**이어야 하는데, 기존 구조에서는 그게 없었다. `StartRoom(map_id)`로 게임 시작 순간에만 실리고 방에는 안 남았다.

```
[Before]  StartRoom(map_id) ──▶ GameStartRequestedMessage.MapId ──▶ 스폰 레이아웃
          DungeonRoom 엔티티 : 맵 필드 없음  ✗  (시작 때만 실리고 휘발)

[After]   CreateRoom(map_id) ──▶ DungeonRoom.MapId 영속(DB + Redis)
                                      │  진실의 원천
          StartRoom ───────────▶ room.MapId ──▶ GameStartRequestedMessage.MapId
```

처음엔 부채 이름 그대로 `DungeonId`(별도 식별자)를 만들려 했다. 그런데 스폰/콘텐츠 시스템은 이미 전부 **`MapId`(string, `spawn-layouts.json`의 키 = `"dungeon_01"`)**로 통일돼 있었다. 여기에 숫자 `DungeonId`를 더하면 `DungeonId → MapId` 매핑이라는 *두 번째 식별자*가 생긴다 — 던전이 1종뿐인 지금은 순수 오버헤드(YAGNI). **식별자는 하나로 통일**하고 이름은 기존 어휘(`MapId`)를 따랐다.

> 부채 티켓의 이름(`DungeonId`)을 그대로 구현하지 않은 것. 이름은 "무엇을 가리키나"가 아니라 "이미 코드가 쓰는 어휘"에 맞춘다.

### 검증·기본값은 Domain이 아니라 Application 권위에

`MapId`의 유효성(빈값→기본 맵, 알 수 없는 맵→거부)은 누가 책임지나? `DungeonRoom`(Domain) 안에 넣으면 깔끔해 보이지만, 검증에는 `SpawnLayoutTable`(어떤 맵이 존재하나)이 필요하고 그건 `Shared.Infrastructure`다. **Domain이 Infrastructure를 알게 하는 레이어 역전**이 된다.

```
CreateRoom(gRPC) ─▶ CreateDungeonRoomAsync (Application)
                       ├─ 빈값?            → MapIds.Default
                       ├─ IsKnown(mapId)?  → 아니면 거부(InvalidRequest)
                       └─▶ DungeonRoom.Create(roomName, host, max, mapId)   ← Domain은 값 보관만
```

`DungeonRoom`은 받은 `mapId`를 *보관만* 한다(roomName/host처럼 직접 입력값은 검증하되, 서버가 정규화해 넘기는 mapId는 안 건드림). 검증은 소비자(Application)에 둔다 — DIP. 엔티티 필드 추가는 규칙대로 4곳(`Create`/`Clone`/`FromRedis` + Redis `ToHashEntry`/`ParseFromRedis`) 동시 수정 + EF 마이그레이션(`AddDungeonRoomMapId`, `varchar(64) NOT NULL`, 기존 행은 `dungeon_01`로 백필) + Redis Hash 필드. 구버전 캐시에 `MapId`가 없을 수 있어 **파싱 시 없으면 Default로 폴백**(거부하지 않음 — 하위호환).

### 던전 선택 UI — proto 계약을 열고 MVI를 관통시킨다

방 생성 시 던전을 고르려면 클라의 선택이 서버까지 흘러야 한다. proto에 `CreateRoomRequest.map_id`(생성)와 `RoomInfo.map_id`(표시)를 추가하고, 클라는 MVI 레이어를 그대로 관통시킨다.

```
[생성] 팝업 드롭다운(DungeonCatalog SO: mapId→표시이름)
   → LobbyIntent.CreateRoom(name, max, mapId)
   → LobbyModel → LobbyRepository → System.DungeonLobbyService
   → CreateRoomRequest.map_id ──▶ 서버 영속(DungeonRoom.MapId)
[표시] RoomInfo.map_id ──▶ DungeonRoomModel.MapId  (방 목록에 "어느 던전")
```

"어떤 던전을 고를 수 있고 화면에 뭐라 보이나"는 **표현 계층의 관심사**다. 서버는 이미 `IsKnown`으로 mapId를 권위 검증하므로, 선택지 메타(표시이름)는 클라 `DungeonCatalog`(SO)에 둔다 — 서버 RPC를 새로 만들지 않는다(YAGNI). 새 던전을 늘리는 건 데이터 작업뿐: `MapDefinition` SO를 만들고 Export → 서버 재빌드. 코드 변경 0.

### export가 데이터를 조용히 지우고 있었다 — `expReward`

던전을 데이터로 늘릴 수 있다는 걸 실증하려고 샘플 던전 `dungeon_02`를 SO로 만들고 Export 툴(`MapDataExporter`)을 돌렸다. 그런데 코드를 읽다 보니 **Export가 `dungeon_01`의 클리어 보상을 0으로 날리는 결함**이 있었다.

```
MapDataExporter.MapDto = { mapId, bounds, points, monsters }   ← expReward 없음!
   → SO들을 모아 JSON으로 bake → expReward 필드가 출력에서 누락
   → 누구든 Map Editor에서 Export하면 dungeon_01의 expReward:100 이 소실
```

`spawn-layouts.json`의 `expReward:100`은 **손편집으로만 유지**되던 상태였다(서버가 보상 산정에 읽는 값인데). `MapDefinition.expReward` 필드 + 익스포터 왕복(Export/Import) + `dungeon_01.asset`을 100으로 맞춰 SO↔JSON을 정합화했다. 새 던전을 본격적으로 만들기 *전에* 이걸 먼저 막지 않았으면, 콘텐츠를 늘리는 행위가 곧 기존 보상을 지우는 행위가 될 뻔했다.

> 교훈: "데이터를 추가하는 도구"가 "기존 데이터를 보존하는지"는 별개 검증이다. bake류 도구는 *왕복(round-trip)*이 보장돼야 안전하다.

### Resources를 버리고 Addressables로 — 왜, 그리고 자산별로 다른 도구

`Resources/` 폴더는 **빌드에 무조건 전부 포함**된다(온디맨드 아님). 던전·맵이 늘어날수록 안 쓰는 데이터까지 항상 번들에 들어간다. 그래서 게임 데이터 SO를 전부 `Resources` 밖(`Assets/GameData/<컨텐츠>/`)으로 옮기고 로딩을 Addressables로 바꿨다.

다만 **"전부 Addressables"가 항상 정답은 아니다.** 자산을 성격으로 나눠 도구를 달리 골랐다:

| 자산 | 로딩 시점·주체 | 선택한 도구 | 이유 |
|------|---------------|-------------|------|
| `MapDefinition` | mapId로 **동적**, `MapLoader`(async `StartAsync`) | **Addressables async** (`LoadAssetAsync` + await + `Release`) | 맵이 늘어도 빌드에 다 안 들어감. async 컨텍스트라 자연스러움 |
| 표시 카탈로그 5종 | `LifetimeScope.Configure`(**동기** DI 등록) | **Addressables + `WaitForCompletion()`** | 씬 수명 = 카탈로그 수명. async-DI 재설계 회피 |
| 저작 전용 SO (DropTable·LevelTable·Monster) | **런타임 미로드** (클라·서버 모두 bake JSON을 읽음) | **이동만** (Addressables 불필요) | 로드되지 않는 자산에 Addressables는 무의미 |

마지막 행이 핵심 통찰이었다. `DropTableDefinition` 같은 SO는 "클라가 Resources.Load로 읽는다"고 주석에 적혀 있었지만, 실제 런타임 코드를 grep하니 **아무도 안 읽고 있었다** — 클라는 `Shared.Infrastructure`의 JSON 카탈로그를 쓰고, 이 SO는 *Export 소스일 뿐*이었다. 로드되지 않는 자산을 Addressable로 만드는 건 순수 오버헤드다. 그래서 이 3종은 GameData로 옮기기만 했다(코드 0).

### sync DI에 async Addressables를 끼우는 법 — `WaitForCompletion()`

가장 까다로운 지점은 카탈로그였다. `LifetimeScope.Configure(builder)`는 **동기**로 의존성을 등록하는데 Addressables는 **비동기**다. async 부트스트랩으로 DI를 재설계하면 등록 순서·수명에 광범위한 회귀 위험이 있다.

```csharp
// 씬 수명 카탈로그를 로컬 Addressable 번들에서 "동기로" 로드.
// 핸들은 의도적으로 보존(앱 내내 필요). 미등록 주소면 null → 호출부가 빈 SO 폴백.
private static T LoadData<T>(string address) where T : Object
    => Addressables.LoadAssetAsync<T>(address).WaitForCompletion();

builder.RegisterInstance(LoadData<EffectIconCatalog>(AddressKeys.Data.EffectIconCatalog)
                         ?? ScriptableObject.CreateInstance<EffectIconCatalog>());
```

`WaitForCompletion()`은 "WHERE(번들에 들어가나)"가 아니라 "WHEN(언제 로드되나)"만 동기로 만든다. 자산은 여전히 Addressable 번들(온디맨드 가능)에 있고 — 빌드 항상포함 회피라는 목적은 달성 — 로컬 번들이라 동기 로드가 안전하다. async-DI 재설계라는 큰 변경을 **`WaitForCompletion` 한 줄로 우회**했다. 반대로 동적 로딩인 `MapLoader`는 이미 async라 정석대로 `await`했다.

> 트레이드오프: `WaitForCompletion`은 원격/미다운로드 콘텐츠에선 주의가 필요하다. 이 프로젝트는 Default Local Group(로컬)이라 안전. 만약 원격 콘텐츠로 가면 그땐 정말로 async-DI가 필요해진다.

### 폴더만 옮겨도 키가 그대로 — root-relative 주소

이 전환이 의외로 저위험이었던 이유: **`Resources.Load`의 키는 가장 가까운 `Resources` 루트 기준 상대경로**다. 그래서 `GameData/Resources/Maps/x` → `Assets/Resources/Maps/x`로 폴더를 옮겨도 키(`"Maps/x"`)는 그대로라 런타임 코드가 안 바뀐다(중간 정리 단계에서 활용). 최종적으로 Addressables로 가면서는 address = 에셋 경로(`Assets/GameData/...asset`)로 통일하고 `AddressKeys.Data.*` 상수로 모았다. asmdef 3곳(`Game.Gameplay`/`Game.VContainer`/`Game.Tests.EditMode`)에 `Unity.Addressables` 참조를 추가하니 컴파일러가 누락을 정확히 짚어줬다 — standalone `dotnet build`는 stale csproj 때문에 못 잡았고, **Unity 컴파일이 진실의 원천**이었다.

---

## 그 외 다듬기 — 코어가 끝난 뒤에야 보이는 결함들

### 인증 레이스 — async 로그인보다 먼저 발사된 RPC

퀘스트 저널을 여니 `"Authorization header is missing"` 401이 떴다. 원인은 **비동기 초기화 순서**였다.

```
EditorAutoLoginInitializer.StartAsync  ── async 서버 왕복(로그인) ──▶ 토큰 채워짐
   (이 사이 빈 구간)
QuestModel.GetQuests  ──▶ 인터셉터: 토큰 비었음 → Authorization 헤더 생략 ──▶ 401
```

`AccessTokenProvider`는 `() => _authSession.AccessToken`이라 *생성자에서* 설정되지만, 토큰 값은 로그인이 끝나야 채워진다. 그 전에 발사된 인증 RPC는 빈 토큰 → 헤더 누락 → 서버 거부. 이미 프로젝트에는 `await AuthenticatedAsync()`(인증 완료까지 대기)라는 메커니즘과 그걸 쓰는 선례(`PlayerProgressionHolder`·`LobbyModel`)가 있었다. 같은 패턴을 `QuestService`(System)에 적용했다 — `QuestModel`·`DialogueModel` 두 호출자가 모두 이 서비스를 거치므로 **한 곳만 막으면 둘 다 보호**된다.

```csharp
private async UniTask WaitAuthAsync(CancellationToken ct)
{
    if (_authSession != null)
        await _authSession.AuthenticatedAsync().AttachExternalCancellation(ct);
}
// GetQuests/Accept/Claim/ReportTalk 각 try 시작에 await WaitAuthAsync(ct);
```

> 호출이 서버까지 도달해 401을 받았다는 것 자체가 진단의 핵심이었다 — 채널·gRPC는 정상, 단지 *그 순간 토큰이 없었던* 것. 즉 코드 버그가 아니라 **타이밍** 문제.

### 실패를 사용자에게 — 클라가 사유를 추론하고, 피드백을 일관화한다

상점에서 구매가 `code=1005`로 실패하는데 **콘솔 로그로만** 보였다. 인-윈도우 토스트는 `toastText`가 프리팹에 미할당이면 `Debug.Log`로 폴백하는 구조였기 때문. 두 가지를 고쳤다.

**(1) 실패 사유를 클라가 계산한다.** 서버는 권위로 거부할 뿐 사유 문자열을 안 준다. 하지만 클라는 *보유 골드와 총가격*을 안다 — 비교해서 사유를 추론한다.

```csharp
long total = selected.BuyPrice * state.Quantity;
string reason = state.Gold < total
    ? $"골드가 부족합니다.\n보유 {state.Gold:N0} / 필요 {total:N0}"
    : "구매할 수 없는 아이템이거나 구매 조건을 만족하지 않습니다.";
```

서버는 권위 게이트, 클라는 *설명*. 서버 검증 로직을 클라가 중복하지 않으면서 사용자에겐 구체적인 이유를 보여준다.

**(2) 실패는 토스트가 아니라 팝업으로, 그리고 상점·인벤토리를 일관되게.** 실패 피드백을 `AlertPopup`(프리팹을 자체 Addressable 로드라 필드 배선과 무관하게 항상 보임)으로 띄운다. 인벤토리는 토스트 채널이 `string`이라 성공/실패 구분이 없었다 — `InventoryToast{Message, Success}`(상점의 `ShopToastMessage` 동형)로 바꿔 **실패만 팝업, 성공은 로그**로 라우팅. MVI는 유지: 메시지(사유 포함)는 Model이 만들고, 팝업 표시는 View가 한다.

---

## 핵심 키워드 정리

- **식별자 통일**: 부채 티켓 이름(`DungeonId`)이 아니라 *이미 코드가 쓰는 어휘*(`MapId`)를 따른다 — 매핑 한 겹을 만들지 않는다.
- **검증 위치 = 소비자**: mapId 정규화/검증은 `SpawnLayoutTable`이 필요하므로 Application에. Domain은 값 보관만(DIP, 레이어 역전 회피).
- **진실의 원천**: 던전 식별값은 방(`DungeonRoom.MapId`)에 영속, `StartGame`이 그걸 읽는다 — 이벤트는 트리거, 상태는 진실원에서.
- **bake 도구는 왕복 보장**: `MapDataExporter`가 `expReward`를 누락해 Export가 보상을 지우던 결함 — 데이터 추가 도구는 기존 데이터 보존이 별도 검증 대상.
- **Resources = 빌드 항상포함**: 온디맨드가 아니므로 게임 데이터는 Addressables/GameData로.
- **자산 성격별 도구**: 동적=async Addressables · 씬 카탈로그=`WaitForCompletion`(sync DI 회피) · 저작 전용(런타임 미로드)=이동만.
- **`WaitForCompletion`**: Addressables를 동기 컨텍스트에 끼우는 다리 — WHERE(번들)는 그대로, WHEN(로드 시점)만 동기. 로컬 번들 전제.
- **root-relative key**: `Resources` 루트만 바꿔도 로드 키 불변 → 중간 이동이 저위험.
- **Unity 컴파일 = 진실**: stale csproj 때문에 `dotnet build`가 놓치는 asmdef 참조 누락을 Unity가 잡는다.
- **비동기 초기화 레이스**: async 로그인 전에 발사된 인증 RPC = 빈 토큰 401. `await AuthenticatedAsync()` 게이트를 **공통 호출 funnel(System 서비스)**에 둔다.
- **클라가 사유 추론**: 서버는 권위 거부, 클라는 자기가 아는 값(골드/가격)으로 *설명*을 만든다(검증 중복 없이).
- **피드백 일관화**: `string` 토스트 → `{Message, Success}` 구조체로 승격해 실패=팝업·성공=로그를 두 화면(상점·인벤토리)에 동일 적용.

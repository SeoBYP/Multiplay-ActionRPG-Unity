# 20. 콘텐츠 파이프라인 — 데이터를 늘리는 도구가 데이터를 지우고 있었다

> **한 줄** — 던전을 데이터로 늘릴 수 있게 만드는 작업이었는데, 착수 직전에 **Export 도구가 기존 보상 값을 조용히 0으로 만들고 있다**는 걸 발견했다. 콘텐츠를 늘리는 행위가 곧 기존 콘텐츠를 지우는 행위가 될 뻔했다.
>
> **범위** 식별자 통일 · 검증 위치 · bake 왕복 · Resources → Addressables · 비동기 초기화
> **핵심 통찰** 자산은 성격이 다르면 **로딩 도구도 달라야** 한다 — "전부 Addressables"는 정답이 아니다

---

## 1. 부채 티켓의 이름을 그대로 구현하지 않았다

방을 만들 때 어떤 던전을 플레이할지가 **방의 속성**이어야 하는데, 그게 없었다.

```
[Before]  StartRoom(map_id) ──▶ 메시지에만 실림 ──▶ 스폰 레이아웃
          DungeonRoom 엔티티 : 맵 필드 없음      ← 시작 순간에만 존재하고 휘발

[After]   CreateRoom(map_id) ──▶ DungeonRoom.MapId 영속(DB + Redis)
                                     │ 진실의 원천
          StartRoom ──────────▶ room.MapId 를 읽어 메시지에 싣는다
```

부채 티켓에 적힌 이름은 `DungeonId`였다. 그런데 스폰·콘텐츠 시스템은 이미 전부 **`MapId`**(`"dungeon_01"`, `spawn-layouts.json`의 키)로 통일돼 있었다. 여기에 숫자 `DungeonId`를 더하면 **`DungeonId → MapId` 매핑이라는 두 번째 식별자**가 생긴다.

> **이름은 "무엇을 가리키나"가 아니라 "이미 코드가 쓰는 어휘"에 맞춘다.** 티켓 제목을 그대로 구현하면 매핑 한 겹이 영구히 남는다.

## 2. 검증은 소비자 레이어에 둔다

`MapId`의 유효성(빈값 → 기본 맵, 모르는 맵 → 거부)은 누가 책임지나? `DungeonRoom`(Domain) 안에 넣으면 깔끔해 보이지만, 검증하려면 **"어떤 맵이 존재하는가"를 아는 `SpawnLayoutTable`이 필요**하고 그건 Infrastructure다.

```
CreateRoom(gRPC) ─▶ CreateDungeonRoomAsync (Application)
                      ├─ 빈값?           → MapIds.Default
                      ├─ IsKnown(mapId)? → 아니면 거부
                      └─▶ DungeonRoom.Create(..., mapId)   ← Domain 은 값 보관만
```

Domain이 Infrastructure를 알게 되면 **의존 방향이 역전**된다. 그래서 검증을 **소비자(Application)** 에 뒀다.

엔티티에 필드를 하나 더하는 일도 규칙대로 4곳을 함께 고쳐야 했다(`Create`/`Clone`/`FromRedis`/`ToHashEntry`+`ParseFromRedis`, [05](./chapter-05-game-start-e2e.md) 9절). 여기에 EF 마이그레이션(기존 행은 `dungeon_01`로 백필)과 **구버전 캐시 하위호환**(Redis Hash에 `MapId`가 없으면 거부하지 않고 Default로 폴백)까지가 한 세트다.

## 3. 선택지 메타는 클라가 갖는다

```
[생성] 드롭다운(DungeonCatalog SO: mapId → 표시이름)
     → LobbyIntent.CreateRoom(name, max, mapId) → … → CreateRoomRequest.map_id → 서버 영속
[표시] RoomInfo.map_id → DungeonRoomModel.MapId  (방 목록에 "어느 던전")
```

**"어떤 던전을 고를 수 있고 화면에 뭐라 보이나"는 표현 계층의 관심사다.** 서버는 이미 `IsKnown`으로 권위 검증을 하므로, 표시 이름을 위해 RPC를 새로 만들 이유가 없다.

결과적으로 **새 던전을 늘리는 건 데이터 작업뿐**이다 — `MapDefinition` SO를 만들고 Export → 서버 재빌드. 코드 변경 0.

## 4. 이 챕터의 핵심 — Export가 보상을 지우고 있었다

던전을 데이터로 늘릴 수 있다는 걸 실증하려고 샘플 `dungeon_02`를 만들고 Export 도구를 돌리려다, 코드를 읽던 중 발견했다.

```
MapDataExporter.MapDto = { mapId, bounds, points, monsters }
                                    ▲ expReward 가 없다

→ SO 를 모아 JSON 으로 bake 하면 expReward 필드가 출력에서 누락
→ 누구든 Map Editor 에서 Export 하는 순간 dungeon_01 의 expReward:100 이 0 이 된다
```

`spawn-layouts.json`의 `expReward:100`은 서버가 던전 보상 산정에 읽는 값인데([14](./chapter-14-dungeon-clear-loop.md) 5절), **손편집으로만 유지**되던 상태였다. 아무도 Export를 안 돌렸기 때문에 여태 살아 있었던 것이다.

`MapDefinition.expReward` 필드를 추가하고 익스포터의 **Export/Import 양방향**을 맞춰 SO↔JSON을 정합화했다(`MapDataExporter.cs:81, 153, 247`).

> **교훈 — "데이터를 추가하는 도구"가 "기존 데이터를 보존하는가"는 별개 검증이다.** bake류 도구는 **왕복(round-trip)이 보장돼야** 안전하다. Export만 있고 Import가 없으면, 그 도구는 자기가 모르는 필드를 전부 지운다.
>
> 그리고 이 결함은 **콘텐츠를 늘리기 직전에** 발견했다. 던전을 몇 개 더 만든 뒤였다면 보상이 0이 된 것도, 언제부터 그랬는지도 몰랐을 것이다. (같은 종류의 드리프트가 나중에 실제로 터진다 → [27](./chapter-27-silent-failure.md) 6절)

## 5. Resources를 버린 이유, 그리고 "전부 Addressables"가 아닌 이유

`Resources/` 폴더는 **빌드에 무조건 전부 포함**된다. 온디맨드가 아니다. 던전·맵이 늘어날수록 안 쓰는 데이터까지 항상 번들에 들어간다.

그래서 게임 데이터 SO를 `Assets/GameData/<컨텐츠>/`로 옮겼다. 다만 **로딩 도구는 자산의 성격에 따라 셋으로 갈랐다.**

| 자산 | 로딩 시점·주체 | 도구 | 이유 |
|---|---|---|---|
| `MapDefinition` | mapId로 **동적**, async 컨텍스트 | Addressables **async** | 맵이 늘어도 빌드에 다 안 들어감 |
| 표시 카탈로그 5종 | `LifetimeScope.Configure`(**동기** DI 등록) | Addressables + **`WaitForCompletion()`** | 씬 수명 = 카탈로그 수명. async-DI 재설계 회피 |
| 저작 전용 SO (DropTable·LevelTable·Monster) | **런타임에 로드되지 않음** | **이동만** (Addressable 아님) | 로드 안 되는 자산에 Addressables는 무의미 |

**마지막 행이 이 절의 핵심이다.** `DropTableDefinition` 같은 SO는 주석에 "클라가 `Resources.Load`로 읽는다"고 적혀 있었는데, 실제 런타임 코드를 grep해 보니 **아무도 읽지 않고 있었다.** 클라는 `Shared.Infrastructure`의 bake된 JSON을 쓰고, 이 SO는 **Export 소스일 뿐**이었다.

> 주석을 믿고 전부 Addressable로 만들었다면, 로드되지도 않는 자산에 주소·그룹·번들 관리 비용만 붙었을 것이다. **"어떻게 로드되나"를 문서가 아니라 코드에서 확인**한 게 이 결정을 갈랐다.

## 6. 동기 DI에 비동기 로딩을 끼우는 다리

가장 까다로운 건 카탈로그였다. `LifetimeScope.Configure(builder)`는 **동기**로 등록하는데 Addressables는 **비동기**다. async 부트스트랩으로 DI를 재설계하면 등록 순서·수명에 광범위한 회귀 위험이 있다.

```csharp
// 씬 수명 카탈로그를 로컬 Addressable 번들에서 동기 로드.
// 미등록 주소면 null → 호출부가 빈 SO 로 폴백.
private static T LoadData<T>(string address) where T : Object
    => Addressables.LoadAssetAsync<T>(address).WaitForCompletion();
```

핵심은 **`WaitForCompletion`이 바꾸는 건 WHEN이지 WHERE가 아니라는 것**이다.

```
WHERE (어느 번들에 들어가나)  →  여전히 Addressable 번들   ← 목적(빌드 항상포함 회피) 달성
WHEN  (언제 로드되나)         →  동기로 완료 대기          ← DI 재설계 회피
```

로컬 번들이라 동기 로드가 안전하다. **큰 재설계를 한 줄로 우회한 것**이고, 반대로 진짜 동적 로딩인 `MapLoader`는 이미 async라 정석대로 `await`했다.

> **대가** — 원격/미다운로드 콘텐츠로 가면 `WaitForCompletion`은 위험하다(다운로드를 동기 대기하게 된다). 그때는 정말로 async-DI가 필요해진다. **지금의 전제(로컬 번들)를 명시해 두는 것까지가 이 결정의 일부다.**

이관 자체가 저위험이었던 이유도 하나 있다 — `Resources.Load`의 키는 **가장 가까운 `Resources` 루트 기준 상대경로**라, 폴더를 통째로 옮겨도 키가 그대로다. 중간 정리 단계에서 이 성질을 이용했다.

> asmdef 3곳에 `Unity.Addressables` 참조를 추가하니 **컴파일러가 누락을 정확히 짚어줬다.** standalone `dotnet build`는 stale csproj 때문에 못 잡는다 — **클라 컴파일의 진실원은 Unity다.**

## 7. 코어가 끝난 뒤에야 보이는 결함 둘

### 인증 레이스 — 로그인보다 먼저 발사된 RPC

퀘스트 창을 열자 `"Authorization header is missing"` 401이 떴다.

```
EditorAutoLoginInitializer.StartAsync ── async 로그인 왕복 ──▶ 토큰 채워짐
        ▲ 이 사이의 빈 구간
QuestModel.GetQuests ──▶ 인터셉터: 토큰 비었음 → 헤더 생략 ──▶ 401
```

토큰 공급자는 `() => _authSession.AccessToken`이라 **생성자에서 설정되지만 값은 로그인이 끝나야 채워진다.** 채널도 gRPC도 정상이었다 — **코드 버그가 아니라 타이밍**이었다.

이미 프로젝트에는 `await AuthenticatedAsync()`(인증 완료 대기) 메커니즘과 선례가 있었다. 같은 패턴을 **`QuestService`(System) 한 곳**에 적용했다 — `QuestModel`·`DialogueModel` 두 호출자가 모두 이 서비스를 거치므로 **한 곳만 막으면 둘 다 보호**된다(`QuestService.cs:31`).

> 훅을 funnel에 다는 판단이 또 나온다([19](./chapter-19-quest-system.md) 6절). 이번엔 보안이 아니라 **초기화 순서**를 위해서다.

### 실패를 사용자에게 — 서버는 거부하고, 클라는 설명한다

구매가 `code=1005`로 실패하는데 **콘솔 로그로만** 보였다. 인-윈도우 토스트가 프리팹 필드 미할당 시 `Debug.Log`로 폴백하는 구조였기 때문이다(설계된 폴백이 실패를 삼킨 사례 — 이 주제는 [27](./chapter-27-silent-failure.md)에서 크게 다뤄진다).

**① 사유는 클라가 계산한다.** 서버는 권위로 거부할 뿐 사유 문자열을 주지 않는다. 하지만 클라는 **자기 골드와 총가격을 안다.**

```csharp
string reason = state.Gold < total
    ? $"골드가 부족합니다.\n보유 {state.Gold:N0} / 필요 {total:N0}"
    : "구매할 수 없는 아이템이거나 조건을 만족하지 않습니다.";
```

**서버는 게이트, 클라는 설명.** 서버 검증 로직을 클라가 중복하지 않으면서도 구체적인 이유를 보여준다.

**② 피드백 채널을 구조화했다.** 인벤토리 토스트는 채널이 `string`이라 성공/실패 구분이 없었다. `{Message, Success}` 구조체로 승격해 **실패는 팝업(자체 Addressable 로드라 필드 배선과 무관하게 항상 보임), 성공은 로그**로 라우팅했다. 상점과 인벤토리 양쪽에 같은 형태를 적용했다.

## 8. 남은 것

### ⚠️ Resources 경로가 하나 살아 있다

이 챕터는 게임 데이터를 Resources 밖으로 옮기는 것이 목표였는데, **`spawn-layouts.json`은 아직 `Resources.Load`로 읽힌다.**

```
Script/Gameplay/Resources/spawn-layouts.json          ← 클라 사본
SpawnLayoutProvider.cs:33  Resources.Load<TextAsset>  ← 살아 있는 프로덕션 경로
   소비자: CharacterSpawner · LocalRespawnController · MainMonsterSpawner
```

하필 **이 챕터 4절의 주인공인 그 파일**이고, 맵이 늘어날수록 커지는 데이터라 "빌드 항상 포함"의 대가가 가장 큰 축에 속한다. SO들은 옮겼는데 **bake 산출물(TextAsset)은 남은 것**이다.

(`Assets/Resources/VContainerSettings.asset`은 프레임워크가 그 위치를 요구하므로 정상이다.)

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| bake 도구는 왕복 보장 | 저작↔bake 드리프트 감시([27](./chapter-27-silent-failure.md)) |
| 자산 성격별 로딩 도구 | 어빌리티·몬스터·레벨 데이터 전면 SO화([26](./chapter-26-measured-combat-cleanup.md)) |
| 식별자는 하나 | 콘텐츠 확장이 코드 변경 0 |
| 인증 게이트를 funnel에 | 초기화 순서 문제의 표준 대응 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-20-content-pipeline-addressables.md](../learning-log/chapter-20-content-pipeline-addressables.md)

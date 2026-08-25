# 코드 정리 백로그 (2026-08-24 갱신)

> "지금 코드에서 문제나 이상한 부분을 다 정리한다"는 트랙의 **입력 문서**.
> 각 항목은 **근거(실측/코드 위치)** 와 **왜 문제인가**, **선행 결정이 필요한지**를 함께 적는다.
> 근거 없이 "정리하면 좋겠다" 수준의 항목은 넣지 않는다 — 그런 건 착수 판단을 흐린다.
>
> 상태: `⬜` 미착수 · `🔄` 진행 · `✅` 완료 · `❓` 선행 결정 필요
> 진실원: 진척은 [plan.md](plan.md), 코드 위치·결정 로그는 [codemap.md](codemap.md).

---

## A. 데이터 정합성 (가장 위험 — 조용히 갈라진다)

### A1. `abilities.json` 저작↔bake 드리프트 — ✅ **해소 2026-08-18**

| 값 | 클라 SO(저작) | 서버 bake | 상태 |
|---|---|---|---|
| `basic_swing` startup/active | **167 / 125** | 200 / 100 | 불일치 |
| `leviathan_attack` startup/active | **213 / 87** | 200 / 100 | 불일치 |
| `combo_a` · `heavy_swing` | 150/100 · 400/150 | 동일 | 일치 |

- **왜 문제인가**: `abilities.json` 은 서버가 임베디드로 읽는 **판정 창·쿨다운의 권위**다. 클라가 167ms 에 히트박스를 열고 서버는 200ms 기준으로 검증하면 던전에서 데미지가 유실·거부될 수 있다.
- **조치**: `Tools/Ability/Export` 재실행 + 서버 재빌드 + Docker 재배포. ⚠ exporter 가 끝에 `EditorUtility.DisplayDialog`(모달)를 띄워 자동화가 그 자리에서 블록된다(AC-E5 함정) → 팝업 없는 경로로 호출.
- **동반 제안**: bake 드리프트를 **CI/테스트로 감지**. 사람이 "export 했나?"를 기억하는 구조는 반드시 한 번은 잊는다.
- **결과(2026-08-18)**: `AbilityCatalogExporter` 재실행으로 bake 갱신 → SO↔bake 불일치 **0건**(15어빌×15필드 재대조). 원인은 `7f5d9754`(CA-5 Phase 1b)에서 타임라인 툴로 SO 만 바꾸고 Export 를 안 돌린 것.
  - 모달 우회법 확립: Unity CLI `unity command --project-path . eval_file --file <cs>` 로 `Exporter.BakeAll()` 직접 호출(메뉴 경로는 `DisplayDialog` 가 에디터 메인스레드를 잡아 이후 명령까지 전부 타임아웃시킨다).
  - `AbilityCatalogTests.게임플레이_수치가_현재_저작값으로_bake_돼_있다` 를 현재 저작값(167/125)으로 갱신 — 이 테스트가 곧 드리프트 감지 가드다. **밸런스 조정 시 Export 후 기대값도 갱신**.

### A2. 나머지 bake 산출물 4종 미대조 — ✅ **대조 완료 2026-08-18 (드리프트 0)**

`drop-tables.json` · `consumable-effects.json` · `spawn-layouts.json` · `level-table.json` + `monsters.json` 을 **각 Exporter 재실행 후 diff** 로 대조 — 게임플레이 수치 변경 **0건**(파일 끝 개행 차이만, 되돌림).

```
eval_file → consumables=1 droptables=12 monsters=13 leveltable=60 mapdata=8
mtime 전부 갱신 확인(= 실행 확증) · 내용 diff 0
```

- **교훈**: "diff 가 없다"를 곧바로 "드리프트 없음"으로 읽지 말 것. 첫 시도에서 5개 Exporter 가 **모달에 막혀 실행조차 안 됐는데** diff 가 없어 정상으로 오독할 뻔했다. **mtime 으로 실행 자체를 확증**한 뒤 diff 를 판정한다.
- **잔여 제안**: 이 대조를 EditMode 테스트로 상시화(사람 기억에 의존하지 않게).

### A4. `items.json`·`quests.json` Exporter 부재 → 카탈로그 드리프트 — ✅ **해소 2026-08-18/19**

bake 산출물 7종 중 **`items.json` 하나만 Exporter 가 없다**. 나머지 6종(abilities·consumable-effects·drop-tables·monsters·level-table·spawn-layouts)은 전부 `Tools/…/Export` 가 있어 SO→bake 로 강제되지만, 아이템만 **서버 `items.json` 수기 + 클라 `ItemDisplayCatalog.asset` 수기**로 이중 저작이다.

실측 대조 (2026-08-18):

```
서버 items.json  10개        클라 ItemDisplayCatalog  11개
                             3. gold_pouch          ← 클라에만 존재(서버 없음)
순서: 3번 이후 전부 1칸씩 어긋남
```

- **왜 문제인가**: ① `gold_pouch` 는 `ItemCatalogData.cs:13-15` 가 기록한 그 사고("gold_pouch 고아")의 **잔재** — 서버만 정리되고 클라는 안 됐다. ② `ItemCatalogData.cs:17` 이 **"파일 순서 = 상점 진열 순서"** 를 명시하는데 두 파일의 순서가 이미 다르다. 지금은 문자열 룩업이라 안 드러나지만 **인덱스·ID 순서에 의존하는 코드가 생기면 즉시 깨진다**.
- **왜 A2 에서 못 잡았나**: A2 는 "Exporter 를 재실행해 diff 를 본다" 방식이었다. `items.json` 은 **Exporter 자체가 없어 대조 대상에서 빠졌다** — 방법론의 사각지대였다.
- **조치**: `ItemCatalogDefinition`(SO) + `ItemCatalogExporter` 신설 → 다른 6종과 동일 교리로 일원화. `ItemDisplayCatalog` 은 표시 전용으로 두되 정의 SO 를 진실원으로 참조. `gold_pouch` 존치 여부 결정 필요.
- **선행 관계**: **ItemId int 전환(사용자 결정 2026-08-18)의 0단계.** 어긋난 상태로 numericId 를 부여하면 드리프트를 숫자로 굳히게 된다.

**해소 내역 (2026-08-18)**

- `ItemCatalogDefinition`(SO, `Game.Gameplay.Items`) + `ItemCatalogExporter`(Tools/Item/Export·Import) 신설 → 다른 6종과 동일 교리로 일원화. **정렬 금지**(저작 순서 = 상점 진열 순서)를 코드 주석으로 못박음.
- 왕복 검증: items.json → Import → SO → Export → items.json, **10종 순서 동일·필드 불일치 0**.
- `gold_pouch` 제거(코드 참조 0건 실측) · `potion_mp_small` 에 Mana +100 Instant 부여(주석이 "과거 사고"로 적었던 효과 누락이 실제로 남아 있었다).
- 재대조: 서버 10 / 클라 10, **구성·순서 모두 일치**.
- 죽은 `ConsumableEffectExporter` 제거 — 서버엔 `ConsumableEffectCatalog` 가 없고 소비 효과는 items.json 의 `consumeEffects` 로 통합됐는데 Exporter 만 남아 아무도 안 읽는 JSON 을 계속 생성했다(실제로 조사 중 오독을 유발).
- 검증: Unity 컴파일 0 · 서버 빌드 0오류 · 테스트 **659/659**(Shared 50 · SocketServer 210 · GameServer 399).

**후속(2026-08-19) — 마지막 수기 저작까지 제거**: `quests.json` 도 Exporter 가 없어 손으로 저작하고 있었다(A4 조사 당시엔 items 만 봤다). `QuestCatalogDefinition`+`QuestCatalogExporter` 신설로 편입 → **bake 산출물 7종 전부 SO→bake 강제**. 추가로 `BakeAllExporter`(`Tools/Bake/Export ALL`)로 7종 일괄 bake + 결과 요약을 제공 — "Export 했나?"를 사람이 기억하는 구조를 없앤다. `BakeAllExporter.BakeAll()` 은 다이얼로그가 없어 CLI 로 호출 가능하므로, `git diff --exit-code` 와 묶으면 드리프트 점검을 자동화할 수 있다.

**부수 발견 — 저장소 자체가 깨져 있었다**: `items.json`·`quests.json` 이 `EmbeddedResource` 인데 **미커밋**이었고, 그것을 읽는 `Shared.Infrastructure/{Items,Quests}/*.cs` 와 `Shared.Gameplay/Items/*.cs` 도 미커밋이었다(Domain→Shared.Infrastructure 이동 리팩터가 삭제만 커밋된 상태). 클론하면 빌드 실패. `8438d721`·`402eb438`·`99bed9b4` 로 복구.

**검증 방법 교훈**: 로컬 `dotnet build` 는 미커밋 파일이 디스크에 있어 **항상 통과**하므로 이 부류를 절대 못 잡는다. `git archive HEAD ServerAll | tar -x -C <tmp>` 로 **커밋본만 꺼내 빌드**하는 것이 진짜 검증이다(전체 `git clone` 은 `.git` 20GB vs 여유 20GB 로 디스크 부족 실패).

### A3. `RemotePlayerCharacter` 머티리얼 누락 — ✅ **해소 2026-08-18** (재직렬화로 고아 블록 제거)

- YAML 텍스트엔 `Capsule` GameObject + MeshRenderer 가 guid `31321ba15b8f8eb4c954353edc038b1d` 를 참조하는 채로 남아 있다.
- **그러나 Unity 로 프리팹을 실제 로드하면 계층에 없다** — `LoadPrefabContents` 후 `GetComponentsInChildren<MeshRenderer>(true)` 결과는 `WeaponProp`(부모 `hand_r`) 하나뿐이고 머티리얼 `M_BludgeonProp` 이 정상 연결돼 있다. SkinnedMeshRenderer 14개도 정상.
- 즉 **도달 불가능한 고아 YAML 블록**이다(루트 `m_Children` 에서 빠진 것으로 추정 — `m_Children` 직접 확인은 **미실측**). 렌더에 영향 없음.
- ⚠ **정정**: 최초 보고의 "SkinnedMeshRenderer 가 참조" / "원격 플레이어가 머티리얼 누락으로 렌더된다" 는 **텍스트 grep 만으로 내린 오판**이었다. 프리팹 구조 판정은 YAML grep 이 아니라 Unity 로드로 확인한다.
- **결과**: A3 조사 중 호출한 `LoadPrefabContents`→`SaveAsPrefabAsset` 이 (5s 타임아웃으로 실패한 줄 알았으나 실제로는 적용돼) 프리팹을 재직렬화하며 고아 블록 **91줄을 제거**했다. `Capsule`·누락 guid 모두 0.
- 검증(Unity 로드): `RemotePlayerCharacter` GameObject 115 · Skinned 14 · Mesh 1 · **머티리얼NULL 0 · 깨진컴포넌트 0** (`PlayerCharacter` 도 동일하게 clean).
- ⚠ **재발 교훈(2회째)**: Unity CLI 의 타임아웃은 "미실행"을 뜻하지 않는다. `abilities.json`(DisplayDialog)·이 프리팹(main-thread 5s) 둘 다 **타임아웃 응답 뒤에 실제로는 적용**됐다. 타임아웃이 나면 결과를 **파일 상태로 재확인**한다.

---

## B. 구조·설계 (동작은 하지만 어긋나 있다)

### B1. `CharacterMotor.Move` 가 `Time.deltaTime` 을 직접 읽음 — ❓ 중간

- 상태머신은 `Update(deltaTime)` 으로 시간을 **인자로 전달**하는데, Motor 만 전역 시계를 직접 읽는다. 두 시간축이 어긋난다.
- **드러난 방식**: `ActionRootTests` 가 프레임레이트 의존이 되어 에디터가 빠를수록 이동량이 줄었다(실측 0.0015m / 기대 >0.01m). 테스트는 실시간 예산 구동으로 우회했고 **원인은 그대로 남아 있다**.
- **선행 결정**: 시그니처를 `Move(input, speed, deltaTime)` 로 바꾸면 이동 감각·기존 튜닝값에 영향이 갈 수 있다. 바꿀지 말지가 먼저다.

### B2. AC-D2 플레이어→플레이어 데미지가 플랫 — ❓ 중간

- 몬스터→플레이어, 플레이어→몬스터는 스탯 스케일인데 **P→P 만 플랫**이라 비대칭.
- **선행 결정**: 코옵에서 friendly fire 를 유지할지 자체가 먼저. 유지 안 할 거면 이 항목은 소멸한다.

### B3. `Game.Gameplay.Input.InteractionSystem` 휴면 중복 — ⬜ 중간

- 던전 상호작용 실작동 경로는 `Game.Gameplay.Character`(detector + `IInteractable`). `InteractionSystem`(리치/라우터)은 아웃게임 등록·휴면 상태로 중복.
- **조치**: 제거 또는 일원화 결정. 아이템 인벤토리(3.1) 합류 시 instigator 흐름과 함께 확정.

### B4. `GetRooms` 페이징의 남는 한계 — ✅ **해소 (2026-08-24)**

- 전량을 읽어 메모리에서 자르던 것을 **DB 로 내렸다**: `GetActiveRoomsPageAsync(offset, limit)` → `ORDER BY "RoomId" DESC OFFSET/LIMIT` + `COUNT(*)`.
- Redis 키 계약(Set→Sorted Set) 변경은 **필요 없었다** — 목록의 진실을 DB 로 옮긴 뒤라(§2.107 ①) 정렬·자르기를 그냥 DB 가 하면 됐다. 보류 사유였던 마이그레이션 자체가 사라진 셈.
- 안정 정렬을 저장소 계약에 못 박아 호출자가 다시 정렬하지 않는다. 총계는 `ActiveRoomsPage.TotalCount` 로 함께 반환.
- 상세 = [codemap.md](codemap.md) §2.108.

### B5. `Game.Gameplay.Editor` → `Game.Network` 참조 — ✅ 승인됨(기록용)

- `CombatTraceWindow` 가 트레이스 링버퍼를 읽어야 해서 추가한 **하향 참조**(위반 아님, 2026-07-17 승인). 재검토 시 근거로 참조.

### B6. `ReportTalk` 근접 검증 — **하지 않기로 결정** — ✅ **닫음 (2026-08-25, 사용자 결정)**

**결론(2026-08-25)** — 아래 3안 중 **가(현행 유지)**. 위치 기반 검증은 넣지 않는다.

> 서버의 책임은 **퀘스트 진행도까지**다. 대사 진행·스킵은 클라이언트 연출 영역이라 서버가 볼 것이 아니고,
> 근접 우회는 **막을 이유가 없다**(사용자 결정).

- **코드 변경 없음.** 현재 `QuestService.ReportTalkAsync` 가 이미 그 경계다 —
  카탈로그에 대화 목표가 있는가(`HasTalkObjective`) + 수주했고 미완료인가(`AdvanceMatchingAsync`). 위치는 보지 않는다.
- 이 항목은 **"안 고침"이 아니라 "고치지 않기로 정함"** 이다. 아래 경위는 나중에 이 결정을 뒤집을 때 쓰라고 남긴다.
- ⚠ 용어: 아래 "근접 우회"는 **걸어가지 않고 대화 퀘스트를 올리는 것**을 말한다.
  UI 의 대사 스킵(텍스트 넘기기)과는 무관하며, 그쪽은 처음부터 클라 전용이다.

---

**경위** — F5 가 "근접 검증 불가"로 닫혔으나, 전제 하나가 바뀌었었다.

> 퀘스트를 주고받는 NPC 는 **일정 범위 안에서만** 움직이게 한다.

→ NPC 를 자유 이동시키지 않으므로 **라이브 위치 동기화가 필요 없다.** 앵커+반경을 카탈로그에 저작해두면
서버가 NPC 위치를 "알" 수 있다. **F5 가 불가 사유로 든 두 가지 중 하나가 사라졌다.**

```
[F5 시점 — 불가 사유 2개]
  ① 서버가 NPC 위치를 모른다      ← 씬 배치, 위치 카탈로그 없음
  ② 서버가 Main 플레이어 위치를 모른다  ← Main 은 소켓 미연결

[지금]
  ① 해소 가능  quests.json / npc 카탈로그에  anchor(x,z) + roamRadius 저작
               NPC 가 그 안에서만 걷는다 → 서버는 "반경 R 안 어딘가" 로 충분히 판정
               동기화 0회. 저작 데이터만 늘어난다.
  ② 지금은 ❌  Main 씬은 소켓을 열지 않는다(검증: GameSessionInstaller — GameSessionConnector 가
               TCP 연결 후 Dungeon 씬을 로드한다. 즉 소켓은 던전 진입 장치다)
     하지만 **영구 제약이 아니다** — 아래 B7(위치 지속화)이 들어오면 서버가 플레이어 위치를 받게 된다.
     그때 ②의 "데이터가 없다" 는 사라지고, 남는 질문은 **"그 좌표를 믿을 수 있는가"** 로 바뀐다.
```

⚠ **2026-08-25 정정** — 이 항목을 처음 쓸 때 ②를 "아키텍처 제약이라 그대로"라고 단정했는데 **틀렸다.**
위치 동기화는 근접 검증과 무관한 이유(**시작 위치 복원**, B7)로 어차피 들어올 예정이다.
따라서 아래 결정은 **"불가능해서"가 아니라 "막을 값어치가 없어서"** 다 — 근거를 혼동하면 B7 이 들어온 뒤
"이제 가능하니 하자"로 잘못 재개된다. **가능해져도 하지 않는다**는 것이 결정이다.

**핵심 함정 — 여기서 판단이 갈린다.**

②를 "클라가 `ReportTalk` 에 자기 좌표를 같이 보낸다" 로 푸는 것은 **검증이 아니다.**
치터는 NPC 좌표를 그대로 적어 보내면 그만이라, 필드만 늘고 보안은 0 이다.

```
클라 → ReportTalk(npcId, myPos)  →  서버: dist(myPos, anchor) <= R ?
                    └── 클라가 만든 값. 검사 대상이 검사 근거를 제출한다.
```

**위치 검증은 위치가 서버 권위일 때만 값을 갖는다.** 선택지는 셋이고, 정하는 것이 선행 과제다:

| 안 | 내용 | 비용 | 실효 |
|---|---|---|---|
| **가** ✅ | 현행 유지 — 위치 검증 안 함, F5 의 "정상 요청만 오게" 로 만족 | 0 | 근접 우회 가능(피해 한정: `RequiredCount` 상한 · 보상 1회) |
| **나** | 클라가 좌표 동봉 + 서버가 앵커 반경 검사 | 작음 | **보안 0** — 위 함정. 실수 방지(버그 탐지)용 로그로는 의미 있음 |
| **다** | Main 도 소켓 연결 → 이동을 서버가 받고 권위화 → 앵커 반경 검사 | **큼** — Main 을 서버 권위로. 2.4/authority-model 재검토 동반 | 진짜 검증. F5 잔여도 함께 닫힌다 |

- **결정됨(2026-08-25)**: 근접 우회는 막을 이유가 없다 → **가**. (F5 가 이미 그 방향으로 닫혀 있어 문서도 일관된다.)
  뒤집을 일이 생기면 **다**로 간다 — **나는 고르지 말 것**, 고쳤다는 착각만 만든다.
- 뒤집는 조건은 "기술적으로 가능해졌다" 가 **아니다**. 근접 우회가 실제 피해를 내기 시작했을 때다.
- NPC 앵커+반경 저작(①)은 **다**를 고를 때만 필요하다. **지금은 하지 않는다** — 쓰이지 않는 저작 데이터는 드리프트(A1~A4 계열)만 늘린다.
- 관련: [F5](#f5-reporttalk-에-근접-검증이-없다--재정의해소-2026-08-25) 의 "남는 한계" 가 이 항목이다.

### B7. Main 플레이어 위치 지속화 — 저장되는 좌표를 **클라가 만든다** — ✅ **해소 (2026-08-25)**

**계획된 기능**(사용자 방향, 2026-08-25): 플레이어는 Main 에 들어올 때 **기존 위치에서 시작**한다.
그러려면 플레이 중 위치를 주기적으로 서버에 올려야 한다.

**현재 상태(실측 2026-08-25)** — 아직 없다.
```
서버   GameServer.Domain/Entities 에 위치 필드 0건 (grep: PositionX·LastPosition·SavePosition → 없음)
클라   Main 스폰 = MapDefinition.playerSpawns 의 저작 고정값 (SpawnLayoutProvider.cs:60-62)
```

**왜 결함 목록에 있는가** — 기능 자체가 아니라, 기능이 **새 신뢰 경계**를 만들기 때문이다.

```
[지금]   Main 진입 → 저작 스폰 포인트. 클라가 위치를 정할 여지 없음.

[B7 이후] 플레이 중 ─주기적─▶ 서버가 좌표 저장 ─재접속─▶ 그 좌표에서 시작
                      └── 이 좌표는 클라가 만든 값이다
                          치터가 "아무 데서나 저장" 할 수 있다
                            · 진입 조건이 있는 구역(레벨 제한·퀘스트 게이트) 안쪽
                            · 지형 밖 / 벽 안 (복원 시 낙사·끼임 → 버그 리포트로 위장된 손상)
```

- 위치 저장은 "편의 기능"이라 피해가 작아 보이지만, **게이트가 걸린 구역을 우회하는 열쇠**가 될 수 있다.
  진입 게이트를 위치가 아니라 **입장 시점의 서버 판정**으로 두면 이 경로는 닫힌다 — 설계할 때 같이 정할 것.
- **선행 결정 3가지**:
  ① **복원 시 서버가 좌표를 검증하는가** — 최소한 "맵 경계 안 · 내비메시 위 · 게이트 통과한 구역" 정도.
     검증 안 할 거면 저장을 **저작 안전지대(가장 가까운 스폰 포인트)로 스냅**해 저장하는 편이 싸고 안전하다.
  ② **동기화 주기·저장소** — 매 N초 Redis vs 씬 이탈/로그아웃 시 1회 DB. 전자는 트래픽, 후자는 강제종료 시 유실.
  ③ **던전 왕복 시 처리** — 던전 갔다 오면 어디서 시작하나(던전 입장 직전 위치 복원 vs 마을 고정 스폰).
- **관련**: 이 기능이 들어오면 [B6](#b6-reporttalk-근접-검증--하지-않기로-결정--닫음-2026-08-25) 의 ②가 풀린다.
  단, B6 은 *가능성*이 아니라 *값어치* 로 닫힌 항목이므로 **자동으로 재개되지 않는다.**
- 기능 진척 자체는 [plan.md](plan.md) 가 진실원. 여기 있는 건 **신뢰 경계 부분**이다.


---

## C. 환경·저장소

**⚠ 착수 전 실측으로 드러난 정정 (2026-08-25)**

- **진입 게이트 시스템은 없다** — `RequiredLevel`·`minLevel` 류 검색 0건. 항목이 든 "게이트 걸린 구역 우회"는 **아직 존재하지 않는 위험**이다.
- **Main 스폰은 "저작 고정값"이 아니라 원점**이었다(`CharacterSpawner` 가 `SocketState != Joined` 면 `Vector3.zero`). 그래서 복원 실패 폴백이 곧 기존 동작이다.
- 서버가 실제로 가진 검증 재료는 **`MapBounds` 하나**다(spawn-layouts bake). 내비메시는 클라 자산이라 서버가 못 본다.

**해소 내역 (2026-08-25)** — 선행 결정 3가지를 먼저 확정하고 그 위에 구현했다.

| 결정 | 채택 |
|---|---|
| 복원 좌표 검증 | 경계 검증 + 밖이면 **최근접 저작 스폰으로 스냅**(clamp 아님 — 경계선 위 임의 점은 벽 안일 수 있다) |
| 동기화·저장소 | **주기 Redis + 이탈 시 DB 확정**(로그아웃·던전 입장 2곳). 읽기는 Redis→DB→없음 |
| 던전 왕복 | **입장 직전 위치** — StartGame 에서 확정 저장하므로 별도 로직 없이 성립 |

- **서버는 경계만, 클라는 지면만**(NavMesh 스냅) 검증한다. 보고는 **이동 2m + 5초** 조건이라 정지 중 트래픽 0.
- **하지 않은 것**: 이동 궤적·근접 검증. 좌표 자체가 클라가 만든 값이라 순환이다 — **NPC 좌표를 bake 해도 마찬가지**(위치를 위조할 수 있는 자는 근접도 위조한다). 제대로 하려면 연속 보고 간 속도 검증까지 가야 하고 그건 별개 기능이다. 해소 조건은 **Main 의 서버 권위 승격**.
- **교리 예외 1건 신설**: 이 도메인만 Redis 가 1차 저장소. 성립 조건 3가지(쓰기 잦음·유실 허용·폴백 존재)를 networking.md 에 명시.
- 검증: 서버 738/738 · PlayMode 228/228 · EditMode 239/239 · 마이그레이션 실환경 적용 확인. 상세 = [codemap.md](codemap.md) §2.115.
- ⚠ 미실측: 사람이 Main 에서 이동 후 재접속해 그 자리에서 시작하는 것. 클라 복원 분기(NavMesh 스냅)는 베이크된 내비메시 씬이 필요해 자동 테스트 미적용.

### C1. 디스크 포화 — ✅ **해소 2026-08-18** (아트 34/34 청크 커밋·푸시 완료, 여유 26G 회복)

> 아래는 당시 기록. `.git` 이 **20GB** 로 커진 것은 남아 있어 Git LFS 이관 검토 대상(별도 작업, 히스토리 재작성 필요).

#### (당시) 디스크 포화 — 높음(차단 요인)

- C: **931G 중 여유 108MB**. `.git` 이 **14GB**(과거 대용량 에셋 이력).
- **현재 차단하고 있는 것**: 아트 팩 커밋이 **7/34 청크(~620MB / 3.0GB)** 에서 중단. git 자동 `gc`/`repack` 도 실패(`fatal: failed to run repack`) → 느슨한 오브젝트가 계속 쌓인다.
- ⚠ Docker(Postgres/Redis)·Unity 가 동시에 도는 환경이라 0 근처에서 서비스 손상 위험.
- **선택지**: ① 공간 확보 후 이어서 ② 아트 커밋 되돌리고 미추적 유지 ③ 실제 참조 에셋만 선별 커밋.

### C2. `.gitignore` 가 Unity 에셋을 삼키는 VS/NuGet 템플릿 규칙 — ✅ **해소 2026-08-24**

- `.gitignore:80` 의 `*.meta` 때문에 **신규 에셋마다 `git add -f` 를 기억해야** 한다. 잊으면 클론 시 GUID 가 새로 생성돼 프리팹·머티리얼 참조가 전부 끊긴다.
- Unity 프로젝트에서 `.meta` 는 **소스와 동급**이다. 이 규칙이 왜 들어왔는지 확인하고, 가능하면 `!*.meta` 예외로 되돌리는 게 맞다.

**실측(2026-08-18)** — 이미 벌어진 손상이다. 앞으로의 함정이 아니라 현재 상태다.

```
추적 자산 17,075 · 그중 .meta 디스크 존재 17,070
★ .meta 미추적(고아) 8,593  = 추적 자산의 50.3%   총 127KB(용량은 무의미)
   png 5,005 · fbx 2,810 · cs 286 · prefab 150 · FBX 147 · psd 53 · mat 41 · asset 29
```

- 오늘 커밋한 아트 3팩(Book of the Dead·Melee Weapons·Magic Pig)은 `-f` 로 메타를 함께 넣어 **정상**. 고아는 그 이전 누적분(Artsystack 4,667 · PROTOFACTOR 3,226 · HONETi 546 · `Script/` 하위 293 등).

**⛔ 착수 블로커 — 지금 메타를 커밋하면 손상이 고정된다**

현재 로컬에 **이미 끊긴 GUID 참조가 대량 존재**한다(Unity 콘솔 error 실측). 이 상태의 GUID 를 커밋하면 정본으로 굳는다.

```
Assets/Art/Magic Pig Games (Infinity PBR)/.../v2_DemoEnvironment.prefab
  Missing Nested Prefab: Wind(606c378e…) · DayNightCycle(9a803e9a…) · Volume(f0239c7d…)
.../LP Files/Synty Dungeon.prefab
  Missing Prefab 04fb0ada… 'SM_Env_Flowers_26' ×25 · 0e2d311a… 'SM_Env_Tiles_102' ×52 … (수십 종)
```

- 원인은 메타 누락이 아니라 **에셋 팩의 데모 씬이 미보유 팩(Synty 등)을 전제**로 하는 것. A3 의 `31321ba…` 도 같은 계열로 보인다.
- **권장 분리**:
  1. ✅ **완료 2026-08-18**: `.gitignore` 의 `*.meta` 제거. **원인 규명 — 이 규칙은 `# Files built by Visual Studio` 블록(`*.ilk`·`*.obj`·`*.pch` 사이)에 있었다. VS 템플릿의 빌드 산출물 규칙이 Unity 에셋 `.meta` 까지 삼킨 것**이지 의도된 Unity 설정이 아니었다. 제거 사유를 파일에 주석으로 남김.
     - 실측: 제거 후 새로 노출된 `.meta` 는 **1,019개**(예상 8,593 아님). 나머지는 `Client/Assets/Packages/` 등 **다른 ignore 규칙**에 여전히 걸려 있다 → 2단계에서 함께 다뤄야 한다.
  2. ✅ **완료(앞선 세션)**: 고아 `.meta` 일괄 커밋. — 2026-08-24 재실측에서 **고아 0건**.
  3. ✅ **완료 2026-08-24**: `**/[Pp]ackages/*`(`.gitignore:207`) 예외 — 아래.

---

#### 해소 기록 (2026-08-24)

**실측 — 문서 기재와 다름.** 위 본문의 "고아 8,593 / ⛔ 블로커"는 이미 난 상태였다.

```
추적 자산(비-meta) 18,089 · 추적 .meta 19,573
★ .meta 미추적 자산(고아)  0건
   (comm 이 잡은 `Packages/*/.signature.p7s` 20건은 오탐 —
    `.` 으로 시작하는 파일은 Unity 가 임포트하지 않아 `.meta` 가 애초에 없다.
    전수 확인: `find -name '.signature.p7s.meta'` → 0건)
```

**남아 있던 진짜 문제 — `*.meta` 와 완전히 같은 사고가 하나 더 있었다.**

```
.gitignore:206  # The packages folder can be ignored because of Package Restore
.gitignore:207  **/[Pp]ackages/*        ← VS/NuGet 템플릿 규칙
                    └─▶ Client/Assets/Packages/   (NuGetForUnity 복원 위치 = Unity 에셋)
```

80행 `*.meta` 가 "VS 빌드 산출물 블록에 섞여 Unity 에셋을 삼킨" 것과 **동일한 원인·동일한 결과**다 —
`git add -f` 를 기억한 5개만 들어가 있는 반쪽 상태:

```
추적됨(-f 강제) 5개 : Microsoft.Bcl.AsyncInterfaces · Microsoft.Bcl.TimeProvider
                        System.ComponentModel.Annotations · System.Runtime.CompilerServices.Unsafe
                        System.Threading.Channels
미추적     15개 : Google.Protobuf · Grpc.Net.Client/Common · Grpc.Core.Api · MemoryPack(3)
                        R3 · ObservableCollections(2) · Microsoft.Extensions.*(2)
                        System.Collections.Immutable · System.IO.Pipelines · System.Diagnostics.DiagnosticSource
                        → .meta 259건 포함 총 416파일이 리포 밖에 있었다
```

**결정: 전부 추적** (미추적로 통일하는 대안 기각)
- 용량 실측 **8.7MB** 전체 — 아트 3GB 리포에서 무시 가능한 비용.
- 미추적로 통일하면 클론 직후 Unity 가 **컴파일 깨진 채** 열린다
  (NuGetForUnity 복원은 에디터를 열어야 돌아가는 닭·달걀 구조) → asmdef 연쇄 실패.
- Unity 에서 `Assets/` 하위는 전부 소스 — C2 가 세운 원칙(".meta 는 소스와 동급")과 같은 논리.

**조치**: `.gitignore` 에 `!Client/Assets/Packages/*` 예외 + 사유 주석. `Packages` 416파일 추가.

**검증**
```
Client/Assets 하위 ignored .meta        259 → 0
Client/Assets 하위 고아 자산            0 (유지)
ServerAll/packages/ · packages/ 무시     IGNORED ✓ (회귀 없음)
소스 파일(.cs/.proto/.asmdef) 변경   0건 — git 인덱스만 바뀜
```

**규칙(재발 방지)**: `.gitignore` 에 VS/.NET 템플릿 규칙을 붙일 때는 **`Client/Assets/` 하위에 걸리는지 먼저 확인**한다.
`*.meta`·`**/[Pp]ackages/*` 둘 다 같은 경로로 들어왔다. 남은 `**/` 규칙도 같은 위험을 가진다.

**범위 밖 — 별도 보고**: `Client/Assets/_Recovery/` (Unity 크래시 복구 씬 덤프) 가 3건 커밋돼 있고 3건은 미추적이다.
GUID 손상은 아니지만 리포에 들어갈 물건이 아니다. `Packages/.2.65.0`·`.8.0.0` 빈 디렉터리도 NuGetForUnity 설치 잔여물.
  2. **선행 정리 후**: 데모 폴더(`_DEMO SOURCE FILES`·`LP Files` 등) 정리 → 콘솔 error 0 확인 → 기존 고아 메타 커밋.

### A5. Editor Exporter 5종이 `DisplayDialog` 로 메인 스레드를 붙잡는다 — ✅ **해소 2026-08-18**

`EditorUtility.DisplayDialog` 는 **사람이 클릭할 때까지 에디터 메인 스레드를 점유**한다. Unity CLI(`unity command`)로 메뉴를 호출하면 bake 는 끝났는데 응답이 다이얼로그에 막혀 타임아웃되고, 더 나쁜 것은 **그 뒤의 모든 Pipeline 명령이 다이얼로그를 닫을 때까지 연쇄 타임아웃**한다(실측: eval 5s · menu 30s · exec 60s 연속 실패).

- 실제 피해: A2 대조 때 Exporter 5개가 **실행조차 안 됐는데** diff 가 없어 "드리프트 0"으로 오독할 뻔했다. `BakeAll` 자체는 **149ms** 로, 느려서가 아니었다.
- `ItemCatalogExporter` 는 해결됨 — `EditorApplication.delayCall` 로 알림을 다음 프레임에 미루고(`ReportLater`), `ImportAll()` 다이얼로그 없는 코어를 `BakeAll()` 과 대칭으로 분리.
- **남은 5종**: `DropTableExporter`(5) · `MonsterCatalogExporter`(5) · `LevelTableExporter`(6) · `MapDataExporter`(5) · (`ConsumableEffectExporter` 는 제거됨). 같은 패턴이라 동일 피해가 재발한다.
- **해소**: `EditorToolReport`(공용 헬퍼) 신설 — `Later`/`ErrorLater` 가 `EditorApplication.delayCall` 로 다음 프레임에 다이얼로그를 띄운다. 명령은 즉시 반환되고 사람은 그대로 확인창을 본다. 5종 전부 적용(치환 21건, 잔여 `DisplayDialog` 0건). `ItemCatalogExporter` 는 `ImportAll()` 다이얼로그 없는 코어도 `BakeAll()` 과 대칭으로 분리.
- **검증**: `eval_file` 로 **5종 연속 BakeAll 성공** — items 10(159ms) · droptables 12(28ms) · monsters 13(25ms) · leveltable 60(24ms) · mapdata 8(65ms), 합계 2.0초. 수정 전에는 첫 호출부터 5초 타임아웃이었다. 재bake 산출물 **비공백 변경 0줄**(드리프트 없음 재확인). Unity 컴파일 0 · 서버 테스트 659/659.
- **규칙**: 자동화가 부르는 로직(`BakeAll`/`ImportAll` 류)엔 다이얼로그를 두지 않는다. 다이얼로그는 `[MenuItem]` 래퍼에서 `EditorToolReport` 로만 띄운다(헬퍼 주석에 명시).

### C3. CA-5 스모크 SFX 가 미커밋 팩에 의존 — ⬜ 중간

- `Ability_BasicSwing` 의 Sfx 이벤트가 `Book of the Dead` 팩 클립을 참조한다. C1 때문에 팩이 7/34 만 올라가 있어 **다른 머신에서 GUID 가 깨질 수 있다**.
- **조치**: 정식 SFX 로 교체하거나, 해당 클립이 포함된 청크까지 커밋 완료.

---

## D. 낮은 우선순위 (정리 대상이지만 위험 아님)

| 항목 | 위치 | 내용 |
|---|---|---|
| gRPC 주소 하드코딩 | `Network/Https/GameApiClient.cs:19` | `TODO: 설정 파일/환경별 주입` — 배포(M6) 전 필요 |
| Redis 락 설정 하드코딩 | `Infrastructure/Common/RedisDistributedLock.cs:10` (F1 때 개명) | `TODO: application.json 으로 분류 예정` |
| 대사 진행 입력 직접 폴링 | `GUI/Dialogue/DialogueView.cs:51-62` | `Keyboard`/`Mouse` 직접 폴링. (`GameHud` 쪽은 F13 에서 이관 완료) — 상세·선행 결정 = **F13** |
| 툴팁 미연동 | `GUI/Hud/Sub/BattleEffectSlot.cs:17` | Event Trigger 연동 |
| 상시 `Debug.Log` | `CharacterSpawner`(21) · `LobbyModel`(14) · `GameSessionConnector`(13) 등 | 릴리스 빌드 로그 노이즈·성능. 조건부 컴파일/로그 레벨 검토 |

---

## E. 미실측 (결론 내리지 않은 것)

여기 있는 항목은 **"문제 없음"이 아니라 "확인 안 함"** 이다.

- **몬스터→플레이어 지연 실측값** — 관측 배선(`PlayerHpApplied`)은 완료됐지만 트레이스가 기본 Off 라 실제 ms 는 아직 없다. `Tools/Combat/Combat Trace` 에서 Record 켜고 던전 플레이 필요.
- **MPPM 실플레이 육안 확인** — leviathan 보스가 실제 모델로 뜨고 슬램 모션이 나오는지. 가드 테스트로 배선은 고정했으나 사람이 본 적은 없다.
- ~~**B4 의 성능 영향**~~ — 측정 없이 해소됨(2026-08-24). 전량 조회를 DB OFFSET/LIMIT 으로 대체해 질문 자체가 사라졌다. 남은 미실측은 "실제 부하에서의 쿼리 시간"이며 그건 인덱스 관점의 별건.
- **아트 팩 3종의 실사용률** — 3.0GB 중 실제 참조되는 에셋 비율 미측정. C3 선별 커밋 판단의 근거가 된다.

---

## F. 포트폴리오 문서 재편(2026-08-22) 중 코드 대조로 발견 — ⬜ 전부 미착수

> 챕터 28편을 코드와 1:1 대조하며 나온 항목. **전부 "문서가 낡았나" 확인 과정에서 부수적으로 드러난 것**이라
> 기능 개발 중에는 보이지 않던 자리들이다. 근거는 전부 코드 위치로 남긴다.
> ⚠️ **미실측**: 코드 경로상 확인이고 재현 테스트는 돌리지 않았다.

### F1. 던전 방 입장 원자성 유실 — ✅ **해소 2026-08-23**

```
DungeonLobbyService.cs:170-174
    GetPlayersByRoomIdAsync → Count >= MaxPlayers 검사 → CreateAsync
                            └─ 사이를 막는 것이 없다(락 없음)
```

- **왜 문제인가**: ① **정원 초과** — 개수 제약은 DB로 표현 불가, 락도 없음. ② **동시 다중 방 입장** — `AlreadyInRoom` 도 check-then-act 이고 `DungeonRoomPlayerConfiguration.cs:22` 의 `HasIndex(UserId)` 가 **unique 가 아니라** DB도 안 막는다.
- **경위**: 예전엔 Lua 스크립트로 원자 입장(`JoinRoomAtomicResult` −1~−5)을 했는데, 멤버십을 `dungeon_room_players` 연관 테이블로 옮기면서 **락이 함께 이사하지 않았다.** 원자 API `TryJoinRoomAsync` 는 인터페이스에 남았지만 **아무도 호출하지 않고**(테스트 Fake 제외) 구현도 상태 확인만 하는 껍데기다.
- **재료는 이미 있다**: `IUserLock`(`RedisUserLock`, `SET NX EX` + 소유자 토큰 Lua 해제)이 `ChatService.cs:36` 에서 쓰이고 있다. 로비 입장에만 안 걸려 있다.
- 상세 = [chapter-03](../portfolio/chapter-03-dungeon-lobby.md) 3절.

**조치(2026-08-23)** — 축이 둘이라 방어도 둘로 나눴다. 상세 = codemap §2.105.

| 깨지던 축 | 방어 | 위치 |
|---|---|---|
| 정원 초과 (서로 다른 유저의 경합) | 방 단위 락으로 "인원 검사~입장 기록"을 한 임계구역에 | `DungeonLobbyService.JoinRoomAsync` (`RoomLockKey`) |
| 한 유저의 동시 다중 방 입장 | `dungeon_room_players.UserId` **UNIQUE** — 락 만료·다중 인스턴스에도 안 깨지는 최종 방어선 | `DungeonRoomPlayerConfiguration` + 마이그레이션 `MakeDungeonRoomPlayerUserIdUnique` |

- `IUserLock` → **`IDistributedLock`** 으로 개명(구현 `RedisDistributedLock`). 방 스코프에도 쓰이는데 이름·키 프리픽스가 유저 전용이었다. 겸사 키가 `lock:user::chat:user:{id}` 로 콜론이 겹치던 것도 `lock:{scope}` 로 정리.
- UNIQUE 위반은 `PlayerAlreadyInRoomException`(Application)으로 번역해 EF/Npgsql 예외 타입이 Application 까지 새지 않게 했다.
- 마이그레이션은 UNIQUE 를 걸기 전에 기존 중복을 정리한다(없으면 제약 생성이 실패해 서버가 못 뜬다). ⚠️ 처음 쓴 `MIN((JoinedAt, RoomId))` 은 PostgreSQL 에서 `function min(record) does not exist` 로 실패 — `ROW_NUMBER() OVER (PARTITION BY ...)` 로 교체했다(실제로 서버 기동 실패로 밟음).
- **경합 재현 테스트**(`DungeonRoomJoinConcurrencyTests`, Testcontainers 실 Redis+PG) 5건 — 락 상호배제 / 키 스코프 / 정원 동시입장 / UNIQUE 제약 / 서비스 end-to-end.
  - **고장 주입으로 실효성 확인**: 락을 `NoOpDistributedLock` 으로 바꾸자 정원 4인 방에 **8명 전원 입장**(Expected 3, Actual 8)으로 실패 → 복구 후 통과.
  - ⚠️ **동시성 테스트만으로는 UNIQUE 를 검증 못 한다**(실측): `.IsUnique()` 를 떼도 두 요청이 충분히 겹치지 않아 서비스 사전 검사가 먼저 걸러 **통과해 버렸다**. → 저장소 계층(사전 검사 없음) 직접 테스트를 따로 두어 결정적으로 고정. 그 테스트는 `.IsUnique()` 제거 시 `Assert.Throws() Failure: No exception was thrown` 로 정확히 실패한다.
- **남은 것**: 죽은 API `IDungeonRoomRepository.TryJoinRoomAsync`(호출자 0, 구현은 상태 확인만) 는 이번에 건드리지 않았다 — 별건으로 제거 대상.

### F2. Refresh 실패 시 세션 파괴가 DoS 벡터 — ✅ **해소 (2026-08-23)**

```
before                                          after
─────────────────────────────────────────────   ──────────────────────────────────────────────
버전 역행(위조 가능) → Clear + RemoveSession     해시 = 현재 세대  → 정상 회전
해시 불일치          → Clear + RemoveSession     해시 = 직전 세대  → 유예 이내 = 재시도
                                                                  → 유예 경과 = 탈취 → 파괴
                                                둘 다 아님        → 실패만. 세션 유지
```

- **조사 중 드러난 것**: 문제는 해시 불일치 분기만이 아니었다. **버전 역행 검사가 해시 비교보다 앞**에 있어 `aaa.0` 한 방으로 `TokenReuseDetected` + 세션 파괴가 났다. → BCP 의 "무효화를 재사용 탐지로 한정"을 그대로 적용해도 DoS 가 안 없어지는 구조였다.
- **택한 해법**: 파괴를 **소유 증명 뒤로** 옮기고, 직전 세대 해시를 Redis 휘발성 키(`game:user:credential:refresh:prev:{userId}`, 값 `해시:회전시각`, TTL=리프레시 수명)에 한 세대만 보관해 "우리가 발급한 토큰"이라는 증명을 되찾았다. 가용성·재사용 탐지 중 하나를 버릴 필요가 없어져 **선행 결정 자체가 소멸**했다.
- **유예창**(`JwtOptions.RefreshReuseGraceSeconds`, 기본 60s): 응답 유실·타임아웃 재시도로 같은 토큰이 두 번 오는 정상 경로를 탈취로 오판하면 대량 로그아웃이 된다. 유예 안 재시도는 **회전시각을 갱신하지 않는다**(갱신하면 탐지를 무한히 미룰 수 있음).
- **버려진 것**: 버전 역행 비교. 파괴 근거로서는 위조 가능해 제거했다(`RefreshTokenVersion` 은 토큰 접미사로만 남음).
- **검증**: `AuthServiceTests` 15/15 · `GameServer.Tests` 417/417 · `SocketServer.Tests` 224/224 · Unity EditMode 231/231 · PlayMode E2E(`AuthE2ETests` 신규 2건 포함).
- 상세 = [chapter-02](../portfolio/chapter-02-authentication.md) 6·8절.

### F15. 유령 방이 영원히 남는다 — ✅ **해소 (2026-08-24)**

- **실측**: `Waiting 733 / Playing 1275 / Closed 0`. 방을 만들고 앱을 종료하면 소켓이 붙은 적이 없어 `PlayerLeft` 이벤트가 안 나오고, `RemovePlayerFromRoomAsync` 가 영원히 호출되지 않는다.
- **선행 확인**: 세션 기반 판정을 하려다 **생존 신호가 시스템에 없다**는 것을 먼저 확인했다 — `UserSession.LastActiveAt` 갱신 지점 0곳(죽은 필드), `CleanupExpiredSessionsAsync` 호출자 0곳, 클라는 콜드스타트에만 refresh(주기 하트비트 없음).
- **조치**: `session:active` score(= 최근 인증 RPC + AccessToken 수명 근사)를 신호로 쓰되 **유예 2시간**. `DungeonRoomReaper`(10분 주기) → **방마다 새 스코프** → `ReapRoomIfAbandonedAsync(roomId)` → 기존 `RemovePlayerFromRoomAsync` 재사용.
- ⚠️ 처음엔 한 스코프로 전량을 돌렸다가 한 방의 실패가 뒤따르는 방까지 연쇄로 깨뜨리는 것을 실측하고 방 단위로 쪼갰다.
- **남은 것 = F16**(정식 하트비트). 도입되면 유예를 줄일 수 있다.
- 상세 = [codemap.md](codemap.md) §2.107 ③.

### F16. 정식 하트비트가 없다 — ✅ **해소 (2026-08-24)**

- `UserSession.LastActiveAt` 은 로그인 시각으로 고정된 **죽은 필드**이고, 클라이언트는 콜드스타트에만 토큰을 갱신한다. 그래서 "이 플레이어가 아직 살아 있다"를 정확히 말할 수단이 없다.
- 현재 F15 리퍼는 `session:active` score 라는 **근사 신호 + 2h 유예**로 버티고 있다. 오탐을 막으려 유예를 크게 잡은 만큼 정리가 늦다.
- **조치**: ① 서버 — `AuthService.ValidateTokenAsync` 가 인증 성공마다 `TouchSessionAsync` 호출(저장소가 스로틀: 남은 수명 절반 이상이면 미기록). `UserSession.Touch()` 로 `LastActiveAt` 이 살아났다. ② 클라 — `SessionKeepAlive`(ProjectScope EntryPoint)가 남은 수명의 60% 지점마다 토큰을 갱신. **proto 변경 없음**.
- **곁다리로 잡은 진짜 버그**: `AccessTokenMinutes: 60` 인데 클라가 콜드스타트에서만 갱신해 **60분 넘게 플레이하면 이후 전 RPC 가 Unauthenticated** 였다. keep-alive 가 이걸 같이 닫는다.
- **오탐 방지**: `GetSessionActiveUntilAsync` 가 Redis score 없으면 DB `LastActiveAt + AccessToken 수명` 으로 폴백.
- **남은 것**: F15 의 `Grace`(2h) 축소는 아직 안 했다 — 신호가 진짜가 됐으니 다음 라운드에서 조정 가능. `CleanupExpiredSessionsAsync`(호출자 0곳) 존치 여부도 그대로 남았다.
- 상세 = [codemap.md](codemap.md) §2.108.

### F17. Register 롤백이 실패한 Insert 를 재커밋했다 — ✅ **해소 (2026-08-24)**

- `AccountService.RegisterAsync` 의 catch 가 `UserProfileRepository.RemoveAsync` → `SaveChangesAsync` 를 부르는데, 실패한 credential Insert 가 Added 로 남아 **다시 커밋되며 같은 UNIQUE 위반**을 던졌다. 원인이 `INTERNAL_SERVER_ERROR` 로 뭉개지고 고아 레코드가 남았다.
- **조치**: 실패한 저장소가 **자기 엔티티만** Detached 로 되돌린다(`UserCredentialRepository.CreateAsync`).
- ⚠️ 처음엔 `GameServerDbContext.SaveChangesAsync` 전역 오버라이드로 넣었다가 되돌렸다 — 리퍼 실행 중 `ArgumentOutOfRangeException: Unexpected entry.EntityState: Detached` 로 변경 추적기가 깨졌다. 전역 정책이 아니라 국소 수정이 맞다.
- 상세 = [codemap.md](codemap.md) §2.107 ②.

### F18. `AuthFlowE2ETests` 가 전역 `TokenStorage` 를 공유해 전체 실행에서만 흔들린다 — ✅ **해소 (2026-08-24)**

- **실측**: 단독 실행 5/5 통과. 전체 실행에서 `자동로그인_만료된토큰이면_리프레시한다` 가 **1회** `Expected: Success / But was: NeedLogin` 로 실패했고, 같은 코드로 재실행하니 **224/224 통과**했다.
- **원인 미규명**: 간섭 주체를 특정하지 못했다. 확인한 것만 적는다 — ⓐ 서버 회귀가 아니다(단독 통과) ⓑ `SessionKeepAlive` 는 원인일 수 없다(`AccessTokenMinutes=60` → 첫 발화가 로그인 후 ~36분인데 런은 4.5분) ⓒ 이 픽스처는 정적 `TokenStorage`(PlayerPrefs)를 프로덕션 `AuthSession.Update/Clear` 와 **그대로 공유**한다.
- **조치**: `ITokenStore` 를 도입해 `AuthSession` 이 저장소를 **주입받는다**(앱=`PlayerPrefsTokenStore`, 테스트=픽스처 전용 `InMemoryTokenStore`). 정적 `TokenStorage` 는 삭제(호출자 0).
- ⚠️ **이 변경이 그 플래키를 없앤다고 주장하지 않는다** — 간섭 주체는 끝내 특정하지 못했다(재현 안 됨). 다만 프로세스 전역 가변 상태를 테스트 경로에서 없앤 것은 그 자체로 옳고, 간섭이라는 부류 전체를 제거한다.
- 상세 = [codemap.md](codemap.md) §2.109 ①.

### F19. `GlobalInputInitializer` 가 입력 맵을 켜고 끄지 않는다 — ✅ **해소 (2026-08-24)**

```
GlobalInputInitializer.Initialize()   Enable() · Player.Enable() · UI.Enable()
        └─ 해제 지점 없음
루트 teardown → VContainer → PlayerInputActions.Dispose() → asset 파괴
        └─ 파이널라이저 assert: "…Player.Disable() has not been called"
```
- **왜 문제인가**: 그 assert 로그가 **그때 실행 중이던 무관한 테스트에 붙어 실패시킨다**. 실제로 `SessionKeepAliveTests` 가 이것으로 깨져 `LogAssert.ignoreFailingMessages` 로 격리해 둔 상태다(마스크는 근본 수정 후 제거 대상).
- **조치**: `IDisposable` 을 붙여 dispose 시 `Disable()`. `asset == null` 가드는 `Unity.InputSystem` asmdef 참조를 요구해 포기하고, 해제 순서 불확정만 try/catch 로 흡수했다.
- `SessionKeepAliveTests` 의 `LogAssert.ignoreFailingMessages` 마스크 **제거** — 마스크 없이 3/3 통과.
- 상세 = [codemap.md](codemap.md) §2.109 ②.

### F3. 멱등 처리 기록의 TTL 이 컬렉션 전체에 걸린다 (3곳 동일 패턴) — ✅ **해소 (2026-08-24)**

| 위치 | 키 | TTL |
|---|---|---|
| `DungeonResultConsumer.cs:42,48` | `DungeonResultProcessed()` Set | 24h (Set 전체, 추가마다 갱신) |
| `LootGrantConsumer.cs:50,56` | `LootPickupProcessed()` Set | 24h (동일) |
| `ChatMessageRepository.cs:23,411` | `ChatAllMessages()` Sorted Set | RedisCacheTtl (동일) |
| ~~`DungeonRoomRepository.cs:229`~~ | ~~`DungeonRoomActive()` Set~~ | ~~RedisCacheTtl~~ ✅ **2026-08-24 해소** — 목록·카운트를 DB 단일 소스로 바꿔 집합을 근거에서 뺐다(§2.107 ①) |

- **왜 문제인가**: 일정 시간 이벤트가 없으면 **처리 기록이 통째로 만료**된다. 그 뒤 오래된 메시지가 재배달되면 **이중 지급**이 가능하다. 재배달 경로도 실재한다(F4).
- 채팅 쪽은 성격이 조금 다르다 — 인덱스가 만료됐다 새 메시지 하나로 되살아난 상태에서 오래된 `afterMessageId` 로 조회하면 **그 사이 이력이 조용히 빈 채로 반환**된다(컬렉션 조회만 전부-아니면-전무. 단건 `GetMessageByIdAsync` 는 DB 폴백함).
**해소 내역 (2026-08-24)**

- 보상·지급 2곳: **DB 원장으로 대체**(`reward_grants`, `GrantKey` UNIQUE) — Redis 멱등 키는 **제거됐다**.
  - 중간 단계로 `SADD`+`EXPIRE(컬렉션)` → `SET {키}:{항목id} 1 NX EX 86400`(항목별 키)까지 갔었다. 하지만 Redis 키인 한
    키와 지급이 **다른 저장소**라 "지급됐는데 기록이 없다 / 그 반대" 창이 남고, claim-first 는 재시도까지 막았다.
    같은 날 ACK 시점 작업(F4 잔여)에서 지급과 기록을 **한 트랜잭션**으로 묶으면서 그 키들은 삭제됐다.
  - 지금 코드에 보상 멱등용 Redis 키는 **0건**이다. 이 항목을 읽고 `SET NX EX` 를 찾지 말 것 — 진실원은 `reward_grants` 다.
- 채팅: SortedSet 은 항목별 TTL 이 불가능 → **정확성이 TTL 에 의존하지 않게** 바꿨다. ① 인덱스가 경계를 덮는지 검사(`ZCOUNT -inf..afterMessageId > 0`), 못 덮으면 DB 폴백 ② `FetchMessagesByIds` 가 해시 없는 id 를 DB 로 보충.
- ⚠ **조사 중 드러난 사실 — 채팅 구멍은 여기 적힌 것보다 넓었다**: 인덱스 TTL 은 메시지 추가마다 갱신되는데 **메시지 해시 TTL 은 생성 시 1회뿐**이라, "인덱스가 만료됐다 부활한 드문 상황" 이 아니라 **트래픽만 꾸준하면 해시가 먼저 죽어 상시** 조용한 누락이 났다.
- 검증: 신규 통합테스트 6건 RED→GREEN + 서버 전체 **709/709**(이 단계 시점). 원장 대체 뒤 최종 **724/724** — §2.112.
- 상세 = [codemap.md](codemap.md) §2.110 · [chapter-14](../portfolio/chapter-14-dungeon-clear-loop.md) 3절 · [chapter-15](../portfolio/chapter-15-loot-drop-inventory.md) 3절 · [chapter-04](../portfolio/chapter-04-chat.md) 9절.

### F4. Consumer Group PEL 자동 회수(`XAUTOCLAIM`) 부재 — ✅ **해소 (2026-08-24)**

- `StreamAcknowledgeAsync` 는 7개 컨슈머그룹 큐 전부에 있지만 **`AutoClaim`/`StreamPending` 호출이 코드 전체에 0건**이다.
- **왜 문제인가**: ACK 전에 컨슈머가 죽으면 그 메시지는 Pending 목록에 **영구 잔류**한다. 현재는 `StartGameAsync` 의 멱등 재시도가 덮고 있지만 **누군가 다시 시도해야만** 복구된다.
- ~~F3 과 묶인다~~ → F3 은 2026-08-24 선행 해소. 최종적으로 보상 멱등은 **TTL 이 없는 DB 원장**이 됐으므로, 회수가 아무리 늦어도 이중 지급은 없다.

**F3 작업 중 확인한 추가 사실 (2026-08-24, 실측)**

- **GameServer 6개 큐의 컨슈머 이름이 매 기동마다 새 GUID** (`$"{Environment.MachineName}-{Guid.NewGuid():N}"`) 다.
  → 재시작하면 `ReadPendingAsync("0")` 는 **빈 새 PEL** 을 읽는다. "재시작 시 내 PEL 복구" 라는 주석은 **GameServer 쪽에선 사실이 아니다**.
  → 죽은 컨슈머의 PEL 은 회수 주체가 아예 없다. (SocketServer 만 `socket-{MachineName}` 로 안정 — 그쪽은 실제로 복구된다.)
- **ACK 가 핸들러보다 먼저다**(`ProcessEntryAsync`: 역직렬화 → `XACK` → yield → 핸들러). 즉 전달 의미는 at-most-once 이고, 핸들러가 던지면 메시지는 **재배달 없이 소실**된다. PEL 잔류 창은 XREADGROUP~XACK 사이로 좁다.
- 7개 큐가 **같은 컨슈머그룹 루프를 각자 복사**해 갖고 있다(~120줄 × 7). 사용자 승인(2026-08-24): 루프를 `RedisMessageQueueBase<T>` 로 내려 한 벌로 만들고 스윕을 그 1곳에 붙인다.
- ACK 시점(at-most-once → at-least-once) 전환 여부는 보류 — 아래 잔여 참조.

**해소 내역 (2026-08-24)**

- 7개 큐의 복제 루프를 `RedisMessageQueueBase<T>.ConsumeGroupAsync()` **한 벌로 통합**(각 큐 ~120줄 → ~25줄). 회수 로직이 1곳에만 있으면 된다.
- **XAUTOCLAIM 스윕** — 유휴 구간에서만(기본 30s 주기), `MinIdle` 60s 초과분만 회수. 살아 있는 컨슈머의 처리 중 메시지는 빼앗지 않는다(테스트로 고정).
- **컨슈머 이름을 안정화**(`{prefix}-{MachineName}`) — GameServer 6개 큐가 쓰던 매 기동 GUID 는 자기 PEL 복구를 무력화하고 있었다.
- **역직렬화 실패 엔트리도 ACK** — 회수를 붙이면 독이 무한 재시도되므로 함께 처리했다(회수가 만들어낸 새 요구사항).
- 검증: 신규 테스트 4건(GameServer 3 RED→GREEN + SocketServer 1, 실 Redis) · 서버 **713/713** · Docker 리빌드 후 **PlayMode 225/225 · EditMode 231/231 · 클라 컴파일 0**. SocketServer 쪽 테스트는 **고장 주입**(스윕 제거 시 즉시 실패)으로 실효성 확인.
- **운영 기본값 실측(Docker 실환경)**: MULTI 로 죽은 컨슈머를 원자 생성 → SocketServer 그룹 **t+64s**, GameServer 그룹 **t+89s** 에 pending 1→0. 컨테이너 로그에 `Reclaimed 1 stale pending entries …` 확인, A 는 핸들러(`알 수 없는 MapId … 보상 스킵`)까지 도달. 두 값의 차이는 편차가 아니라 **스윕 위상 차**(기동 시각 7초 차)이며 이론 상한 `MinIdle+Interval`=90s 안이다.
- 상세 = [codemap.md](codemap.md) §2.111.

**잔여였던 ACK 시점 — ✅ 해소 (2026-08-24)**

- ACK 를 **핸들러 성공 뒤로** 옮겨 at-least-once 가 됐다(`StreamMessage<T>` 봉투 + 재시도 상한 5회).
- 보상 2경로는 ACK 시점만 바꿔서는 실효가 없었다(claim-first 가 재시도를 막는다) → **`reward_grants` 원장**(GrantKey UNIQUE)을
  지급과 같은 트랜잭션에 넣어 exactly-once 로. 멱등 단위가 메시지 → **참가자**로 내려가 부분 실패 후 나머지만 마저 준다.
- 핸들러 6종 멱등성 감사 결과 5종은 이미 안전, `PlayerConsumedConsumer` 만 비멱등이라 `ConsumeId` 추가로 차단.
- 검증 = 서버 724/724 · PlayMode 225/225 · EditMode 231/231 · 실환경 exactly-once 실측(재발행 시 Exp 변화 0).
- 상세 = [codemap.md](codemap.md) §2.112.
- 상세 = [chapter-05](../portfolio/chapter-05-game-start-e2e.md) 10절.

### F5. `ReportTalk` 에 근접 검증이 없다 — ✅ **재정의·해소 (2026-08-25)**

```
KillMonster  클라 → ClaimMonsterExp(slot) → 서버 슬롯 검증 → 서버 내부 ReportKill  ← gRPC 표면에 없음
TalkToNpc    클라 → ReportTalk(npcId) ────────────────────▶ 진행 +1                ← 검증 없음
```

- `QuestService.cs:51` → `AdvanceMatchingAsync` 가 확인하는 것은 인증·퀘스트 Accepted 상태·`TargetId` 문자열 일치뿐. **실제로 그 NPC 근처에 갔는지 검증하지 않는다.**
- **피해는 제한적**: `AddProgress` 가 `RequiredCount` 상한을 두고 보상은 Claimed 선마킹으로 1회만. 얻는 건 "NPC 까지 걸어가는 시간 절약"이지 무한 파밍이 아니다.
- **그래도 기록하는 이유**: 챕터 19 가 세운 "클라는 진행을 건드릴 수 없다"가 더 이상 전면적으로 참이 아니다.

**⚠ 위 항목의 전제가 틀렸다 (2026-08-25 실측)**

원래 "서버가 NPC 위치를 알고 있으므로 근접 검증을 넣으면 된다"고 적혀 있었다. **둘 다 아니다**:
- 서버는 **NPC 위치를 모른다** — NPC 는 씬 배치(`NPCDialogueInteractable`)고 위치 카탈로그가 없다. 서버가 아는 건 `quests.json` 의 `targetId` 문자열뿐.
- 서버는 **Main 씬 플레이어 위치도 모른다** — Main 은 소켓 미연결(`SocketState != Joined`).
- 대조군 `ClaimMonsterExp` 도 위치를 검증하지 않는다. `(mapId,slotId)` 카탈로그 존재 + Redis 쿨다운(파밍률 상한)뿐이다.

→ **근접 검증은 Main 을 서버 권위로 올려야 성립한다.** 그 전에는 어떤 서버 검증도 위치를 증명하지 못한다.

**해소 내역 (2026-08-25)** — 목표를 "치팅 차단"에서 **"정상 요청만 오게 한다"** 로 바꿔 닫았다(사용자 결정).
- **클라 게이트**: `NPCDialogueInteractable.hasQuest`(저작 플래그)로 잡담 NPC 는 **통신 0회**. 실제 보고 여부는
  서버가 내려준 퀘스트 상태(`QuestInfo.target_id`·`status`)로 판단 — 플래그만 믿지 않는다. proto 무변경.
- **서버 검증**: 카탈로그에 대화 목표가 없는 npcId 는 **DB 를 읽지 않고** 0 + 경고 로그(예전엔 DB 를 먼저 읽었다).
  실패를 Failure 로 만들지 않는다 — 퀘스트 없는 NPC 와의 대화는 정상 행동이다.
- **뺀 것**: per-(user,npc) 쿨다운. `TalkToNpc` 퀘스트가 1개·`requiredCount=1`(실측)이라 막을 파밍이 없다(YAGNI).
- ⚠ **남는 드리프트**: `hasQuest=false` 인 NPC 에 퀘스트를 붙이면 조용히 진행되지 않는다(호출이 아예 안 와서 서버 로그로도 안 잡힘). 툴팁에 경고.
- **남는 한계(재분류)**: "안 걸어가도 대화 퀘스트를 완료할 수 있다" 는 그대로다. 이건 `ReportTalk` 의 결함이 아니라
  **Main 이 클라 권위**라는 구조의 한 단면이므로, 해소 조건은 Main 의 서버 권위 승격이다.
  → **2026-08-25 재개**: NPC 를 제한 범위 이동으로 하기로 해 불가 사유 ①(NPC 위치)이 사라졌다. **B6** 로 추적한다.
- 검증: 서버 728/728 · PlayMode 228/228 · EditMode 239/239. 상세 = [codemap.md](codemap.md) §2.114.
- 상세 = [chapter-19](../portfolio/chapter-19-quest-system.md) 8절.

### F6. `spawn-layouts.json` 이 아직 `Resources.Load` 로 읽힌다 — ✅ **해소 (2026-08-25)**

```
Script/Gameplay/Resources/spawn-layouts.json          ← 클라 사본이 Resources 안
SpawnLayoutProvider.cs:33  Resources.Load<TextAsset>  ← 살아 있는 프로덕션 경로
   소비자: CharacterSpawner · LocalRespawnController · MainMonsterSpawner
```

- **왜 문제인가**: Addressables 전환의 목적이 "Resources = 빌드 항상 포함" 회피였는데, **SO 는 옮겼지만 bake 산출물(TextAsset)은 남았다.** 맵이 늘수록 커지는 데이터라 대가가 가장 큰 축.
- ※ `Assets/Resources/VContainerSettings.asset` 은 프레임워크가 그 경로를 요구하므로 **정상**. 잔존 문제는 이 1건.
- 상세 = [chapter-20](../portfolio/chapter-20-content-pipeline-addressables.md) 8절.

**해소 내역 (2026-08-25)** — 클라는 `MapDefinition`(SO)을 Addressables 로 **직독**하고 Resources 사본은 삭제했다.
- 대가: 클라=저작(SO)·서버=bake(JSON) → **Export 를 잊으면 스폰이 갈린다**. 그래서 전수 대조 가드를 함께 넣었고,
  고장 주입(서버 JSON z 를 -16→-99)으로 `Expected: -16.0 / But was: -99.0` 실패를 확인했다.
- 착수 전엔 안 보이던 것: MapDefinition 8개 중 **Addressable 등록이 4개뿐**이라 그대로 갔으면 `dungeon_03/04/05/e2e` 스폰이 죽었다. 가드가 잡아 4건 등록.
- 검증: 컴파일 0 · EditMode 239/239 · PlayMode 225/225. 상세 = [codemap.md](codemap.md) §2.113.

### F15b. 고아 `PlayerInputActions.cs` 사본 — ✅ **해소 (2026-08-25)**

```
Script/Gameplay/Input/PlayerInputActions.cs   namespace Game.Gameplay.Input   2192줄  ← 살아 있음
                                              (.inputactions 의 wrapperCodePath 가 가리키는 생성물)
Script/Input/PlayerInputActions.cs            namespace Game.Input            2036줄  ← 참조 0건
```

- F13 작업 중 발견. `Game.Input` 네임스페이스를 쓰는 코드가 **0건**이다(실측).
- 생성 설정이 갱신되기 전의 옛 출력으로 보인다. 컴파일 시간·혼동 비용만 있고 기능은 없다.
- **조치**: 삭제 완료 — `Script/Input/` 폴더와 `.meta` 까지 제거(GUID 고아 방지).
  삭제 전 `.inputactions` 의 `wrapperCodePath` 가 살아 있는 쪽(`Script/Gameplay/Input/`)을 가리키는 것을 확인했고,
  `Game.Input` 참조가 0건임을 재확인했다. 검증: 컴파일 0 · EditMode 239/239 · PlayMode 225/225.

### F7. 존재하지 않는 어셈블리를 참조하는 asmdef 3개 — ⬜ 낮음

- `Game.System.DungeonLobby` 를 `Game.Presentation.asmdef` · `Game.System.InGame.asmdef` · `Game.Tests.EditMode.asmdef` 가 참조하는데 **그 이름의 asmdef 를 정의하는 파일이 없다.**
- Unity 가 missing reference 로 무시해 컴파일은 통과한다(Editor.log `error CS` 0건). 인스펙터 경고로만 남는 잔재.

### F8. SocketServer 의 `SessionId` 가 앰비언트 컨텍스트가 아니다 — ⬜ 낮음

- `SocketServer/Program.cs:30` 콘솔 출력 템플릿이 `SessionId={SessionId}` 를 찍는데 `LogContext.PushProperty("SessionId", ...)` 하는 곳이 **한 군데도 없다.** 대신 **21개 호출부가 메시지 템플릿에 직접** `{SessionId}` 를 써넣어 채운다.
- 결과: 그 21곳은 헤더·본문에 **두 번** 찍히고, 나머지 세션 로그는 **빈칸**(예: `Session.cs:195` 경고).
- TraceId 에는 앰비언트 기법을 제대로 적용해 놓고 SessionId 에만 안 했다. 세션 처리 진입점에서 한 번 `PushProperty` 하면 중복도 누락도 사라진다.
- 상세 = [chapter-06](../portfolio/chapter-06-logging.md) 6절.

### F9. `EquipmentType` 공개 계약에 오타가 3곳으로 전파 — ⬜ 낮음 / ❓ 선행 결정

```
Shared.Gameplay/Equipment/EquipmentType.cs:13  Header = 1     ← Helmet/Head 의 오타로 보임
                                          :15  Shoose = 3     ← Shoes 의 오타
equipment.proto:26,28                          EQUIPMENT_TYPE_HEADER / _SHOOSE
클라(Plugins/Shared.Gameplay.dll)              동일 타입 사용
```

- 특히 `Header` 는 프로그래밍에서 전혀 다른 뜻이라 읽는 사람이 오해한다.
- **선행 결정**: proto·직렬화 필드는 CLAUDE.md 가 "명시 요청 없이 변경 금지"로 지정한 공개 계약이다. 고치려면 proto 재생성 + 클라 Generated 갱신 + **이미 저장된 슬롯 값** 확인이 함께 필요.

### F10. 팝업 호출 보일러플레이트 8곳 반복 — ⬜ 낮음

- `SetAddressableOwner` 호출부 8곳이 전부 같은 4단계(`LoadAndInstantiateAsync` → `GetComponent<XxxPopup>()` → `SetAddressableOwner` → `Setup`)를 반복한다. 팝업 헬퍼/서비스는 없다(`ShowAlert`/`PopupService` 검색 0건).
- `AddressableInstance` 가 Addressable 계층에서 없앤 중복이 **팝업 계층에서 다시 생겼다.** `ShowAlertAsync(title, msg, glow)` 하나면 8곳이 1줄로 준다.

### F11. GameServer 헬스체크 엔드포인트 없음 — ✅ **해소** (2026-08-24 코드 확인)

- 기재 당시엔 `MapHealthChecks` 가 없었으나 현재는 배선돼 있다:
  `ServiceInstaller.cs:95` `services.AddHealthChecks()` · `MiddlewareInstaller.cs:31` `app.MapHealthChecks("/healthz")`.
- ⚠️ 미실측: 엔드포인트가 실제로 200 을 반환하는지, Docker Compose `healthcheck` 에 연결됐는지는 확인하지 않았다.

### F12. `Shared.Infrastructure` 네이밍이 규칙과 충돌해 보인다 — ⬜ 낮음 / ❓ 선행 결정

- `GameServer.Application.csproj:10` 이 `Shared.Infrastructure` 를 참조하는데, CLAUDE.md 는 "Application 이 Infrastructure 를 참조하면 위반"이라고 못 박고 있다.
- 실제 내용은 DB 어댑터가 아니라 **임베디드 JSON 정적 카탈로그**(items/abilities/drop-tables/spawn) + 메시지 계약이라 **진짜 위반은 아니다.** 다만 이름만 보고는 위반으로 읽혀서, 이 규칙을 검사하는 사람·에이전트가 오판할 수 있다. `Shared.GameData` 계열이 맞다.
- **선행 결정**: 어셈블리 개명은 참조 그래프 전체 + Dockerfile restore 목록 갱신을 동반한다.

### F13. 입력 임시방편의 전제조건이 이미 해소됐다 — 🔄 **부분 해소 (2026-08-25)** — `DialogueView` 잔존

당시 막고 있던 것(생성 래퍼 미반영)은 **이미 해결됐는데** 마지막 배선만 안 됐다.

```
PlayerInputActions.cs:1290  m_Player_Inventory = FindAction("Inventory")   ← 래퍼 재생성됨 ✅
GameInputAction.ToggleInventory (enum 값)                                   ← 존재 ✅
InputRouter → ToggleInventory 라우팅                                         ← 없음 ❌
GameHud.cs:255              Keyboard.current 직접 폴링                       ← 잔존 ❌
DialogueView.cs:55          동일 패턴                                        ← 2곳째 ❌
```

- 주석 두 곳(`GameInputAction.cs:14`, `InventoryViewController.cs:19`)이 아직 "연결 예정"으로 남아 있다.
- 대조군: **락온은 정식 경로로 갔다**(`.inputactions` → 생성 래퍼 → `<Keyboard>/tab` → `LockOn` 액션). 같은 프로젝트에서 한쪽은 제대로 됐다.

**해소 내역 (2026-08-25)** — i·k·q·g 전부 `.inputactions` → 래퍼 → `InputRouter` → `GameInputAction` → `GameHud.TryHandle` 로 이관.
- ⚠ **"한 곳만 배선하면 끝난다" 는 틀렸다**: ① `InputRouter` 가 **Main 스코프에만** 있어 던전에서 죽는다(→ `InputInstaller` 로 분리해 양쪽 설치)
  ② `.inputactions` 에 `Quest`·`Ability` 액션이 **없었다**(추가 후 래퍼 재생성) ③ 폴링 키는 3개가 아니라 **4개**였다(g).
- 곁다리 누수 수정: `InputRouter` 가 `performed` 구독을 해제하지 않아, 루트 싱글턴 `PlayerInputActions` 에
  씬 왕복마다 죽은 라우터 델리게이트가 쌓이고 있었다. Dispose 해제 + 테스트 고정.
- 검증: 컴파일 0 · EditMode 239/239(신규 5건) · PlayMode 225/225. 상세 = [codemap.md](codemap.md) §2.113.

⚠ **남은 1곳 — `DialogueView` (2026-08-25 확인)**. 위 결함 본문이 "2곳째" 로 적어둔 자리인데 해소 내역에 언급이 없다.

```
DialogueView.cs:51-62  Update() 안에서
    Keyboard.current.enterKey / spaceKey .wasPressedThisFrame
    Mouse.current.leftButton.wasPressedThisFrame        ← 대사 진행(Advance) 입력
```

- GameHud 쪽(i·k·q·g)은 정식 경로로 갔고 `HudToggleRoutingTests` 가드까지 있다. 이 한 곳만 같은 패턴이 남았다.
- 다만 성격이 조금 다르다 — HUD 토글은 **전역 단축키**라 `InputRouter` 가 맞지만, 대사 진행은 **모달이 떠 있는 동안만** 받는 입력이라
  `Advance` 액션을 새로 만들지, UI Submit(`EventSystem`)에 얹을지 **먼저 정할 것**. 판단 없이 `InputRouter` 로 밀면 대사창이 닫혀도 키가 살아 있다.

### F14. 채팅 스트림에 트리밍이 없다 — ⬜ 낮음

- ⚠ **범위가 채팅만이 아니다(2026-08-25 실측)**: `maxLength` 없는 생산 지점은 비테스트 기준 **3곳**
  (`ChatBroadcastChannel.cs:18` · `DungeonRoomBroadcastChannel.cs:13` · `RedisMessageQueueBase.PublishAsync`).
  마지막 것은 공용 발행부라 **여기 하나 고치면 Consumer Group 큐 전부가 유계**가 된다.
- `ChatBroadcastChannel.cs:18` `StreamAddAsync(channel, ...)` 에 `maxLength` 가 없고 스트림 키에 TTL 도 없다. 개별 메시지 Hash·인덱스 Sorted Set 에는 TTL 이 있어(`ChatMessageRepository.cs:408-411`) 캐시 층은 유계인데 **스트림만 무계**다. 장기 가동 시 Redis 메모리를 잠식한다. `XADD ... MAXLEN ~ N` 필요.

### (참고) F 섹션에서 제외한 것 — 이미 다른 항목에 있음

| 재편 중 발견 | 기존 항목 |
|---|---|
| `CharacterMotor.Move` 가 `Time.deltaTime` 직접 읽음 | **B1** |
| ~~`GetRooms` 페이징의 남는 한계(전량 조회)~~ | ~~**B4**~~ ✅ 2026-08-24 |
| HUD 입력 임시 폴링 | **D** (F13 이 보강) |

---

## 착수 순서 제안 (2026-08-25 갱신)

**🔴 높음 0건.** F1·F2·C2·F3·F4·F5·F6·F13(부분)·F11·B4·F15~F19 소진.
**B6**(근접 검증)은 *하지 않기로 결정*해 닫힘 — 미착수가 아니다.
**B7**(Main 위치 지속화) 신설 — 계획된 기능이 새 신뢰 경계를 만든다.

1. **B7** ❓ — Main 위치 지속화. **코드보다 설계 결정이 먼저**(복원 좌표 검증 / 동기화 주기·저장소 / 던전 왕복).
   착수 시점이 곧 신뢰 경계를 정하는 시점이라, 기능을 만든 뒤에 붙이면 늦는다.
2. **F13 잔여 · B3 · C3** — 각각 선행 판단이 하나씩 붙는다(대사 입력 경로 / InteractionSystem 일원화 / SFX 팩).
3. **F14** — 공용 발행부(`RedisMessageQueueBase.PublishAsync`) 한 곳이면 큐 전부가 유계가 된다. 비용 대비 효과가 가장 좋다.
4. **B1 · B2 · F9 · F12** — 전부 ❓ 선행 결정 필요(공개 계약·이동 감각).
5. **F7 · F8 · F10 · D** — 개별 정리.

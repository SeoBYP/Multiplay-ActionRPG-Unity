# 코드 정리 백로그 (2026-08-18 기준)

> "지금 코드에서 문제나 이상한 부분을 다 정리한다"는 트랙의 **입력 문서**.
> 각 항목은 **근거(실측/코드 위치)** 와 **왜 문제인가**, **선행 결정이 필요한지**를 함께 적는다.
> 근거 없이 "정리하면 좋겠다" 수준의 항목은 넣지 않는다 — 그런 건 착수 판단을 흐린다.
>
> 상태: `⬜` 미착수 · `🔄` 진행 · `✅` 완료 · `❓` 선행 결정 필요
> 진실원: 진척은 [plan.md](plan.md), 코드 위치·결정 로그는 [codemap.md](codemap.md).

---

## A. 데이터 정합성 (가장 위험 — 조용히 갈라진다)

### A1. `abilities.json` 저작↔bake 드리프트 — ⬜ **높음**

| 값 | 클라 SO(저작) | 서버 bake | 상태 |
|---|---|---|---|
| `basic_swing` startup/active | **167 / 125** | 200 / 100 | 불일치 |
| `leviathan_attack` startup/active | **213 / 87** | 200 / 100 | 불일치 |
| `combo_a` · `heavy_swing` | 150/100 · 400/150 | 동일 | 일치 |

- **왜 문제인가**: `abilities.json` 은 서버가 임베디드로 읽는 **판정 창·쿨다운의 권위**다. 클라가 167ms 에 히트박스를 열고 서버는 200ms 기준으로 검증하면 던전에서 데미지가 유실·거부될 수 있다.
- **조치**: `Tools/Ability/Export` 재실행 + 서버 재빌드 + Docker 재배포. ⚠ exporter 가 끝에 `EditorUtility.DisplayDialog`(모달)를 띄워 자동화가 그 자리에서 블록된다(AC-E5 함정) → 팝업 없는 경로로 호출.
- **동반 제안**: bake 드리프트를 **CI/테스트로 감지**. 사람이 "export 했나?"를 기억하는 구조는 반드시 한 번은 잊는다.

### A2. 나머지 bake 산출물 4종 미대조 — ⬜ 중간

`drop-tables.json` · `consumable-effects.json` · `spawn-layouts.json` · `level-table.json` 은 이번에 **대조하지 않았다**(미실측). `monsters.json` 은 대조 결과 **불일치 0건**.

- **조치**: A1 과 같은 대조 스크립트를 4종으로 확장 → 그대로 EditMode 테스트화.

### A3. `RemotePlayerCharacter` 머티리얼 누락 — ⬜ 중간

- 프리팹의 SkinnedMeshRenderer `m_Materials` 가 guid `31321ba15b8f8eb4c954353edc038b1d` 를 참조하는데 **프로젝트 어디에도 없다**(Assets 전체·Packages·임포트 아트 팩 3종을 `.meta` 기준 전수 검색). 나머지 14개 참조는 정상.
- **왜 문제인가**: 원격 플레이어가 머티리얼 누락 상태로 렌더된다.
- **조치**: 올바른 머티리얼 재지정 또는 해당 렌더러 정리.

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

### B4. `GetRooms` 페이징의 남는 한계 — ⬜ 낮음(문서화됨)

- 리포지토리가 여전히 **전체 활성 방을 읽고 메모리에서 자른다**. 진짜 O(page) 는 `room:active` 를 Set→Sorted Set 으로 옮겨야 하는데 Redis 키 계약 변경 + 마이그레이션이라 보류 중.
- 페이징으로 실제로 줄어든 것 = 응답 크기 + 플레이어/유저 배치 쿼리 범위.

### B5. `Game.Gameplay.Editor` → `Game.Network` 참조 — ✅ 승인됨(기록용)

- `CombatTraceWindow` 가 트레이스 링버퍼를 읽어야 해서 추가한 **하향 참조**(위반 아님, 2026-07-17 승인). 재검토 시 근거로 참조.

---

## C. 환경·저장소

### C1. 디스크 포화 — ⬜ **높음(차단 요인)**

- C: **931G 중 여유 108MB**. `.git` 이 **14GB**(과거 대용량 에셋 이력).
- **현재 차단하고 있는 것**: 아트 팩 커밋이 **7/34 청크(~620MB / 3.0GB)** 에서 중단. git 자동 `gc`/`repack` 도 실패(`fatal: failed to run repack`) → 느슨한 오브젝트가 계속 쌓인다.
- ⚠ Docker(Postgres/Redis)·Unity 가 동시에 도는 환경이라 0 근처에서 서비스 손상 위험.
- **선택지**: ① 공간 확보 후 이어서 ② 아트 커밋 되돌리고 미추적 유지 ③ 실제 참조 에셋만 선별 커밋.

### C2. `.gitignore` 가 `*.meta` 를 제외 — ⬜ **높음(구조적 함정)**

- `.gitignore:80` 의 `*.meta` 때문에 **신규 에셋마다 `git add -f` 를 기억해야** 한다. 잊으면 클론 시 GUID 가 새로 생성돼 프리팹·머티리얼 참조가 전부 끊긴다.
- Unity 프로젝트에서 `.meta` 는 **소스와 동급**이다. 이 규칙이 왜 들어왔는지 확인하고, 가능하면 `!*.meta` 예외로 되돌리는 게 맞다.

### C3. CA-5 스모크 SFX 가 미커밋 팩에 의존 — ⬜ 중간

- `Ability_BasicSwing` 의 Sfx 이벤트가 `Book of the Dead` 팩 클립을 참조한다. C1 때문에 팩이 7/34 만 올라가 있어 **다른 머신에서 GUID 가 깨질 수 있다**.
- **조치**: 정식 SFX 로 교체하거나, 해당 클립이 포함된 청크까지 커밋 완료.

---

## D. 낮은 우선순위 (정리 대상이지만 위험 아님)

| 항목 | 위치 | 내용 |
|---|---|---|
| gRPC 주소 하드코딩 | `Network/Https/GameApiClient.cs:19` | `TODO: 설정 파일/환경별 주입` — 배포(M6) 전 필요 |
| Redis 락 설정 하드코딩 | `Infrastructure/Common/RedisUserLock.cs:10` | `TODO: application.json 으로 분류 예정` |
| HUD 입력 임시 폴링 | `GUI/Hud/GameHud.cs:203` | Keyboard 직접 폴링 — `InputRouter` 경로로 이관 예정 |
| 툴팁 미연동 | `GUI/Hud/Sub/BattleEffectSlot.cs:17` | Event Trigger 연동 |
| 상시 `Debug.Log` | `CharacterSpawner`(21) · `LobbyModel`(14) · `GameSessionConnector`(13) 등 | 릴리스 빌드 로그 노이즈·성능. 조건부 컴파일/로그 레벨 검토 |

---

## E. 미실측 (결론 내리지 않은 것)

여기 있는 항목은 **"문제 없음"이 아니라 "확인 안 함"** 이다.

- **몬스터→플레이어 지연 실측값** — 관측 배선(`PlayerHpApplied`)은 완료됐지만 트레이스가 기본 Off 라 실제 ms 는 아직 없다. `Tools/Combat/Combat Trace` 에서 Record 켜고 던전 플레이 필요.
- **MPPM 실플레이 육안 확인** — leviathan 보스가 실제 모델로 뜨고 슬램 모션이 나오는지. 가드 테스트로 배선은 고정했으나 사람이 본 적은 없다.
- **B4 의 성능 영향** — 방이 많을 때 전량 조회가 실제로 문제인지 측정한 적 없음.
- **아트 팩 3종의 실사용률** — 3.0GB 중 실제 참조되는 에셋 비율 미측정. C3 선별 커밋 판단의 근거가 된다.

---

## 착수 순서 제안

1. **C1(디스크)** — 다른 모든 작업의 차단 요인. 여기부터 아니면 커밋·빌드가 계속 실패한다.
2. **A1(abilities 드리프트)** — 유일하게 **실제 플레이 동작에 영향**을 주는 항목.
3. **A2 + 드리프트 감지 테스트** — A1 의 재발 방지. 두 개를 붙여서 해야 의미가 있다.
4. **C2(.meta)** — 신규 에셋마다 반복되는 함정 제거.
5. **A3, B3, D** — 개별 정리.
6. **B1, B2** — 선행 결정 후 착수.

# 챕터 16 학습 로그 — Main 싱글 루트 경로 (클라 시뮬·렌더 + 서버 검증 B-lite)

> 3.3 B / 4.6.3. 챕터 15(던전 = 서버 권위 루트)의 후속.
> **이 챕터의 핵심 = 설계가 한 번 틀렸다가 교정된 과정이다.**
> 처음엔 "Main은 싱글이니 클라 권위로 드랍을 판정하고 `GrantItem` gRPC로 지급, 진입점 가드면 충분"이라 봤다.
> 그런데 그 가드는 **무한 파밍 핵**을 못 막았다 — 클라가 몬스터를 무한 스폰해 잡고 `GrantItem`을 연타하면 만렙.
> 그래서 **B-lite 서버 검증**(클라는 "어느 슬롯을 죽였다"만 보고, 서버가 map 데이터로 검증·roll·지급)으로 교정했다.
> 정본 = [main-spawn-claim.md](../wiki/main-spawn-claim.md) / [authority-model.md §4b](../wiki/authority-model.md).

## 한눈에 — 두 경로의 차이 (교정 후)

```
던전 (Co-op·서버권위)                         Main (싱글·클라 시뮬 + 서버 검증)
──────────────────────                        ──────────────────────
몬스터 sim/사망  : SocketServer                몬스터 sim/렌더  : Client(LocalMonster)
드랍 roll        : SocketServer                드랍 roll        : **GameServer**(ClaimKill 시 권위 roll)
바닥/줍기 중재   : SocketServer(경쟁)          바닥 오브        : Client(연출만, 내용은 서버가 결정)
지급(영속)       : GameServer ← Redis Stream   지급(영속)       : GameServer ← gRPC **ClaimKill**
                   (SocketServer 발행)                            (슬롯 검증 + 쿨다운 + 권위 roll)
```

**공통점**: 영속 보상의 **내용·정원을 클라가 정하지 못한다**(둘 다 서버 권위 roll). 던전은 SocketServer가, Main은 GameServer가 그 권위를 가진다. 클라는 "무슨 일이 있었는지"(킬)만 보고하고, "무엇을 받을지"는 서버가 결정한다.

---

## 설계 결정과 근거

### 1. 폐기된 1차 설계 — "싱글이니 클라 권위 + GrantItem 가드" (왜 틀렸나)

처음 논리: 던전은 경쟁·치팅 방지가 필수라 SocketServer가 다 판정하지만, Main은 싱글 PVE라 동기화할 상대가 없으니 클라가 로컬에서 끝내고 줍는 순간 `GrantItem(itemId, qty)` gRPC 1번만 치자. 위조 가능성은 "싱글 + 서버 가드 3겹"으로 수용:

```
GrantItem(item_id, qty)  가드 3겹:
  ① 인증     = AuthInterceptor(JWT)
  ② 수량 상한 = MaxGrantPerCall=99 (gRPC 진입점)
  ③ 정의 검증 = ItemCatalog.Get(itemId)==null 이면 실패
```

**무엇이 구멍이었나** — 이 가드는 *한 호출의 위조 폭*만 제한하지 *호출 빈도*를 막지 못한다. 치터가:
```
LocalMonster 무한 스폰(클라가 결정) → 잡기 → GrantItem(qty≤99) 연타  →  무한 아이템 → 사실상 만렙
```
"싱글이라 안전"은 **진행이 서버 계정에 영속되는 순간** 거짓이 된다 — 솔로 플레이어도 자기 계정을 치팅할 수 있고, 그게 다른 사회적/경쟁 맥락과 닿으면 문제다. 이건 플레이어 HP를 서버 권위로 승격할 때와 **같은 교훈**(authority-model §0: *"클라가 할 수 있다 ≠ 소유해야 한다"*)이었는데, 루트에선 한 박자 늦게 깨달았다.

**근본 정리**: 클라가 저작한 이벤트("내가 잡았다")는 **서버가 그 콘텐츠를 소유하지 않는 한 검증 불가능**하다. rate-limit은 파밍 *속도*만 늦추는 반창고일 뿐 근본 차단이 아니다.

### 2. 교정 설계 — B-lite (서버가 map 데이터를 보유 → 슬롯 단위 검증)

서버가 **map 스폰 데이터를 보유**(이미 던전이 쓰던 `spawn-layouts`에 Main map 추가)하면, 클라의 킬을 **슬롯 단위로 검증**할 수 있다. 클라는 "보상을 달라"가 아니라 **"어느 슬롯을 죽였다(mapId, slotId)"**만 보고한다.

```
LocalMonster(slot) 킬 → 오브 → E 줍기 → ClaimKill(mapId, slotId) ─▶ GameServer
   ① 슬롯 ∈ map ?              (SpawnLayoutTable — 위조 슬롯/맵 거부)
   ② per-user 쿨다운 경과 ?     (Redis SET NX PX — 재청구 차단 = 파밍률 상한)
   ③ 서버 권위 DropTableRoll    (클라 roll 불신 — 보상 내용을 서버가 결정)
   → GrantItemAsync → granted[] 반환
```

- **`SET key NX PX cooldownMs` 한 줄**이 "키 없으면 점유(클레임)+TTL / 있으면 쿨다운 중(거부)"을 원자 처리 — 레이스 없음.
- 파밍률이 **맵 설계치(슬롯 수 ÷ 쿨다운)로 상한**된다. 무한 스폰해도 쿨다운 내 재청구는 보상 0.
- `GrantItem(itemId,qty)` gRPC는 **제거**(치팅 진입점 봉쇄). 도메인 `GrantItemAsync`는 ClaimKill·던전 `LootGrantConsumer`가 계속 쓴다.
- **클라 재스폰**: 슬롯 쿨다운(서버와 동일 값) 후 `MainMonsterSpawner`가 그 슬롯에 몬스터를 다시 스폰 → 재등장 시점에 보상도 다시 가능.

**안 한 것(YAGNI)**: B-full(Main을 서버 권위 풀 시뮬 = 솔로 소켓 세션)은 co-op 오픈월드가 필요해질 때. 지금은 서버가 map 데이터 + 클레임 쿨다운만 보유(실시간 AI 없음).

---

### 3. DropTable·스폰 데이터화 — SO 저작 → bake → 서버 (데이터 진실원 교리)

처음 DropTable은 SocketServer 하드코딩이었다(챕터 15). 클라(디자이너)는 ScriptableObject로 편집하고 싶고, 서버는 `.asset`을 못 읽는다(`Shared.*`는 Unity 밖 DLL). 충돌의 해법 = 이 프로젝트의 **데이터 진실원 교리**([gas-architecture §2.5](../wiki/gas-architecture.md)):

```
[기획자] SO 저작(편집 쉬움)  ─Export bake→  *.json(서버 임베디드, 기획자 비노출)  →  서버가 읽어 검증
```

이 교리를 세 종류 데이터가 동일하게 따른다:

| 데이터 | 저작 SO | bake | 서버 로드 |
| --- | --- | --- | --- |
| 드랍 | `DropTableDefinition` | `drop-tables.json` | `DropTableCatalog`(Shared.Infrastructure) |
| 스폰/슬롯 | `MapDefinition.MonsterSpawn`(+`slotId`/`respawnCooldownMs`) + 맵 에디터 마커 | `spawn-layouts.json` | `SpawnLayoutTable` |
| 소모품 회복 | `ConsumableCatalog` | `consumable-effects.json` | `ConsumableEffectCatalog` |

- roll *로직*은 결정성을 위해 공유 DLL `Shared.Gameplay.DropTableRoll`(순수)에 둔다 — 던전·Main이 같은 함수. B-lite에선 이 함수를 **서버(ClaimKill)가** 호출한다(클라 roll은 제거).
- `slotId`/`respawnCooldownMs`는 처음 JSON·런타임 파서에만 넣었다가, "**기획자가 SO에서 못 보고 재-export 시 날아간다**"는 지적으로 `MapDefinition` SO + 맵 에디터 마커까지 round-trip하도록 보완했다. 교리는 "데이터가 저작 SO에 있어야 한다"까지가 완성.

---

### 4. Main 로컬 전투는 던전 컴포넌트를 재사용하지 않는다

던전 `MonsterEntity`·`GroundItemEntity`는 "서버 명령을 받아 표시·중계"하는 전용(보간, `C_PickupItem`). Main은 클라가 *시뮬·렌더*하는 역할이라 근본이 다르다. 억지로 합치면 분기로 더 복잡해진다 — 그래서 별도 컴포넌트:

```
LocalMonster   : HP·간단 AI(Chase + 근접 공격 → 플레이어 ASC 즉발 피해)·TakeDamage→OnDied + slotId/mapId
LocalCombat    : PlayerCharacterAgent.OnAttackPerformed 구독 → Physics.OverlapSphere 수집
                 → 서버와 동일 HitboxMath.Overlaps(basic_swing) 정밀 판정 → TakeDamage
LocalGroundItem: IInteractable, E 줍기 → IInventoryGrpcService.**ClaimKillAsync(mapId, slotId)** → granted 반영
MainMonsterSpawner: 비-Joined(Main)일 때만 슬롯 기반 스폰, OnDied → 오브 스폰 + 슬롯 쿨다운 후 재스폰
```

**재사용한 것은 "로직"이지 "컴포넌트"가 아니다** — 적중 `HitboxMath`, roll `DropTableRoll`은 던전 서버와 같은 `Shared.Gameplay` DLL. 단, B-lite 후 **roll은 클라가 아닌 서버가** 호출한다(LocalGroundItem은 슬롯만 들고 ClaimKill).

> 플레이어 사망/리스폰도 이 경로에 합류했다(2.5.1): `LocalMonster`의 근접 공격이 플레이어 HP를 깎아 HP0이면 `PlayerCharacterAgent`가 다운(State.Dead 게이트), Main 전용 `LocalRespawnController`가 일정 시간 후 부활. 던전은 이 컨트롤러를 등록하지 않아 다운잠금 유지(의도된 비대칭).

---

### 5. 몬스터 수집은 레지스트리 대신 Physics

`LocalCombat`이 때릴 대상을 별도 리스트로 관리할 수도 있지만, 몬스터엔 콜라이더가 있으니 `Physics.OverlapSphere` + `GetComponentInParent<LocalMonster>`로 광역 수집 후 `HitboxMath`로 정밀 판정하면 레지스트리 동기화 없이 끝난다. Unity 관용 — YAGNI.

---

## 트러블슈팅 (실제 디버깅)

### 슬롯이 처치 후 영구히 비었다 — 클라 재스폰 누락
서버 ClaimKill 쿨다운(재청구 차단)은 넣었지만, 클라 `MainMonsterSpawner`가 슬롯을 시작 시 1번만 스폰해 다 잡으면 필드가 영구히 비었다. → `ScheduleRespawn`(`UniTask.Delay(RespawnCooldownMs)` 후 재스폰, `_cts`로 Dispose 취소)로 보완. 서버 쿨다운과 같은 값이라 재스폰 시점에 보상도 다시 가능 = 클라·서버 일관.

### 런타임 타입명이 저작 SO 타입과 충돌 — `MonsterSpawn` 중복
spawn-layouts 파싱 결과용 런타임 타입을 `MonsterSpawn`으로 지었는데, 같은 네임스페이스에 저작 SO 타입 `MapDefinition.MonsterSpawn`이 이미 있어 `CS0101`. → 런타임 타입을 `MonsterSlot`으로 개명(저작 SO ≠ 런타임 파싱 결과, 두 레이어 구분).

### `Game.System` 네임스페이스가 `System`을 가렸다
`Game.Gameplay.Character` 안에서 `System.Random`의 `System`이 `Game.System`으로 해석돼 컴파일 에러 → `global::System.Random`. (CLAUDE.md 테스트 규칙이 경고하던 함정이 런타임 코드에서도.)

### Docker 빌드만 실패 — 전이 의존 누락(`NETSDK1004`)
`Shared.Infrastructure → Shared.Gameplay` 참조 추가 후 로컬 sln은 통과하나 GameServer Docker만 실패. Dockerfile의 선택 restore 목록에 `Shared.Gameplay.csproj`가 없어서. → COPY 한 줄 추가. 교훈: 공유 프로젝트 참조 그래프를 바꾸면 Dockerfile 선택 restore 목록도 갱신. 로컬 그린 ≠ Docker 그린.

---

## 검증

| 영역 | 방식 |
| --- | --- |
| ClaimKill 슬롯/쿨다운/서버roll/미인증 | 단위 `MainSpawnClaimServiceTests` + `InventoryGrpcServiceTests`(GameServer) |
| 드랍 roll 확률/수량 | 단위 `DropTableRollTests`(Shared.Gameplay) |
| 임베디드 데이터 파싱(SO→Export 반영) | 단위 `DropTableCatalogTests`(SocketServer) |
| Main ClaimKill 체인(정상지급·쿨다운차단·위조슬롯) | E2E `MainLootE2ETests` 3종(Docker) |
| 적중 판정 | 단위 `HitboxMathTests`(Shared.Gameplay) |
| 슬롯 스폰→킬→ClaimKill 지급(potion+gold)→5s 재스폰→쿨다운 차단, 다운→3s 부활 | **플레이 검증(사람)** |

전체 서버 솔루션 **374 그린**(Docker 리빌드 후) + Unity PlayMode E2E + 플레이. 자동 테스트(로직·서버 계약)와 플레이(MonoBehaviour 글루)가 상호 보완.

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
| --- | --- |
| 클라가 할 수 있다 ≠ 소유해야 | 싱글이라 클라가 판정 *가능*하지만, 영속 보상은 서버가 *소유*해야(파밍 핵 차단) |
| GrantItem → ClaimKill | itemId/qty 클라 지정(파밍 핵) 폐기 → mapId/slotId만 보고, 서버가 검증·roll·지급 |
| B-lite | 서버가 map 데이터 보유 → 슬롯/쿨다운 검증 + 권위 roll. 실시간 AI는 클라(B-full은 YAGNI) |
| Redis SET NX PX | per-(user,slot) 쿨다운을 원자적으로 점유 = 재청구(파밍) 차단 |
| 데이터 진실원 교리 | 드랍·스폰·소모품 = SO 저작 → bake → 서버 읽어 검증(gas-architecture §2.5) |
| 슬롯 라운드트립 | slotId/respawnCooldownMs를 MapDefinition SO + 맵 에디터 마커까지(재-export 보존) |
| 클라 재스폰 | 슬롯 쿨다운 후 MainMonsterSpawner 재스폰(서버 쿨다운과 동일 값) |
| 컴포넌트 분리 | LocalMonster/LocalCombat/LocalGroundItem = 클라 신규(던전 보간 컴포넌트 재사용 X) |
| Physics 수집 | 레지스트리 대신 OverlapSphere(Unity 관용, YAGNI) |
| `global::System` / `MonsterSlot` 개명 | 네임스페이스·타입명 충돌 회피(저작 SO ≠ 런타임 타입) |
| Docker 전이 의존 | 참조 그래프 변경 시 Dockerfile 선택 restore 목록도 갱신(로컬 그린 ≠ Docker 그린) |

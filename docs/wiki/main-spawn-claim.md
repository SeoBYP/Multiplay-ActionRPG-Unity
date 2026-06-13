# Main 획득 서버 검증 — B-lite 스폰-클레임

> 결정 정본 = [authority-model.md §4b](authority-model.md). 데이터 진실원 교리 = [gas-architecture.md §2.5](gas-architecture.md).
> 이 문서 = 그 결정의 **구현 스펙**(스키마·proto·서버 로직·클라·테스트).

## 문제 (무한 파밍 핵)

Main(싱글)은 클라가 몬스터 스폰·킬·드랍을 로컬 소유하고 `GrantItem(itemId, qty)` gRPC 로 영속 보상을 **직접 지정**했다. 서버 가드 = 인증 + 호출당 수량상한(≤99) + catalog 검증뿐 — 호출 빈도·킬 증명이 없어 **클라가 몬스터 무한 스폰 → 무한 GrantItem = 무한 파밍(만렙 핵)**.

## 결정 = B-lite

서버가 **map 스폰 데이터를 보유**(SO→bake→Shared 교리)하고, 클라의 킬을 **슬롯 단위로 검증**한다. 서버는 Main 실시간 AI 는 안 돎(map 데이터 + per-user 클레임 상태만 보유 = 부분 서버 상태).

**핵심 전환**: 클라는 "보상을 달라(itemId,qty)"가 아니라 **"어느 슬롯을 죽였다(mapId,slotId)"**만 말한다. roll·정원·쿨다운·grant 는 전부 서버 → 파밍률이 맵 설계치(슬롯수÷쿨다운)로 상한.

## 컴포넌트 배치 + 의존 방향

```
                Shared (SO 저작 → bake → 임베디드, 교리 §2.5)
                  spawn-layouts.json : map → [SpawnSlot{ slotId, monsterType, pos, cooldownMs }]
                  drop-tables.json   : monsterType → drops[]
                       ▲ 클라=SO/Resources 미러         ▲ 서버=SpawnLayoutTable / DropTableCatalog
   ┌────────────────────┴───────────┐  gRPC   ┌──────────┴───────────────────────────────────────┐
   │ CLIENT (Main, 소켓 세션 없음)   │ ──────▶ │ GameServer                                          │
   │ MainMonsterSpawner ─ 슬롯 스폰   │         │  InventoryGrpcService.ClaimKill                     │
   │   └ LocalMonster{slotId,mapId}  │         │   └ IMainSpawnClaimService (App)                    │
   │      킬 → 오브(LocalGroundItem) │         │      └ MainSpawnClaimService (Infra)                │
   │      E 줍기 → ClaimKill(map,slot)┼────────▶│         ├ SpawnLayoutTable.Get  슬롯검증           │
   │   ◀ granted[] 반영              │ ◀────── │         ├ Redis SET NX PX        쿨다운(원자)       │
   │                                │         │         ├ DropTableCatalog.Roll  서버 roll          │
   └────────────────────────────────┘         │         └ IInventoryService.GrantItemAsync → DB     │
   권위: 클라=스폰·렌더·예측                    └─────────────────────────────────────────────────────┘
         서버=슬롯검증·쿨다운·roll·grant(영속 보상의 진실)
```

## 정상 흐름

```
LocalMonster(slot3) 죽음 → 오브(slotId=3) → E 줍기 → ClaimKill("main_field_01", 3) ─▶ GameServer
   SpawnLayoutTable.Get(map) → slot3{monsterType=slime, cd=5000}   슬롯 ∈ map? ✓
   SET mainclaim:{u}:{map}:3 "1" NX PX5000  → OK(키 없었음)
   DropTableCatalog.Roll("slime") → [potion_hp_small x1]   ← 서버 권위 roll
   GrantItemAsync(u, potion_hp_small, 1) → newQty
   ◀ ClaimKillResponse{ OK, granted=[{potion_hp_small,1,newQty}] } → 클라 인벤토리/토스트
```

## 치팅 차단 (같은 게이트가 전부 막음)

```
A 쿨다운 내 재청구: ClaimKill(slot3) 2회차(즉시) → SET NX = nil(키존재) → Reject(granted=[])
B 위조 슬롯       : ClaimKill(slot=999) → SpawnLayoutTable: 999 ∉ map → Reject("invalid slot")
C 위조 맵         : ClaimKill(map="hack") → Get → KeyNotFound → Reject
 ⇒ 파밍률 = (슬롯수 ÷ 쿨다운) 상한. 무한 불가.
엣지(합법 재스폰): 키 TTL(=cd) 만료 후 재청구 → SET OK → 보상 ✅ (설계된 파밍률 내)
```

## 던전 vs Main (분기는 "스폰 권위"뿐, 검증은 둘 다 서버)

```
              스폰 권위     킬 판정      보상 roll·grant       클라 역할
 던전(Co-op)  서버(Room)   서버 hitbox  서버(LootGrant)       렌더(MonsterEntity 보간)
 Main(싱글)   클라(Local)  클라 로컬    서버(ClaimKill) ★     스폰·렌더·예측 + 슬롯 클레임
   ★ = 이번 B-lite로 클라→서버 이동(무한파밍 차단). map 데이터는 둘 다 서버 보유.
```

## 구현 증분

### 선행 (Part 0) — 회복 단일소스 클라측 (회귀 복구)
코드시드(`potion_hp_small`) 제거로 깨진 던전 회복 미러 복구. 상세 = codemap §2.6c.
- `ConsumableCatalogSeeder : IInitializable` — SO `ConsumableCatalog` → DI `GameplayEffectCatalog.Register`. Dungeon+Main 스코프 등록.
- Editor `ConsumableEffectExporter` — SO → `consumable-effects.json` bake(`DropTableExporter` 자매).

### B-lite
| # | 작업 | 위치 |
|---|------|------|
| ① | spawn-layouts 스키마 +`slotId`+`respawnCooldownMs`(additive, 던전 무영향) + Main map(`main_field_01`, count=1 슬롯). **저작 = `MapDefinition.MonsterSpawn` SO + `MonsterSpawnMarker`(맵 에디터) → `MapDataExporter` bake → JSON**(교리 §2.5) | `MapDefinition.cs`·`MonsterSpawnMarker.cs`·`MapDataExporter.cs`·`MapEditorWindow.cs`·`SpawnLayoutTable.cs`·`MonsterSpawnDef.cs`·`spawn-layouts.json` |
| ② | 클라 슬롯 기반 스폰 — `MainMonsterSpawner`가 `SpawnLayoutProvider.Get(mainMap).Monsters` 순회, `LocalMonster{slotId,mapId}` | 클라 |
| ③ | `ClaimKill(mapId,slotId)` gRPC + `IMainSpawnClaimService`/`MainSpawnClaimService`(슬롯검증+Redis NX쿨다운+서버roll+grant) | proto·GameServer App/Infra/API |
| ④ | 클라 줍기 → `ClaimKill` 교체 — `LocalGroundItem.Initialize(mapId,slotId)`, 클라 roll 제거(서버가 roll) | 클라 |
| ⑤ | `GrantItem` gRPC 제거(치팅 진입점 봉쇄). 도메인 `GrantItemAsync`는 ClaimKill·던전 LootGrant 가 유지 | proto·`InventoryGrpcService`·클라 Generated |
| ⑥ | 테스트 — 서버 단위(슬롯/쿨다운/roll/미인증) + E2E(정상지급·쿨다운거부) | `GameServer.Tests`·E2E |

### Redis 키
```
mainclaim:{userId}:{mapId}:{slotId}   값="1"   SET ... NX PX {respawnCooldownMs}
  존재=쿨다운중(거부) / 없음=클레임(SET 성공) → 원자적, TTL 자동만료
```

### 안 한 것 (YAGNI)
- B-full(Main 서버 권위 풀 시뮬: 서버가 몬스터 스폰·AI 소유) = co-op 오픈월드 필요 시.
- rate-limit 단독(반창고) — 슬롯/쿨다운 검증이 근본.

## 구현 상태 (2026-06-13)

**서버 — 완료·검증**(GameServer 빌드 0오류 / 단위 14 그린: `MainSpawnClaimServiceTests` 4 + `InventoryGrpcServiceTests` ClaimKill 4 + ConsumeItem 6 / SocketServer 89/89 던전 무영향):
- 스키마: `MonsterSpawnDef`+`SpawnLayoutTable` 에 `SlotId`/`RespawnCooldownMs`(additive) + `spawn-layouts.json` Main map `main_field_01`(slime 슬롯 1~3, cd 5000).
- `IMainSpawnClaimService`/`MainSpawnClaimService`(슬롯검증+쿨다운+서버roll+grant) + `IClaimCooldownStore`/`RedisClaimCooldownStore`(SET NX PX).
- gRPC `ClaimKill` 추가 / `GrantItem` 제거(proto·`InventoryGrpcService`·클라 Generated·ClientCodegen 래퍼 재생성) + DI.
- Part 0 회복 단일소스 서버측: `ConsumableEffectCatalog`(임베디드 JSON) + `CombatEffectCatalog` 흡수 + `GameplayEffectCatalog` potion 시드 제거.

**클라 — 코드 완료·Unity 컴파일/플레이 검증 대기**(unity-mcp 미연결):
- `SpawnLayoutProvider`+`MapSpawnLayout` 에 `Monsters`/`MonsterSpawn` 파싱 / `LocalMonster.Configure(slotId,mapId)` / `MainMonsterSpawner` 슬롯 기반 스폰 + 클레임 드랍(클라 roll 제거) / `LocalGroundItem` → `ClaimKill` / `MainLifetimeScope` 설정.
- Part 0: `ConsumableCatalogSeeder`(SO→DI `GameplayEffectCatalog`, Dungeon+Main 등록) + Editor `ConsumableEffectExporter`(SO→JSON).
- E2E 재작성: `MainLootE2ETests`(ClaimKill 정상/쿨다운차단/위조슬롯 3종) · `InventoryE2ETests`(GrantItem 제거) · `SocketE2ETests`(포션 시드 ClaimKill 대체).

**서버 검증(2026-06-13)**: ✅ 양 서버 Docker 리빌드·기동(healthy) + **전체 서버 솔루션 374 그린**(Shared 30 / SocketServer 89 / GameServer 255 — Testcontainers 통합·E2E 포함). ClaimKill 슬롯/쿨다운/roll 은 단위(`MainSpawnClaimServiceTests` 등)로 커버.

**Unity PlayMode E2E(2026-06-13)**: ✅ **그린(사용자 확인)** — `MainLootE2ETests` 3종(정상지급·쿨다운차단·위조슬롯)·`SocketE2ETests` 회복. Unity 컴파일도 통과.

**남음(플레이 검증)**: MonoBehaviour 글루(입력·콜라이더·연출) 시각 검증 — Main 씬 `MainLifetimeScope` 프리팹/`mainMapId` 할당 후: 슬롯 스폰→몬스터 추격·공격→피격 다운(로그)→3s 부활→처치 오브 E 줍기→ClaimKill 지급, 쿨다운 파밍 차단. `ConsumableCatalog` 변경 시 Tools/Consumables/Export 재bake.

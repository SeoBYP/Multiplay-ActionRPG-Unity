# 루트/드랍 + 줍기 설계 (3.3) — 설계 문서 (구현 전)

> 상태: **설계만**(2026-06-07). 구현 미착수. 전제: 인벤토리 도메인(3.1) CRUD 완성됨(`GrantItemAsync` 진입점 존재).
> 핵심 결정: ① **월드 드랍 + 줍기**(자동지급 아님 — 플레이어가 "먹을지" 선택) ② **roll=SocketServer(월드)·grant=GameServer(인벤토리)** 분리 ③ **씬 무관 설계** — 던전/Main 공통, 단 Main 몬스터는 4.6(네트워크 씬) 의존.

---

## 1. 권위 모델 — 왜 이렇게

| 책임 | 주체 | 이유 |
|---|---|---|
| 몬스터 사망 판정 | SocketServer (월드 권위) | 이미 서버 권위(M3). 치팅 방지 |
| 무엇이 드랍되나 (roll) | **SocketServer** | 월드(바닥 아이템)는 SocketServer가 소유. 모든 클라가 같은 드랍을 봐야 함(일관성) |
| 바닥 아이템(GroundItem) | SocketServer Room/World | 위치·점유·경쟁 줍기를 서버가 중재 |
| 줍기 확정 (누가 가져가나) | SocketServer | 경쟁(co-op 동시 줍기) 중재 = 1회만 |
| 인벤토리 지급 | **GameServer** | 인벤토리·아이템정의·영속은 GameServer 소유(3.1). 서버 간 직접 호출 금지 → Redis Stream |
| 획득 알림(토스트) | SocketServer → 클라(TCP) | 픽업 확정 시점에 즉발. 인벤토리 수치 갱신은 클라가 GetInventory pull |

→ **"roll도 grant도 서버"** 인데 *역할이 다름*: SocketServer는 **월드(드랍·바닥·줍기 중재)**, GameServer는 **인벤토리(영속 지급)**. 둘이 Redis Stream으로만 연결(직접 RPC 금지 규칙).

## 1.1 서버별 책임 (확정 — 무엇이 다른가)

> 한 줄 요약: **SocketServer = 월드(실시간·드랍·바닥·줍기), GameServer = 인벤토리(영속·지급·정의검증).** 둘은 Redis Stream 단방향(Socket→Game)으로만 연결.

| 항목 | **SocketServer** (월드·실시간·TCP) | **GameServer** (영속·도메인·gRPC) |
|---|---|---|
| 소유 상태 | Room/World 런타임: 몬스터·**바닥아이템(GroundItem)**·플레이어 위치 | DB `inventory_items` · 아이템정의 `ItemCatalog` · Redis 캐시 |
| 드랍 roll (무엇이 떨어지나) | ✅ `DropTable` 확률 roll | ❌ 전투 루프에 없음 |
| 바닥 GroundItem 스폰·위치·제거·브로드캐스트 | ✅ | ❌ 월드를 안 가짐 |
| 줍기 경쟁 중재 (1명만 성공) | ✅ 락/Interlocked | ❌ |
| 획득 토스트 push | ✅ `S_ItemPickedUp`(TCP) | ❌ 클라와 실시간 연결 없음 |
| **인벤토리 지급(영속)** | ❌ DB 접근 없음·도메인 아님 | ✅ `GrantItemAsync` → `inventory_items` |
| 멱등(at-most-once) | (픽업 1회 보장) | ✅ `PickupId` Redis SET claim |
| 아이템 정의 검증 | ❌ itemId **문자열만** 앎 | ✅ `ItemCatalog.Get(itemId)` |
| 통신 | → Stream **발행만** | ← Stream **소비만** |

**절대 금지(경계 위반)**:
- SocketServer가 DB/인벤토리 직접 쓰기 ❌ · SocketServer → GameServer 직접 RPC ❌(Stream만)
- GameServer가 월드/바닥아이템/실시간 상태 보유 ❌ · GameServer가 전투 중 드랍 결정 ❌

**경계 넘는 데이터(단방향 Socket→Game)**: `ItemPickedUpMessage { UserId, ItemId(string), Qty, PickupId }` — 딱 이것만.

**DropTable이 왜 SocketServer인가**: roll은 *바닥 스폰*과 한 몸이고 *전투 틱 루프 안*에서 일어남. roll엔 **itemId 문자열 + 확률**만 필요(이름·아이콘·MaxStack 같은 *정의*는 불필요) → SocketServer가 `ItemCatalog`를 몰라도 됨. 드리프트(없는 itemId)는 GameServer가 grant 시 `ItemCatalog`로 검증·실패처리 → **안전**.

## 1.2 Client 책임 (확정)

> Client = **얇은 표시 + 입력(의도)** 만. 어떤 권위 결정(roll·줍기 승자·지급)도 안 함. "먹을지" 선택 = *요청을 보낼 뿐*, 결정은 서버.

| 항목 | Client | 비고 |
|---|---|---|
| 바닥 아이템 렌더 | ✅ `S_SpawnGroundItem` → `GroundItemEntity` | 표시만 |
| 근처 감지 + 줍기 프롬프트 | ✅ `InteractionDetector` + `IInteractable` | "F로 줍기" |
| 줍기 **의도** 송신 | ✅ `C_PickupItem{GroundId}` | "먹을지" 선택 = 요청만, **결정은 서버** |
| 바닥 제거 렌더 | ✅ `S_GroundItemRemoved` | |
| 획득 토스트 | ✅ `S_ItemPickedUp` 표시 | |
| 인벤토리 수치 갱신 | ✅ `GetInventory`(pull) | **진실 = 서버 DB** |
| 드랍 roll / 줍기 승자 / 인벤토리 쓰기 | ❌ | 전부 서버 |
| 자기 인벤토리 상태를 진실로 주장 | ❌ | 치팅 방지 |

## 1.3 "획득 = Create 혹은 Update" (CRUD 매핑 확정)

획득(줍기 확정)이 GameServer에 닿으면 **`GrantItemAsync` → `Repo.AddQuantityAsync`** 가 처리하며, **이게 C 와 U 를 둘 다 자동 분기**한다 (이미 3.1 구현됨):

```
GameServer LootGrantConsumer → IInventoryService.GrantItemAsync(userId, itemId, qty)
   → Repo.AddQuantityAsync(userId, itemId, qty, maxStack):
       (userId, itemId) 행 없음 → ① Create  : INSERT inventory_items (신규 슬롯)
       (userId, itemId) 행 있음 → ② Update  : Quantity += qty (maxStack clamp)
   → SaveChanges → 캐시 DEL
```

- **Main이든 던전이든 GameServer 로직은 동일** — GameServer는 **씬을 모른다**. 픽업 메시지(`UserId·ItemId·Qty`)만 오면 위 Create/Update를 그대로 수행.
- Main의 특수성은 *"몬스터·줍기가 발생하느냐"*(= 4.6 네트워크 World) 뿐. **지급(C/U) 자체는 씬 무관·동일.**
- 즉 사용자가 말한 "Main에서 획득하면 Create/Update" = **이미 `AddQuantityAsync`가 보장**. 드랍에서 추가로 만들 건 *그걸 호출하는 LootGrantConsumer* 뿐.

## 1.4 던전 vs Main 드랍 경로 (확정 — 2개 경로)

> 던전 = **Co-op·서버권위**(SocketServer 시뮬→Stream→GameServer). Main = **싱글·클라로컬**(Client 시뮬→**Client gRPC 직접**→GameServer). **공통점은 지급(GameServer Create/Update)뿐**, 그 앞단은 완전히 다름.

```
던전 (Co-op·서버권위)                    Main (싱글·클라로컬)
──────────────────────                   ──────────────────────
몬스터 sim : SocketServer Room            몬스터 sim : Client 로컬        ← SocketServer/Stream 불필요
드랍 roll  : SocketServer                  드랍 roll  : Client 로컬
바닥/줍기  : SocketServer 중재(경쟁)        바닥/줍기  : Client 로컬(경쟁 없음=싱글)
지급(영속) : GameServer ← Redis Stream     지급(영속) : GameServer ← Client gRPC `GrantItem`
             (SocketServer가 발행)                      (Client가 직접 호출)
```

- **Main은 4.6 네트워크 World가 불필요** — 싱글 로컬이라 SocketServer 안 거침. (이전 가정 정정: Main 몬스터 ≠ 서버 World 의존.)
- **Main 지급 = 클라 신뢰 + 서버 경계(가드)** (포트폴리오 결정 2026-06-07):
  - 신규 **`GrantItem(itemId, qty)` gRPC**(인증 필요). 단일 PVE라 클라가 드랍 결정 후 직접 호출.
  - **서버 가드(치팅 최소화)**: ① `ItemCatalog`에 있는 itemId만(이미 `GrantItemAsync`가 검증) ② 호출당 수량 상한(예 ≤ N) ③ (선택) 레이트리밋 ④ (선택) "드랍 가능" 아이템 화이트리스트.
  - 위조 가능성은 인지하되 **싱글 PVE 구간 + 경계**로 수용. 던전(co-op)은 클라 신뢰 0(서버권위).

## 1.5 Main 경로 상세 — Client가 어디까지 역할하나 (확정)

> 핵심: **Main에선 던전의 SocketServer 역할(몬스터 sim·전투판정·드랍·줍기)을 Client가 로컬로 떠안는다.** 싱글이라 동기화·경쟁이 없어 가능. GameServer는 **지급 1회**만 관여, SocketServer는 **미관여**.

| 단계 | 던전(누가) | **Main(누가)** | 비고 |
|---|---|---|---|
| 몬스터 스폰 | SocketServer | **Client 로컬** | Main 맵 스포너 |
| 몬스터 AI·이동 | SocketServer(`MonsterAiMath`) | **Client 로컬** | 던전은 서버 ← 가장 큰 차이 |
| 전투 피격·HP·사망 판정 | SocketServer(서버권위) | **Client 로컬** | Main 몬스터는 클라 권위 |
| 드랍 roll | SocketServer | **Client 로컬**(`DropTable`) | 같은 DropTable 코드 공유 |
| 바닥 아이템 스폰·렌더 | SocketServer→브로드캐스트 | **Client 로컬 생성·렌더** | 남에게 보일 필요 없음(싱글) |
| 줍기 | SocketServer 경쟁중재 | **Client 로컬**(경쟁 없음) | |
| **지급(영속)** | GameServer ← Stream | **GameServer ← `GrantItem` gRPC** | **유일한 서버 통신** |
| 가드(검증) | (서버가 다 권위) | GameServer: catalog·수량상한·auth | 클라신뢰 보완 |
| 획득 토스트 | SocketServer push | **Client 로컬** | |
| 인벤토리 수치 | GetInventory(pull) | GetInventory(pull) | 진실=DB(동일) |

**Main Flow**
```
[Main 씬 — Client 로컬 권위: 몬스터/전투/드랍/줍기 전부]
 플레이어가 몬스터 처치 (Client 로컬 판정)
   → Client: DropTable.Roll(monsterId) → 바닥 아이템 생성·렌더(로컬, 남에게 안 보냄)
   → 플레이어 줍기 (로컬, 경쟁 없음)
   → Client ──gRPC GrantItem(itemId, qty)──▶ GameServer
                                              ├ 가드: ItemCatalog 존재? 수량 ≤ 상한? 인증?
                                              └ GrantItemAsync → inventory_items (Create/Update)
   → Client: 획득 토스트 + (인벤토리 열면) GetInventory로 수치 갱신
 [SocketServer 미관여]
```

**왜 이게 "통신 최소"인가**: 던전은 매 처치/줍기가 SocketServer→Stream→GameServer 다단계. Main은 **로컬에서 다 끝내고, 줍은 순간 GameServer에 gRPC 1번**. 그래서 당신 말("GameServer랑 다 통신할 필요 없다")이 성립 — 단 *지급 1회*는 영속+치팅방지로 불가피.

**대가(수용됨)**: Main 몬스터/전투/드랍이 클라 권위 → 위조 가능. 싱글 PVE + 서버 가드(catalog·수량상한)로 수용(포트폴리오). 던전(co-op)은 클라신뢰 0 유지.

## 2. 컴포넌트 배치도

```
 [SocketServer = 월드 권위]                                   [GameServer = 인벤토리 권위]
 ┌──────────────────────────────────────────┐               ┌─────────────────────────────────┐
 │ CombatHandler (몬스터 사망 감지, M3)       │               │ LootGrantConsumer (신규)        │
 │   └─ DropTable.Roll(monsterId) ──┐         │               │   └─ IInventoryService.GrantItem │
 │ Room/World: GroundItem[] 보유     │         │  Redis Stream │       (3.1 진입점 재사용)        │
 │   ├─ Spawn (사망 시)               │         │  stream:game: │   멱등: pickupId claim(SET)     │
 │   ├─ TryPickup(userId, gItemId)   │────────▶│  loot:pickup  │─────▶ DB inventory_items         │
 │   │    경쟁 중재(1회), 거리 검증   │         │               └─────────────────────────────────┘
 │   └─ Remove                        │         │
 │ 패킷: S_SpawnGroundItem / C_PickupItem /     │
 │       S_GroundItemRemoved / S_ItemPickedUp   │
 └──────────────────┬───────────────────────────┘
                    │ TCP
                    ▼
 [Client] GroundItemEntity(렌더) · InteractionDetector→IInteractable(줍기 입력) ·
          S_ItemPickedUp→획득 토스트 · 인벤토리 수치는 GetInventory(pull)로 갱신
```

## 3. 시나리오 Flow

**① 몬스터 사망 → 드랍 롤 → 바닥 아이템 스폰**
```
CombatHandler.ApplyAttackToMonsters — dead==true
   │ 알고 있음: attacker.UserId · monster.MonsterId("slime") · monster.Pos · InstanceId
   ▼ DropTable.Roll("slime")  → [(itemId,qty), ...]   (확률 roll, 서버 Random)
   ▼ Room.SpawnGroundItem(itemId, qty, dropPos)  → GroundItem{ GroundId, ItemId, Qty, Pos }
   ▼ room.Broadcast(S_SpawnGroundItem{ GroundId, ItemId, Qty, Pos })
   클라: 바닥에 아이템 렌더(GroundItemEntity) — IInteractable 등록
```

**② 줍기 (플레이어가 "먹을지" 선택)**
```
클라: InteractionDetector가 근처 GroundItem 추적 → 줍기 입력(F/자동) → C_PickupItem{ GroundId } 송신
   ▼
SocketServer: Room.TryPickup(userId, GroundId)
   ├─ 거리/존재 검증 + 경쟁 중재(Interlocked/lock — 1명만 성공)
   ├─ 성공 → GroundItem 제거
   ├─ room.Broadcast(S_GroundItemRemoved{ GroundId })          ← 모든 클라 바닥에서 제거
   ├─ session에 S_ItemPickedUp{ ItemId, Qty } 송신             ← 줍은 본인에게 획득 토스트(push)
   └─ ILootPickupPublisher.Enqueue(ItemPickedUpMessage{ UserId, ItemId, Qty, PickupId })
        ▼ Redis Stream  stream:game:loot:pickup
GameServer: LootGrantConsumer
   ├─ 멱등: PickupId claim(Redis SET, at-most-once)
   └─ IInventoryService.GrantItemAsync(UserId, ItemId, Qty)    ← DB 영속
   클라: 인벤토리 창 열면 GetInventory로 최신 수치 반영(pull)
```

**③ 경쟁(Co-op 동시 줍기)**
```
A·B 동시에 같은 GroundId 픽업 → SocketServer Room.TryPickup이 락/Interlocked로 1명만 성공
   → 1명만 S_ItemPickedUp + 1건만 pickup 메시지 발행 → 중복 지급 없음
```

**④ 멱등 (at-most-once)**
```
- GroundItem 제거는 SocketServer가 1회 보장(경쟁 중재) → pickup 메시지도 1건.
- GameServer는 PickupId(=GroundId 또는 unique) Redis SET claim으로 중복 메시지에도 1회만 지급
  (Exp 보상의 RoomId 멱등과 동일 패턴).
```

## 4. 패킷 / 메시지 (Union ID — 1830~1839 루트)

| 방향 | 패킷/메시지 | 필드 |
|---|---|---|
| S→C | `S_SpawnGroundItem`(1830) | GroundId, ItemId, Qty, PosX/Y/Z |
| S→C | `S_GroundItemRemoved`(1831) | GroundId |
| C→S | `C_PickupItem`(1832) | GroundId |
| S→C | `S_ItemPickedUp`(1833) | ItemId, Qty (획득 토스트) |
| 입장 시 | (S_SpawnGroundItem 로스터로 기존 바닥 아이템 재전송 — 늦은 입장 대응) | |
| SocketServer→GameServer | `ItemPickedUpMessage`(Shared) | UserId, ItemId, Qty, PickupId (던전 경로) |
| Client→GameServer (gRPC) | `GrantItem`(inventory.proto, **신규**) | item_id, qty (Main 경로·서버 가드) |

> ※ 패킷 추가 3단계(클래스·Union 등록·핸들러) + Union ID 범위는 `packets.md`/`networking.md` 준수.
> ※ `GrantItem` gRPC = 공개계약 변경 → 클라 `Generated/` 재생성 필요(명시 승인 후).

## 5. 드랍 테이블 (정적 카탈로그)

- `DropTable`: `monsterId → List<DropEntry(ItemId, Chance, MinQty, MaxQty)>`. 정적 기획데이터 → **코드 카탈로그**(ItemCatalog·MonsterCatalog·spawn-layouts와 동일 컨벤션). 위치 = **SocketServer**(roll이 거기서). itemId는 GameServer ItemCatalog와 문자열로 정렬.
- 예: `slime → [(potion_hp_small, 0.5, 1, 1), (gold_pouch, 0.2, 1, 3)]`.

## 6. 두 경로의 공통점과 차이 (정정 — Main은 4.6 의존 아님)

- **공통 = 지급(GameServer)** 뿐: 던전이든 Main이든 결국 `GrantItemAsync`(Create/Update). GameServer는 *누가 호출하든*(Stream vs gRPC) 씬을 모르고 동일 동작.
- **차이 = 그 앞단(누가 드랍을 판정·중재하나)**:
  - 던전 = **SocketServer 서버권위**(co-op 일관성·치팅방지 필수) → Stream 발행.
  - Main = **Client 로컬**(싱글이라 동기화·경쟁 없음) → Client gRPC 직접.
- **정정**: 이전엔 "Main 몬스터 = 4.6 네트워크 World 의존"이라 봤으나, **싱글 로컬로 결정** → Main 드랍은 **4.6 없이도 가능**(SocketServer 안 거침). 4.6 오픈월드 네트워킹은 *co-op 오픈월드*가 필요해질 때의 별개 주제.

```
던전 처치 → SocketServer roll/바닥/줍기중재 → Stream ─┐
                                                      ├─→ GameServer GrantItemAsync (Create/Update)
Main 처치 → Client roll/바닥/줍기(로컬) → gRPC GrantItem ┘   ← 서버 가드(catalog·수량상한)
```

## 7. 증분 순서 (구현 시 — 아직 안 함)

**A. 던전 경로 (서버권위 — 먼저)**
1. `DropTable`(SocketServer 정적) + roll 단위테스트
2. 패킷 4종 + Union(1830~1833) + `ItemPickedUpMessage`(Shared)
3. SocketServer: Room `GroundItem` 보유·`SpawnGroundItem`·`TryPickup`(경쟁 중재) + CombatHandler 사망분기 발행
4. SocketServer: `C_PickupItem` 핸들러 + `ILootPickupPublisher`/MessageQueue(`stream:game:loot:pickup`)
5. GameServer: `LootGrantConsumer`(ResilientStreamConsumer) → GrantItemAsync, PickupId 멱등
6. 클라: `GroundItemEntity` 렌더 + `IInteractable` 줍기 + `S_ItemPickedUp` 토스트 + 입장 로스터
7. 테스트: SocketServer 단위(roll·경쟁) + GameServer 통합(Stream→DB 지급 멱등) + E2E(사냥→드랍→줍기→인벤토리)

**B. Main 경로 (클라로컬 — 던전 검증 후, 4.6 진행 시)**
8. `GrantItem` gRPC(inventory.proto) + 서버 가드(catalog·수량상한·레이트리밋) + 클라 Generated 재생성
9. 클라: Main 로컬 몬스터 sim + 로컬 드랍/줍기 + `GrantItem` 호출
10. 테스트: GrantItem E2E(인증·수량상한·미존재 itemId 거부) + 가드 단위
> ※ DropTable·GroundItem 렌더·줍기 IInteractable은 A에서 만든 것을 클라 로컬에서 재사용(코드 공유).

## 8. 미결정 / 리스크

- **줍기 방식**: 자동 줍기(밟으면) vs 수동(F). 현재 `IInteractable`+`InteractionDetector`(E키 상호작용) 재사용이 자연스러움 → **수동(F) 권장**, 자동은 후속.
- **Main World 일반화**: Room→World 추상화 범위는 4.6 설계에서 확정. 본 문서는 "그때 일반화하면 드랍 자동 적용"까지만.
- **드랍 외부화**: DropTable JSON/DB 외부화는 후속(지금 코드 카탈로그).
- **획득 토스트 UI**: 클라 토스트 위젯은 7.x UI에서.

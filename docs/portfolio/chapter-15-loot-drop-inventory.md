# 챕터 15 학습 로그 — 루트/드랍 + 인벤토리 (몬스터 ↔ 인벤토리 연결)

> 3.3 + 7.2. 몬스터를 잡으면 월드에 아이템이 떨어지고, 줍기로 서버 권위 인벤토리에 영속된다.
> 던전(Co-op) 경로 = SocketServer 서버권위 → Redis Stream → GameServer 지급. 클라는 표시 + 줍기 의도만.

## 설계 결정과 근거

### roll과 grant를 왜 두 서버로 나눴나 — 책임 경계

몬스터 사망을 감지한 SocketServer가 그 자리에서 DB 인벤토리에 써 넣는 게 가장 짧다. 하지만 SocketServer는 **월드·실시간 전용**(TCP/틱)이고, 인벤토리·아이템정의·영속은 **GameServer 책임**이다. 두 서버는 직접 RPC 금지 — Redis Stream 단방향으로만 잇는다.

```
[SocketServer = 월드 권위]                        [GameServer = 인벤토리 권위]
몬스터 사망 → DropTable.Roll(monsterId)            LootGrantConsumer (Consumer Group)
  → Room.SpawnGroundItem → S_SpawnGroundItem        → PickupId 멱등 claim
  (줍기) C_PickupItem → Room.TryPickup(경쟁 중재)    → IInventoryService.GrantItemAsync
  → S_GroundItemRemoved + S_ItemPickedUp            → inventory_items (Create/Update)
  → ItemPickedUpMessage ──stream:game:loot:pickup──▶
```

"무엇이 떨어지나(roll)·바닥·줍기 중재"는 월드를 소유한 SocketServer가, "영속 지급"은 인벤토리를 소유한 GameServer가. 결합은 메시지 하나(`UserId,ItemId,Qty,PickupId`)뿐.

---

### DropTable이 왜 SocketServer에 있나 — 필요한 데이터만

roll은 *바닥 스폰*과 한 몸이고 *전투 틱 루프 안*에서 일어난다. roll엔 **itemId 문자열 + 확률**만 필요하다 — 이름·아이콘·MaxStack 같은 *정의*는 불필요. 그래서 SocketServer는 `ItemCatalog`(GameServer 소유)를 몰라도 된다. 드리프트(없는 itemId)는 GameServer가 grant 시 `ItemCatalog.Get`으로 검증·실패 처리 → **안전**. 정의 검증의 단일 지점을 인벤토리 소유자에게 둔 것.

---

### 줍기는 "의도"만 — 서버 권위 확정

`C_PickupItem`은 "먹을지" 선택 = **요청일 뿐**, 줍기 확정·바닥 제거·지급은 전부 서버다. 클라가 먼저 바닥을 지우면 Co-op 동시 줍기에서 패배한 쪽이 불일치한다.

```
A·B 동시 픽업 → Room.TryPickup이 lock으로 1명만 성공
  → 1명만 S_ItemPickedUp + 1건만 pickup 메시지 → 중복 지급 없음
GameServer는 PickupId Redis SET claim으로 재전달에도 1회만 지급 (at-most-once)
```

클라 권위 0 — 위치/존재/경쟁/지급 모두 서버. (Exp 보상의 RoomId 멱등과 동일 패턴 재사용)

---

### 클라 렌더는 몬스터 경로를 그대로 미러 — 일관성

바닥 아이템은 "서버가 소유하고 클라는 표시만" 하는 점에서 몬스터와 같다. 그래서 M3 몬스터 렌더 경로를 그대로 미러했다(새 패턴 도입 X):

```
S_SpawnGroundItem → LootPacketHandler → ISocketPacketState.AddGroundItem(이벤트)
  → GroundItemSpawner(IAsyncStartable) → GroundItem.prefab 인스턴스화
  → GroundItemEntity(IInteractable): InteractionDetector가 최근접 선택
  → 로컬 드라이버 E 입력 → Interact → ISocketSession.SendAsync(C_PickupItem)
```

네트워크 레이어(`Game.Network`)는 프리팹/씬을 모른다 — state 경유로 `Game.Gameplay`가 스폰. 레이어 경계 준수.

---

### 인벤토리 표시 정의를 어디에 두나 — 클라 카탈로그

서버 proto는 `itemId + 수량`만 보낸다(권위 데이터). 이름·아이콘·분류 같은 **표시 데이터는 클라가 소유**한다 — `ItemDisplayCatalog`(ScriptableObject, `itemId → {displayName, Sprite, category}`). 서버 `ItemCatalog`(정의)와 itemId 문자열로 정렬되는 클라 미러.

```
서버 GetInventory → (itemId, qty)
  → InventoryModel: catalog.Get(itemId) → InventoryItemModel{Icon, Name, Category}
  → View는 합성된 도메인 모델만 본다 (proto 타입 비노출, MVI)
```

---

### 슬롯 = 컨테이너 / Content 분리 — 타입별 슬롯 대비

슬롯을 "아이템 슬롯" 한 덩어리로 만들면, 나중에 장비/소비/재료별로 다른 디자인을 넣을 때 갈아엎어야 한다. 그래서 **컨테이너와 내용물을 분리**했다:

```
UniversalSlot (컨테이너, 고정 30개 무조건 생성)
   └─ ItemContentsSlot (Content, 아이템 있는 칸만 동적 생성)
탭/정렬/내용 변경 → 컨테이너는 그대로, Content만 EnsureContent/ClearContent로 교체
다른 타입(prefab) 요청 시 기존 Content 파괴 후 재생성 → 타입별 슬롯 디자인 자연 수용
```

빈 칸 = 컨테이너만, 채운 칸 = Content 생성·바인딩. 슬롯 prefab 둘은 Addressable로 로드.

---

## 트러블슈팅 (이번 작업의 실제 디버깅)

### "막타 잡으면 입력이 안 먹힌다" — 진짜 원인은 입력이 아니었다

**증상:** 슬라임(1마리) 잡으면 드랍은 뜨는데 E로 못 줍는다. "던전 클리어 창이 입력을 막는다"고 의심.

**추적:** 코드·프리팹을 끝까지 봤더니 **클리어는 Player 입력맵을 끄지 않는다**(EnterUi 호출자는 로비/인벤토리뿐, timeScale 정지도 없음). DungeonClear 패널엔 풀스크린 레이캐스트 블로커도 없었다. 즉 입력은 살아 있었다.

**실제 원인 2가지:**
1. 슬라임 1마리 = 막타 = **전멸=클리어가 드랍과 동시에** 떠 루팅 타이밍이 없었음 → 클리어 결과 패널을 몇 초 **지연 표시**(상태는 즉시, 표시만 늦춤 — 모델/테스트 무영향)로 막타 드랍 루팅 여유 확보.
2. E 줍기가 안 된 진짜 이유는 아래.

교훈: "입력이 안 먹힌다"는 증상을 입력 차단으로 단정하지 말 것. 코드·씬을 양쪽으로 검증해 가설을 깼다.

### E 줍기가 안 됨 — 드랍 오브가 너무 낮았다

플레이어 `InteractionDetector`는 **가슴 높이(≈y1)** 에 감지 구체를 쏜다. 드랍 오브는 몬스터 사망 위치(y≈0, 바닥)에 스폰돼 구체와 안 겹쳤다(수직 갭 ≈ 반지름 합). 레이어(7)·트리거·`queriesHitTriggers=1`는 모두 정상이었다. → `GroundItemEntity`가 스폰 시 **+0.7 띄워** 가슴 높이로 올려 해결.

### 인벤토리가 안 열림 — 스코프에 컨트롤러 누락

**증상:** I키/HUD 버튼을 누르면 `InGameModel.Accept(ToggleInventory)`까지 가는데 창이 안 뜬다.

**판별:** 인스턴스 해시 로그를 양쪽에 박아 보니 `InventoryViewController.Initialize`가 **아예 호출되지 않음**. `InventoryViewController`는 `DungeonLifetimeScope`에만 등록돼 있고 **`MainLifetimeScope`엔 누락** — 로비(Main) 씬엔 토글 신호를 받을 구독자가 없었다. → Main 스코프에도 인벤토리 스택 등록.

교훈: "신호는 보내는데 반응이 없다" = 송신/수신 인스턴스 동일성 + 구독자 생존을 먼저 의심. 해시 로그로 즉시 좁혔다.

### 아이콘이 안 뜸 — 경로 불일치 + itemId 미정렬

두 겹이었다: ① `ItemDisplayCatalog` 에셋은 `Resources/ItemDisplayCatalog`인데 로드 코드는 `"Inventory/ItemDisplayCatalog"` → Main 폴백이 null(빈 카탈로그). ② entry의 itemId가 placeholder(`1/2/3`)라 서버 itemId(`potion_hp_small` 등)와 매칭 0. → 로드 경로 정정 + entry를 서버 itemId에 정렬.

### I키 미배선 — 생성 래퍼가 stale

`.inputactions`엔 `Inventory` 액션(`<Keyboard>/i`)을 넣었지만 **생성된 `PlayerInputActions` C# 래퍼에 미반영**(`.Player.Inventory` 없음) + 던전엔 InputRouter 미등록. 정석(래퍼 재생성 → InputRouter route)은 Unity 작업이 필요해, 임시로 `GameHud.Update`가 `Keyboard.current.iKey`를 폴링해 버튼과 동일 funnel로 합류시켰다(후속 이관 대상).

---

## 아직 미완성인 것 (TODO)

```
Main 경로(싱글 PVE): GrantItem gRPC + 서버 가드(catalog·수량상한) — ✅ 완료 → 챕터 16
정식 획득 토스트 위젯 (현재 OnItemPickedUp 로그)
아이템 타입별 ItemContentsSlot 디자인 (UniversalSlot은 교체 대비 완료)
I키 정석 배선: PlayerInputActions 래퍼 재생성 → InputRouter route
인벤토리 탭(Material/Quest/Etc) 토글 배선
```

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
| --- | --- |
| roll/grant 분리 | 월드(SocketServer)=DropTable·바닥·줍기중재 / 인벤토리(GameServer)=영속 지급, Redis Stream |
| 줍기=의도만 | C_PickupItem은 요청, 제거·지급은 서버 권위(경쟁 중재 1회) |
| PickupId 멱등 | Redis SET claim → at-least-once 전달을 at-most-once 효과로 |
| 정의 검증 단일점 | SocketServer는 itemId만, GameServer가 grant 시 ItemCatalog 검증 |
| 클라 렌더 미러 | 몬스터 경로(PacketState→Spawner→Entity) 재사용, 네트워크는 씬 비참조 |
| ItemDisplayCatalog | 표시 정의(itemId→Sprite/이름/분류)는 클라 소유, proto는 itemId+qty만 |
| 컨테이너/Content 분리 | UniversalSlot(고정 30) + ItemContentsSlot(동적), 타입별 슬롯 대비 |
| 증상≠원인 | "입력 안 먹힘"의 실제 원인 = 막타 동시 클리어 + 드랍 높이(가설 검증으로 규명) |
| 스코프 누락 디버깅 | 인스턴스 해시 로그로 "구독자 미생성(Main 스코프 누락)" 특정 |

# 15. 루트/드랍 — 하나의 아이템을 두 서버가 나눠 갖는다

> **한 줄** — "몬스터가 떨어뜨린 것"과 "가방에 든 것"은 **소유자가 다르다**. 월드(SocketServer)는 무엇이 떨어지고 누가 먼저 주웠는지를, 인벤토리(GameServer)는 그것을 영속하는 일을 맡는다. 둘을 잇는 건 메시지 하나뿐이다.
>
> **범위** roll/grant 분리 · 줍기 경쟁 중재 · 멱등 지급 · 표시 데이터 소유권 · 슬롯 구조
> **하이라이트** "입력이 안 먹힌다"는 증상의 **진짜 원인 찾기** (7절)

---

## 1. 책임을 어디서 자를 것인가

몬스터 사망을 감지한 SocketServer가 그 자리에서 DB 인벤토리에 쓰는 게 가장 짧다. 하지만 그러면 SocketServer가 **아이템 정의·인벤토리 스키마·영속**을 전부 알아야 한다.

```
[SocketServer = 월드 권위]                     [GameServer = 인벤토리 권위]
몬스터 사망 → DropTable.Roll(monsterId)         LootGrantConsumer (Consumer Group)
  → 바닥에 스폰 → S_SpawnGroundItem               → PickupId 멱등 claim
  (줍기) C_PickupItem → Room.TryPickup(중재)      → InventoryService.GrantItemAsync
  → S_GroundItemRemoved + S_ItemPickedUp          → inventory_items 영속
  → ItemPickedUpMessage ──stream:game:loot:pickup──▶
```

자른 기준은 **"이 데이터를 누가 소유하는가"** 였다. 바닥에 놓인 아이템은 월드의 일부이고(위치가 있고 방과 함께 사라진다), 가방 속 아이템은 계정의 일부다(영속되고 방과 무관하다). **수명이 다른 두 가지를 한 서버가 갖고 있을 이유가 없다.**

결합면은 메시지 하나다 — `{UserId, ItemId, Qty, PickupId}`.

## 2. SocketServer는 아이템이 무엇인지 모른다

`DropTable`은 SocketServer에 있다. roll이 **바닥 스폰과 한 몸**이고 **전투 틱 루프 안**에서 일어나기 때문이다.

여기서 필요한 건 **itemId 문자열과 확률뿐**이다 — 이름·아이콘·최대 스택 같은 *정의*는 필요 없다. 그래서 SocketServer는 `ItemCatalog`(GameServer 소유)를 참조하지 않는다.

> **그럼 없는 itemId가 흘러가면?** — GameServer가 지급 시점에 `ItemCatalog.Get`으로 검증하고 실패 처리한다. **정의 검증의 단일 지점을 정의의 소유자에게 둔** 것이다. 양쪽에서 검증하면 두 카탈로그가 드리프트할 때 어느 쪽이 옳은지 알 수 없어진다.

## 3. 줍기는 "의도"일 뿐이다

`C_PickupItem`은 **요청**이다. 바닥에서 지우는 것도, 지급하는 것도 서버가 한다.

```
A·B 동시에 E 입력
  → Room.TryPickup 이 lock 으로 1명만 성공
  → 승자에게만 S_ItemPickedUp, pickup 메시지도 1건
  → GameServer 는 PickupId 를 claim-first 로 잠가 재전달에도 1회만 지급
```

클라가 먼저 바닥을 지우게 두면 **패배한 쪽의 화면에서만 아이템이 사라진다.** Co-op에서 이런 불일치는 곧바로 눈에 띈다.

멱등 방식은 [14](./chapter-14-dungeon-clear-loop.md)의 Exp 보상과 **같은 패턴을 재사용**했다 — 키만 `RoomId`에서 `PickupId`로 바뀐다.

```csharp
// LootGrantConsumer.cs:50 — GrantItemAsync 는 += 라 비멱등이므로 먼저 잠근다
bool claimed = await _redis.SetAddAsync(RedisKeys.LootPickupProcessed(), message.PickupId);
if (!claimed) return;
```

## 4. 클라 렌더는 새 패턴을 만들지 않았다

바닥 아이템은 "서버가 소유하고 클라는 표시만 한다"는 점에서 **몬스터와 성질이 같다**([13](./chapter-13-monster-server-authority.md)). 그래서 그 경로를 그대로 미러했다.

```
S_SpawnGroundItem → LootPacketHandler → ISocketPacketState.AddGroundItem (이벤트)
  → GroundItemSpawner → GroundItem.prefab 인스턴스화
  → GroundItemEntity(IInteractable) → InteractionDetector 가 최근접 선택
  → 로컬 드라이버 E 입력 → Interact → C_PickupItem 송신
```

**네트워크 레이어는 프리팹도 씬도 모른다.** 패킷은 state를 갱신하고, 스폰은 `Game.Gameplay`가 한다. 레이어 경계를 지키면서 새 개념을 하나도 도입하지 않았다.

## 5. 표시 데이터는 클라가 소유한다

서버 proto는 **`itemId + 수량`만** 보낸다. 이름·아이콘·분류는 클라의 `ItemDisplayCatalog`(ScriptableObject)가 갖는다.

```
서버 GetInventory → (itemId, qty)
  → InventoryModel: catalog.Get(itemId) → InventoryItemModel { Icon, Name, Category }
  → View 는 합성된 도메인 모델만 본다 (proto 타입 비노출)
```

**아이콘을 서버가 알아야 할 이유가 없다.** 서버가 소유해야 하는 건 "몇 개 있는가"(권위)이고, "어떻게 보이는가"는 클라의 문제다. 리스킨이나 현지화가 서버 배포를 요구하지 않게 된다.

## 6. 슬롯 = 컨테이너 / 내용물 분리

슬롯을 한 덩어리로 만들면 나중에 장비·소비·재료별로 다른 디자인을 넣을 때 갈아엎어야 한다.

```
UniversalSlot      (컨테이너, 30개 고정 생성)
   └ ItemContentsSlot  (Content, 아이템이 있는 칸만 동적 생성)

탭 전환·정렬·내용 변경 → 컨테이너는 그대로, Content 만 교체
다른 타입 요청 시 → 기존 Content 파괴 후 재생성
```

빈 칸은 컨테이너만, 채운 칸은 Content까지. **"칸이 있다"와 "무엇이 들었다"를 분리**하면 타입별 디자인이 자연스럽게 수용된다.

## 7. "막타 잡으면 입력이 안 먹힌다" — 가설을 깨는 과정

이 챕터에서 가장 오래 걸린 건 기능이 아니라 **원인을 잘못 짚은 시간**이었다.

### 첫 가설: 던전 클리어 창이 입력을 막는다

그럴듯했다. 슬라임 1마리를 잡으면 드랍은 뜨는데 E로 줍히지 않았고, 마침 클리어 창이 떠 있었다.

**검증해서 깼다** — 코드와 씬을 양쪽으로 확인했다.

```
EnterUi 호출자 = 로비·인벤토리뿐        → 클리어는 Player 입력맵을 끄지 않는다
timeScale 정지 없음                      → 시간도 멈추지 않았다
DungeonClear 패널에 레이캐스트 블로커 없음 → 클릭도 안 막는다
⇒ 입력은 살아 있었다. 가설 기각.
```

### 진짜 원인은 두 개였다

**① 루팅할 시간이 없었다** — 슬라임이 1마리라 **막타 = 전멸 = 클리어**가 드랍과 동시에 일어난다. 결과 패널이 즉시 뜨면서 "주울 틈"이 사라진 것이지, 입력이 막힌 게 아니었다.
→ **상태는 즉시 확정하고 표시만 몇 초 지연**시켰다. 모델과 테스트는 건드리지 않았다.

**② 감지 구체와 아이템이 안 겹쳤다**

```
InteractionDetector : 가슴 높이(y≈1)에서 구체 감지
GroundItem          : 몬스터 사망 위치(y≈0, 바닥)에 스폰
                      → 수직 간격이 반지름 합보다 커서 영원히 안 걸린다
```

레이어(7)·트리거·`queriesHitTriggers` **전부 정상**이었다. 설정이 맞는데 안 되는 상황이라 더 헤맸다. → 스폰 시 **+0.7 띄워** 해결.

> **교훈** — "입력이 안 먹힌다"는 **증상의 서술이지 원인의 서술이 아니다.** 실제로 입력은 정상이었고 원인은 타이밍과 기하였다. 가설을 세웠으면 **그 가설이 틀렸음을 증명할 수 있는 지점**(EnterUi 호출자, timeScale, 블로커)을 먼저 확인해야 시간을 아낀다.

### 곁가지로 나온 진단 둘

**인벤토리가 안 열림 — 신호는 가는데 받는 사람이 없었다**
`Accept(ToggleInventory)`까지는 도달하는데 창이 안 떴다. 인스턴스 해시를 양쪽에 찍어 보니 `InventoryViewController.Initialize`가 **호출조차 되지 않았다** — 컨트롤러가 `DungeonLifetimeScope`에만 등록돼 있고 `MainLifetimeScope`엔 빠져 있었다.
→ **"신호는 보내는데 반응이 없다"면 송·수신 인스턴스 동일성과 구독자 생존을 먼저 의심한다.** 해시 로그 두 줄이면 즉시 좁혀진다.

**아이콘이 안 뜸 — 두 겹이었다**
① 카탈로그 에셋 경로와 로드 경로가 달라서 폴백이 빈 카탈로그였고, ② 그걸 고쳐도 entry의 itemId가 placeholder(`1/2/3`)라 서버 itemId(`potion_hp_small`)와 **하나도 매칭되지 않았다**. 두 번째 층은 첫 번째를 고쳐야 보였다.
→ **하나 고쳤는데 여전히 안 되면, 고친 게 틀린 게 아니라 층이 하나 더 있는 것일 수 있다.** ([27](./chapter-27-silent-failure.md)에서 이 현상이 훨씬 크게 반복된다.)

## 8. 그 이후

| 당시 TODO | 결말 |
|---|---|
| Main(싱글) 경로 | ✅ 클라 시뮬·렌더 + 서버 검증(B-lite)로 완성 → [16](./chapter-16-main-loot-path.md) |
| 획득 토스트 위젯 | ✅ `GameHud`에 구현(획득감용 금색 2초) |
| 타입별 슬롯 디자인 | ✅ 구조 완성 — `GUI/Common/Slots/`에 `UniversalSlot` + `Contents/ItemContentsSlot` |
| 표시 카탈로그 로딩 | ✅ Resources → **Addressables**로 이관([20](./chapter-20-content-pipeline-addressables.md)) |
| **I키 정석 배선** | ❌ **미완** — 아래 |

### ⚠️ 남은 것 — 임시방편이 그대로 남았다

당시 `.inputactions`에 `Inventory` 액션을 넣었지만 **생성된 C# 래퍼에 반영되지 않아** 임시로 `GameHud`가 키를 직접 폴링하게 했고, "후속 이관 대상"이라고 적어 뒀다.

지금 상태:

```
PlayerInputActions.cs:1290   m_Player_Inventory = ... FindAction("Inventory")   ← 래퍼는 재생성됨 ✅
GameInputAction.ToggleInventory                                                  ← enum 값은 있음 ✅
InputRouter → ToggleInventory 라우팅                                             ← 없음 ❌
GameHud.cs:255              Keyboard.current 직접 폴링                           ← 임시방편 잔존 ❌
```

**막고 있던 전제조건(래퍼 재생성)은 해결됐는데 마지막 배선만 안 됐다.** 주석 두 곳(`GameInputAction.cs:14`, `InventoryViewController.cs:19`)이 아직 "연결 예정"이라고 적혀 있다. `DialogueView.cs:55`에도 같은 직접 폴링이 있다.

동작에는 문제가 없지만, [입력 규칙](../wiki/unity-input-system.md)이 세운 "입력은 한 경로로 합류한다"는 원칙에서 벗어난 지점이다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 소유자가 다르면 서버도 다르다 | 장비·상점·퀘스트가 전부 같은 경계로 확장([17](./chapter-17-equipment-system.md)·[18](./chapter-18-wallet-shop.md)·[19](./chapter-19-quest-system.md)) |
| 정의 검증은 소유자 한 곳에서 | 카탈로그 드리프트를 한 지점에서 흡수 |
| 표시 데이터는 클라 소유 | 아이템·이펙트·아이콘 카탈로그의 공통 원칙 |
| 증상 ≠ 원인 | 조용한 실패 사냥의 전초([27](./chapter-27-silent-failure.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-15-loot-drop-inventory.md](../learning-log/chapter-15-loot-drop-inventory.md)

# 챕터 18 학습 로그 — 재화(Wallet) + 상점(Shop)

> 3.4 + 3.5 + 7.6. 골드를 **통화**로 승격(인벤토리 아이템 아님) → 지갑 잔액 도메인 + 상점(구매/판매)으로 소비. 서버 권위(가격·증감) + 클라 MVI/GUI.
> 핵심: **골드는 통화다.** 인벤토리에 "골드 주머니"를 쌓지 않고, 드랍·킬 보상을 영속 경계 한 곳에서 지갑으로 **라우팅**한다. 상점은 자기 영속 없이 지갑·인벤토리를 **조합**한다.

---

## 설계 결정과 근거

### 골드를 인벤토리 아이템에서 통화로 — 라우팅 한 곳

루트 시스템(3.3)을 먼저 만들 때 골드는 `gold_pouch`라는 **스택형 인벤토리 아이템**이었다. 통화 도메인이 없었으니 임시방편이었다. 3.4에서 지갑이 생기자 갈림길이 왔다: 골드를 통화로 전환할 것인가, 아이템으로 둘 것인가. ARPG에서 골드는 통화다 — 인벤토리 칸을 먹지 않고, 단일 잔액이며, 상점이 증감한다. 그래서 **통화로 전환**했다.

문제는 "어디서 바꾸냐"였다. 골드는 이미 generic 루트 파이프라인을 탄다:

```
[던전]  SocketServer: drop roll → 바닥 아이템 → 줍기 → ItemPickedUpMessage(itemId)
                                                              │
[Main]  ClaimKill → DropTableRoll(itemId, qty) ───────────────┤
                                                              ▼
                                          GameServer 영속 경계 (지급 chokepoint)
                                          ┌──────────────────────────────────┐
                                          │ if itemId == Currencies.Gold      │
                                          │   → IWalletService.AddAsync       │  ← 통화
                                          │ else                              │
                                          │   → IInventoryService.GrantItem   │  ← 아이템
                                          └──────────────────────────────────┘
```

**SocketServer는 무수정.** 골드는 SocketServer에게 그냥 itemId 문자열("gold")이라 바닥 스폰·줍기·메시지가 전부 그대로 동작한다. 통화/아이템 분기는 **GameServer 영속 경계 2곳**(`LootGrantConsumer`·`MainSpawnClaimService.ClaimKill`)에서만 일어난다. 블래스트 반경을 한 레이어로 가뒀다.

`gold_pouch`(아이템) → `gold`(통화) 키 정리 + `Currencies.Gold` 상수 + `ItemCatalog`에서 gold 제거(= 인벤토리로 가면 "unknown item"으로 안전하게 막힘). drop-tables.json은 양 서버 임베디드라 키를 바꾸면 둘 다 리빌드.

---

### 지갑 = 인벤토리의 단일값 미러 — 새 패턴을 만들지 않는다

지갑은 "유저당 정수 잔액 하나"다. 인벤토리(유저당 itemId→수량 Hash)의 **단일값 버전**이라, 검증된 패턴을 그대로 미러했다.

```
InventoryItem (UserId,ItemId)→Qty   │   UserWallet (UserId)→Balance(long)
Redis Hash (field=itemId)            │   Redis String (정수)
AddQuantity / RemoveQuantity         │   AddBalance / TrySpendBalance
Cache-Aside + Delete                 │   Cache-Aside + Delete  (동일)
```

- **Redis String** — 단일 스칼라라 Hash가 필요 없다. 그리고 인벤 Hash와 달리 **잔액 0도 캐시**한다(String "0"은 MISS와 구분 가능 → 폴백 트래픽 절감).
- 차감은 도메인 `UserWallet.TrySpend`가 "잔액 ≥ 금액"을 원자 가드(부족하면 false=미차감) — 인벤 `Remove`와 동형.
- `user_wallets`(UserId PK). 마이그레이션은 EF로 생성 후 `Up`을 멱등 raw SQL(`CREATE TABLE IF NOT EXISTS`)로 교체 — 인벤/장비와 동일 컨벤션.
- 별도 Installer를 안 만들고 `InventoryInstaller`(경제 클러스터)에 합류 — 인벤/장비/지갑/상점이 같은 경제 도메인이라 응집(YAGNI: Installer 난립 방지).

---

### gRPC는 조회 전용 — 증감 RPC가 없는 게 핵심

`wallet.proto`·`shop.proto`에서 **클라가 골드를 증감하는 RPC를 두지 않았다.**

```
wallet.proto : GetWallet 만           (잔액 조회)
shop.proto   : GetShop / Buy / Sell   (구매·판매는 itemId+qty 만 보냄)
```

가격·증감은 전부 서버가 결정한다. 구매 요청은 `(itemId, qty)`뿐 — 클라가 "이거 1골드에 살게"라고 가격을 위조할 수 없다. 골드 증감의 유일한 경로는 **서버 내부**(루트/킬 보상, 상점 거래)다. 이는 Main 무한파밍 핵을 막을 때 세운 교리(클라가 보상을 임의 지정 못 함, authority-model §4b)와 같은 원칙이다.

---

### 상점 = 조합, 영속 없음 — 구매/판매 원자성

상점은 자기 테이블/Repository가 없다. 가격은 정적 카탈로그(`ShopCatalog`, 코드)고, 실제 상태는 지갑·인벤토리가 소유한다. 상점은 둘을 **조합**할 뿐이다.

핵심은 **차감을 먼저** 한다는 것 — 복제(dupe)를 막는다.

```
구매 Buy(itemId, qty)                    판매 Sell(itemId, qty)
 ① 가격 = ShopCatalog.Get(itemId)         ① 판매가 = ShopCatalog.Get(itemId)
 ② Wallet.TrySpend(price·qty)             ② Inventory.Consume(itemId, qty)
      부족 → 거부(변화 0)                      미보유 → 거부(변화 0)
 ③ Inventory.Grant(itemId, qty)           ③ Wallet.Add(sellPrice·qty)
      실패 → Wallet.Add 환불(보상)
```

- **구매: 차감 먼저** → "골드 안 내고 아이템 받기" 불가. 지급이 (설정 오류 등으로) 실패하면 차감분을 **환불**(보상 트랜잭션). 서버 단일 프로세스라 분산 트랜잭션은 과함(YAGNI) — 환불 한 줄로 충분.
- **판매: 차감 먼저** → "아이템 두고 골드 받기" 불가.
- 두 도메인에 걸친 작업이라 진짜 트랜잭션은 아니지만, **차감 선행 + 실패 시 보상**이 단일 프로세스에서의 실용적 안전장치다.

스탯 미리보기(진열의 "공격력 +5")는 `EquipmentCatalog`에서 파생 — 중복 저작 없이 gRPC 레이어가 비0 스탯만 뽑아 채운다. 공개 표시 정보라 proto에 실어도 권위 전투 스탯과 무관(치팅 아님).

---

### 클라 MVI — 인벤토리 스택을 그대로 미러

지갑·상점 모두 클라는 인벤토리 MVI 스택을 미러했다. 레이어 규칙(GUI→Presentation→System→Network, View는 자기 Model만)을 그대로 따른다.

```
Shop View (GUI, ShopModel만 주입)
   ▲ State 구독 / Intent(탭·선택·수량·구매) 발행
ShopModel (Presentation) ── IShopService + IWalletService(골드) 주입
   ▼
Game.System  IShopService (proto 은닉, GetShop+Buy)
   ▼
Game.Network IShopGrpcService (ClientCodegen 자동 생성) → shop.proto
```

- **지갑 표시**: 인벤토리 창에 골드 잔액 연동 — `InventoryModel`이 `IWalletService`를 선택주입(null-safe)해 `RefreshAsync`에서 함께 로드, `_goldText`에 바인딩. 정식 지갑 위젯(7.x) 전까지 인벤토리가 표시 책임.
- **카테고리 enum 3겹**(proto / System / Presentation)은 레이어 격리상 불가피 — GUI는 Presentation만 보므로 System 타입을 노출하지 않으려면 각 레이어가 자기 enum을 갖고 경계에서 매핑한다.

---

### 동적 슬롯 — 별도 Addressable prefab의 의도

상점 리스트 행(`Shop_Item`)과 스탯 행(`Status_Slot`)을 **Addressable prefab으로 동적 생성**한다(고정 배치 X). 인벤토리의 `LoadSlotPrefabsAsync` 패턴 재사용: 프리팹 로드 → `GetComponent` → 사전배치 자식 제거 → `Instantiate` 풀링 → `OnDestroy` Release. 항목 수가 바뀌어도 슬롯을 수동 배치할 필요가 없다.

---

### 구매 결과 토스트 — 결과를 모델이 싣는다

"성공/실패에 따라 색을 다르게" 하려면 View가 결과를 알아야 한다. 문자열만으론 부족해, `OnToast`를 `Subject<string>`→`Subject<ShopToastMessage>`(메시지 + `Success` 플래그)로 바꿨다. View는 성공=초록/실패=빨강으로 창 내 토스트를 띄우고 자동으로 숨긴다(전역 토스트 위젯은 7.x — 상점 자체 피드백으로 선행).

---

### 열기 트리거 — 충돌하는 키는 뺀다

상점은 처음에 **S키 + HUD 버튼**으로 열게 했는데, S는 WASD 후진 이동키와 겹쳐 **후진할 때마다 상점이 토글**되는 버그였다. 키를 빼고 **HUD 상점버튼 단독**으로 정리. 열린 동안은 `UiInputCaptureBehaviour`(인벤/장비와 동일 refcount)로 이동 입력을 점유해 캐릭터가 안 움직인다. Main 전용 등록(던전엔 상점 없음).

---

### 골드 밸런스 — 통화는 항상 나온다

골드 드랍을 확률 0.2/1~3에서 **항상 드랍(1.0)/10~30**으로 올렸다. 통화는 "가끔 나오는 레어 아이템"이 아니라 매 처치의 기본 수급이어야 상점(가격 50~300)이 굴러간다. 매 슬라임 처치 = **골드(보장) + 포션(보장) + 장비(확률)** = "골드 + 추가 보상".

---

## 코드 위치

| 영역 | 파일 |
|------|------|
| 통화 상수 | `GameServer.Domain/Currencies.cs` |
| 지갑 도메인 | `GameServer.Domain/Entities/Wallet/UserWallet.cs` · `Application/Domains/Wallet/` · `Infrastructure/Domains/Wallet/WalletRepository.cs` |
| 골드 라우팅 | `Infrastructure/Common/Consumer/LootGrantConsumer.cs` · `Infrastructure/Domains/Inventory/MainSpawnClaimService.cs` |
| 상점 도메인 | `GameServer.Domain/Entities/Shop/` · `Application/Domains/Shop/` |
| gRPC | `Shared.Contracts/Protos/{wallet,shop}.proto` · `API/Services/{Wallet,Shop}GrpcService.cs` |
| 클라 지갑 | `Client/.../System/Wallet/` · `Presentation/Inventory/InventoryModel.cs`(골드 합류) |
| 클라 상점 | `Client/.../System/Shop/` · `Presentation/Shop/` · `GUI/Shop/` · `GUI/OutGame/ShopViewController.cs` |
| 드랍 밸런스 | `Shared.Infrastructure/Loot/drop-tables.json` · 클라 `DropTableDefinition.asset` |

## 검증

- 서버: GameServer **340/340**(통합 포함 — Wallet 23 · Shop 11) · SocketServer 103 · Shared 34.
- 클라: ShopModelTests 7/7 · InventoryModelTests 6/6 · InputRouter/InGameExpRelay 12/12.
- E2E(Docker): 던전 줍기 1/1 · MainLoot 4/4. 양 서버 리빌드 후 컨테이너 healthy(Wallet·Shop gRPC 호스트 부팅).

## 한 줄 회고

골드를 통화로 바꾸는 일의 90%는 "어디서 분기하냐"였다. 파이프라인 끝(영속 경계)에서 한 번만 분기하니 SocketServer·드랍·줍기가 전부 무수정으로 남았다. 상점도 마찬가지 — 자기 상태를 안 가지니 "차감 먼저 + 실패 시 환불"만 지키면 두 도메인 조합이 안전했다.

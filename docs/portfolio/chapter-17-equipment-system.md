# 17. 장비 시스템 — 합류점이 하나면 기능은 저절로 퍼진다

> **한 줄** — 장비가 전투에 반영되게 하려면 데미지 계산·HP·스탯창을 다 고쳐야 할 것 같았지만, **모든 스탯이 이미 한 곳으로 수렴하고 있었다**. 거기에 한 줄을 더하니 SocketServer·전투·스탯창이 전부 무수정으로 장비를 반영했다.
>
> **범위** 정의/소유/착용 3분리 · 공통 enum · 스탯 합류점 · 표시 정책 · MVI 크로스 갱신
> **하이라이트** 생성자에 의존성 하나를 더했더니 **DI 호스트 4곳이 조용히 깨진** 사건 (8절)

---

## 1. 셋으로 나눈 이유 — 변하는 축이 다르다

장비를 한 테이블에 다 넣고 싶어지지만, 세 가지는 **바뀌는 이유가 각각 다르다.**

```
정의  ItemCatalog / EquipmentCatalog (코드)   이름·등급·슬롯·스탯   ← 기획이 바꾼다
소유  inventory_items (UserId,ItemId)→Qty     "가지고 있다"        ← 플레이가 바꾼다
착용  user_equipments (UserId,Slot)→ItemId    "어디에 꼈다"        ← 플레이가 바꾼다(다른 빈도)
```

- **정의를 DB에 두지 않았다.** 정적 기획 데이터는 카탈로그(코드/bake)로 — `MonsterCatalog`·`drop-tables.json`과 같은 컨벤션이다([16](./chapter-16-main-loot-path.md) 6절). `items` 테이블을 만들면 배포마다 데이터 마이그레이션이 따라붙는다.
- **착용은 소유의 부분집합 상태다.** 장착해도 소유는 유지된다. 둘을 한 테이블로 합치면 *"장착하면 인벤토리에서 빠진다"* 를 **DB가 강제**하게 되고, 그건 표시 정책이지 데이터 사실이 아니다(4절).

장비는 `Stackable:false, MaxStack:1`이고 **개별 인스턴스(강화 수치·랜덤 옵션)는 만들지 않았다.** 지금 필요 없다(YAGNI). 대신 부수 효과가 하나 생긴다 — 한 종류를 1개만 가지므로 **착용 매칭이 `itemId` 비교만으로 정확**해진다. 강화가 생기면 그때 `equipment_instances`로 승격한다.

## 2. 같은 enum이 세 군데 있으면 반드시 어긋난다

처음엔 서버를 Weapon/Armor **2슬롯**으로 만들었는데, GUI 프리팹은 머리·신발·반지까지 **8슬롯**이었다.

슬롯 enum이 서버 도메인 / proto / 클라 GUI에 따로 있으면 값이 늘 때마다 세 곳을 고치고 매핑 코드를 유지해야 한다. **드리프트는 시간 문제**다.

```
Shared.Gameplay.Equipment.EquipmentType     ← 단일 정의 (8슬롯 + None=0)
        │
        │ 클라는 Plugins/Shared.Gameplay.dll 로 같은 타입을 참조
   ┌────┴────────────────┐
[서버 도메인]        [클라 GUI/Presentation/System]
        │
proto enum EquipmentType (9값)  ← 정수값을 도메인과 1:1로 맞춤
        → 경계 매핑이 (ProtoType)(int)x 캐스팅 한 줄
```

proto는 C# enum을 참조할 수 없으니 별도 정의가 불가피하다. 대신 **정수값을 1:1로 맞춰** `switch` 매핑을 없앴다. 매핑 코드가 없으면 매핑 버그도 없다.

카탈로그는 아직 Weapon/Armor만 채워져 있고 나머지 6슬롯은 비어 있다 — **enum 값은 있는데 아이템이 없을 뿐**이다. 슬롯 UI와 미래 확장 자리를 미리 열어 둔 것이지 죽은 코드가 아니다.

> **대가** — 이미 만든 2슬롯 proto와 도메인을 8슬롯으로 재작업해야 했다. `user_equipments`가 비어 있는 개발 단계라 마이그레이션 리스크가 없어서 지금 하는 게 가장 쌌다.

## 3. 합류점은 이미 있었다

장비 스탯을 전투에 반영하려면 여러 곳을 고쳐야 할 것 같았다. 그런데 **모든 스탯이 이미 한 곳으로 수렴**하고 있었다.

```csharp
// ProgressionService.cs:43-44 — 여기에 한 줄
var s  = LevelTable.StatsAt(progression.Level);       // base
var eq = await equipment.GetEquippedStatsAsync(userId, ct);   // ← 추가한 것
```

```
GetStatsAsync (단일 합산 권위)
   ├─▶ GameStartRequestedMessage → SocketServer InitPlayerState → 전투 데미지   ← 무수정
   └─▶ gRPC GetProgression → 클라 스탯창                                        ← 무수정
```

**합산 로직 자체는 장비 도메인이 소유한다.** `ProgressionService`는 결과를 더하기만 하고 카탈로그도 슬롯도 모른다. 그래서 나중에 버프·세트효과가 생겨도 **같은 자리에 합류**하면 된다.

> 이 구조가 가능했던 건 [authority-model §4c](../wiki/authority-model.md)의 *"SocketServer는 합산 결과만 받는다"* 원칙 덕이다. SocketServer가 스탯을 스스로 계산했다면 장비 반영을 위해 SocketServer도 장비를 알아야 했을 것이다.
>
> 같은 판단이 반복된다 — [13](./chapter-13-monster-server-authority.md) 4절의 "검증은 합류 지점에", [19](./chapter-19-quest-system.md)의 "훅은 funnel에".

## 4. "장착하면 인벤토리에서 사라진다"는 표시 정책이다

요구는 명확했다 — 장착하면 인벤토리에서 사라지고 장비창에 나타난다. 그런데 서버는 장착해도 소유를 유지한다. 그럼 인벤토리에 그대로 보일 텐데?

```
InventoryModel.Refresh:
    items    = GetInventory()      (소유 전체)
    equipped = GetEquipped()       (착용 itemId 집합)
    표시      = items - equipped
```

**DB에서 빼는 게 아니라 클라 표시에서 거른다.** "이동"은 시각적 착시일 뿐이고 소유는 서버 한 곳에 그대로 있다.

이렇게 두면 나중에 정책을 바꾸기 쉽다 — "착용 중인 것도 인벤토리에 회색으로 표시"는 클라 한 줄이지만, DB에서 뺐다면 데이터 구조를 되돌려야 한다. **되돌리기 쉬운 쪽에 정책을 둔다.**

## 5. 두 창이 동시에 갱신돼야 하는데, View끼리 엮으면 안 된다

장비창에서 해제하면 인벤토리도 갱신돼야 한다. 하지만 MVI 규칙상 **View는 자기 Model만 안다** — Inventory View가 EquipmentModel을 부를 수 없다.

```
IEquipmentService.OnChanged           (장착·해제 성공 시 1회 발행)
   ├─▶ EquipmentModel  구독 → Refresh   (장비창)
   └─▶ InventoryModel  구독 → Refresh   (착용 필터 재적용)
```

System 레이어 서비스가 변경을 통지하고, **두 Presentation Model이 각자 구독**한다. View끼리 직접 엮이지 않으면서 한 이벤트로 두 창이 일관되게 갱신된다.

**신호를 어느 레이어에서 발행하느냐가 결합을 결정한다** — 서비스에서 쏘면 구독자가 몇이든 서로 모른다. 이 판단은 나중에 HUD 창들이 늘어날 때 그대로 재사용됐다([22](./chapter-22-hud-windows-mvi.md)).

## 6. 두 번째 소비자가 생긴 시점에 추출한다

슬롯 클릭 → 액션 패널(Addressable 로드 + 백드롭 + 위치 계산 + 재진입 가드 + 닫기)은 원래 인벤토리 안에 있었다. 장비창도 똑같이 필요해지자 공용으로 뺐다.

```
ItemActionPanelController (GUI/Common)
   OpenAsync(canvas, slotRect, configure)   ← 로드·배치·가드·닫기
호출처는 버튼 구성만 주입:
   Inventory : Bind(id, onUse, onEquip, null,       canUse, canEquip, false)
   Equipment : Bind(id, null,  null,    onUnequip,  false,  false,    true)
```

패널은 `itemId`만 다루지만, 장비창의 `onUnequip` 클로저가 그 슬롯의 `EquipmentType`을 **캡처**해 정확한 슬롯을 해제한다.

> **첫 번째 소비자일 때는 추상화하지 않았다.** 소비자가 하나일 때 만든 추상화는 그 하나에 맞춰진 모양이라, 두 번째가 오면 어차피 고쳐야 한다. **두 번째에 추출하면 공통점이 실제로 보인다** — YAGNI와 DRY의 균형점.

## 7. 미묘한 토글 요구

```
I키 / HUD 버튼  = 쌍 토글 (인벤토리 상태 기준)
      인벤토리 열림? ─YES→ 둘 다 닫기
                  └─NO → 둘 다 열기
K키             = 장비 단독 토글
각 창 X 버튼     = 자기만 닫기 (독립)
      예) 인벤토리만 X → 장비 잔류 → I키 → 인벤토리만 다시 열림
```

"쌍"은 `InventoryViewController`가 `EquipmentViewController.Show/Hide`를 호출해 묶고, 독립 닫기는 각 View가 자기 GameObject만 끈다. 입력 점유는 **refcount**라 둘 다 열려도 안전하게 누적·복구된다.

## 8. 이 챕터에서 가장 비쌌던 것 — 생성자 한 줄

`ProgressionService` 생성자에 `IEquipmentService`를 추가했다(3절). **단위 테스트는 전부 통과**했는데 통합·E2E 6개가 **타임아웃**으로 실패했다.

```
IProgressionService 만 등록하던 테스트용 DI 호스트 4곳
   (DungeonResultConsumer 통합 · DungeonResultReward E2E · GameStart E2E · RoomLifecycle)
      → 새 의존성을 해석하지 못해 컨슈머가 기동 중 사망
      → 보상이 지급되지 않음
      → 테스트는 "보상 대기"에서 타임아웃  ← 원인과 증상이 멀다
```

**타임아웃은 증상이고 원인은 DI 등록 누락**이었다. 실 스택 호스트엔 장비 체인을, Fake 호스트엔 0-modifier 스텁을 등록해 해소했다.

> **교훈** — 서비스 **생성자를 바꾸는 것은 공유 계약을 바꾸는 것**이다. 단위 테스트는 그 서비스를 직접 만들어 쓰므로 절대 안 깨진다. 통합·E2E까지 돌려야 조립 지점이 검증된다. 이후 "공유 계약·서비스 생성자 변경 후엔 전체 회귀"를 규율로 남겼다.

### 같은 라운드에서 나온 진단 셋

**"입력 테스트 9개 실패"가 내 변경 탓이 아니었다**
`InputRouterTests` 9개가 전부 미발화로 실패했다. 마침 `.inputactions`에 Equipment 키를 더한 직후라 그 탓으로 의심했는데, 추적해 보니 **테스트 Setup이 `Player.Enable()`을 호출하지 않았다.** `InputRouter`가 맵 활성화를 `GlobalInputInitializer`에 위임하도록 바뀐 뒤 유닛 테스트만 미반영이었던 것(자매 테스트에는 그 줄이 있었다).
→ **"최근 바꾼 것" 탓으로 단정하지 말고 `git diff`로 원인 파일을 좁힌다.** 입력 라우터는 내가 건드리지도 않았다.

**"미인증 거부" 테스트가 실행 순서에 따라 실패 — 토큰 누수**
`E2ETestBase`가 `AccessToken`을 SetUp에서 리셋하지 않아, 앞선 테스트가 로그인해 둔 토큰이 다음 테스트로 새어 **미인증 테스트가 인증 상태로 실행**됐다. 기존 인벤토리 E2E는 미인증 케이스가 알파벳순으로 먼저 돌아 **우연히** 안 걸렸을 뿐이다.
→ 테스트 격리는 "각자 만든 것을 정리"만으로 부족하다. **픽스처가 공유하는 상태는 SetUp에서 초기화**해야 한다.

**stale 도커 이미지가 다른 회귀를 숨기고 있었다**
장비 드랍을 보려고 socketserver 이미지를 리빌드하자 **무관한 사망 E2E**가 깨졌다. 원인은 배포되지 않았던 이전 변경이었다 — `MeleeDamage = max(1, AD − Def)`라 슬라임 AD5 − Lv1 Def5 = **1뎀/히트**, HP100을 깎으려면 100히트라 사망 대기가 타임아웃됐다.
→ 내 변경이 아니라 **리빌드가 드러낸 기존 회귀**였다. stale 이미지 가드 경고를 무시하면 **옛 서버를 검증하면서 통과했다고 착각**한다.

## 9. 남은 것

- **개별 인스턴스/강화·랜덤 옵션** — 지금은 스택형(YAGNI). 강화가 생기면 `equipment_instances`로 승격.
- **외형 동기화** — 다른 플레이어에게 착용 장비가 보이지 않는다(코드 검색 결과 미구현). 장비 데이터는 서버에 있으므로 원격 캐릭터 스폰 시 함께 내려보내면 되지만 아직 안 했다.
- **enum 철자** — `Header`(머리)·`Shoose`(신발)는 오타로 보이지만 **C# enum·proto·클라 DLL 3곳에 전파된 공개 계약**이라, 고치려면 proto 재생성과 저장된 슬롯 값 확인이 함께 필요하다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 합류점 하나에 더한다 | 재화·상점이 같은 자리에 합류([18](./chapter-18-wallet-shop.md)) |
| 서비스 이벤트로 크로스 갱신 | HUD 창 MVI 확장의 기본형([22](./chapter-22-hud-windows-mvi.md)) |
| 두 번째 소비자에 추출 | 공용 컴포넌트 도입 기준 |
| 생성자 변경 = 공유 계약 변경 | 전체 회귀 실행 규율 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-17-equipment-system.md](../learning-log/chapter-17-equipment-system.md)

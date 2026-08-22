# 챕터 17 학습 로그 — 장비 시스템 (서버 풀스택 + 클라 MVI/GUI)

> 3.2 + 7.2. 착용 슬롯·장비 스탯 모디파이어 → 전투 스탯 합산. 서버(도메인~gRPC) 권위 + 클라 MVI(장착/해제/인벤토리 연동).
> 핵심: **공통 enum 한 개**와 **합산 합류점 한 곳**으로 "장비가 전투에 반영"되는 경로를 무수정 연결한다.

---

## 설계 결정과 근거

### 정의 / 소유 / 착용을 3개로 쪼갠 이유 — 변하는 축이 다르다

장비를 한 테이블에 다 넣고 싶은 충동이 있지만, **변하는 이유가 셋 다 다르다.** 그래서 책임을 분리했다.

```
정의(정적·기획)   = ItemCatalog(코드) + EquipmentCatalog(코드)   ← 이름·등급·슬롯·스탯. DB 아님
소유(영속·수량)   = inventory_items (UserId,ItemId)→Qty          ← "가지고 있다"
착용(영속·상태)   = user_equipments (UserId,Slot)→ItemId         ← "어디에 꼈다"
```

- 정의는 카탈로그(코드)로 — `GameplayEffectCatalog`·`MonsterCatalog`·`drop-tables.json` 과 동일 컨벤션(정적 데이터는 카탈로그). DB에 `items` 테이블을 만들지 않는다.
- 착용은 소유의 *부분집합 상태*다. 장착해도 소유는 유지(인벤토리에 남음) — 착용은 별도 테이블에 "무엇을 어느 슬롯에"만 기록. 둘을 합치면 "장착하면 인벤토리에서 빠진다"를 DB가 강제하게 돼 유연성을 잃는다(표시 정책은 클라가 결정해야 함, 아래 참조).

장비는 `Stackable:false, MaxStack:1`. **개별 인스턴스(강화수치·랜덤옵션)는 만들지 않았다** — 지금 필요 없다(YAGNI). 강화(3.6)가 생기면 그때 `equipment_instances`로 승격. 스택형이라 "한 종류 1개 소유"가 보장돼 착용 매칭이 `itemId` 비교로 충분해진다.

---

### EquipmentType 하나로 통일 — 클라·서버·proto 단일 소스

처음엔 서버를 Weapon/Armor **2슬롯**으로 만들었는데, GUI 프리팹은 머리·신발·반지 등 **8슬롯**이었다. "GUI의 EquipmentType을 서버도 쓴다"가 요구였다. 슬롯 enum이 세 군데(서버 도메인 / proto / 클라 GUI)에 따로 있으면 매번 매핑·드리프트가 생긴다.

```
Shared.Gameplay.Equipment.EquipmentType   ← 단일 정의(8슬롯, None=0)
        │  (클라는 Plugins/Shared.Gameplay.dll 로 동일 타입 참조)
   ┌────┴───────────────┐
[서버 도메인]         [클라 GUI/Presentation/System]
   │
proto enum EquipmentType (9값) — 정수값을 도메인과 1:1로 맞춤
   → 경계 매핑이 (ProtoType)(int)x 캐스팅 한 줄로 끝남
```

proto는 C# enum을 직접 참조할 수 없으니 **별도 enum이되 정수값을 도메인과 1:1**로 정의했다. 그러면 gRPC 경계(서버/클라 양쪽)에서 `switch` 매핑이 아니라 단순 캐스팅이 된다. 카탈로그는 아직 Weapon/Armor만 채우고 나머지 6슬롯은 빈 칸 — enum 값은 있되 아이템이 없을 뿐(GUI 표시·미래 확장 자리).

> 트레이드오프: 이미 만든 2슬롯 proto/도메인을 8슬롯으로 **재작업**해야 했다. 하지만 `user_equipments` 테이블이 비어 있어(개발 단계) 마이그레이션 리스크가 없었고, "공통 enum" 요구를 정확히 만족한다.

---

### 합산 합류점은 딱 한 곳 — GetStatsAsync

장비 스탯을 전투에 반영하려면 데미지 계산·HP 등 여러 곳을 고쳐야 할 것 같지만, **이미 모든 스탯이 수렴하는 단일 지점**이 있었다.

```
ProgressionService.GetStatsAsync(userId):
    base     = LevelTable.StatsAt(level)              (레벨 룩업)
  + equipped = IEquipmentService.GetEquippedStatsAsync()   ← 추가한 한 줄
    = PlayerStats(합산)
       │
       ├─▶ GameStartRequestedMessage → SocketServer InitPlayerState → 전투 데미지
       └─▶ gRPC GetProgression → 클라 스탯창
```

이 한 곳에 장비 Σ를 더하니 **SocketServer·전투·스탯창이 전부 무수정**으로 장비를 반영한다(authority-model §4c "SocketServer는 합산 결과만 받는다"). 합산 *로직 자체*는 장비 도메인(`GetEquippedStatsAsync`)에 응집 — Progression은 base에 더하기만 하고 장비 내부(카탈로그·슬롯)를 모른다. 버프가 생겨도 같은 자리에 합류한다.

---

### 장착하면 인벤토리에서 "사라진다" — DB가 아니라 표시 정책

요구: 장착하면 인벤토리에서 그 아이템이 사라지고 장비창에 나타난다(해제하면 반대). 서버는 장착해도 **소유를 유지**한다(인벤토리에 남음). 그러면 인벤토리에 그대로 보일 텐데?

→ **DB에서 빼는 게 아니라, 클라 표시에서 착용분을 필터링**한다.

```
InventoryModel.Refresh:
    items    = GetInventory()           (소유 전체)
    equipped = GetEquipped()            (착용 itemId 집합)
    표시      = items - equipped         (착용분 제외)
```

"이동"은 시각적 착시일 뿐 — 소유는 한 곳(서버)에 그대로. 장비가 스택1이라 `itemId` 매칭만으로 제외가 정확하다. DB를 진실로 두고 표시 정책은 클라가 갖는 분리.

---

### 장착/해제 시 두 창이 동시에 갱신 — OnChanged 한 이벤트, MVI는 유지

장비창에서 해제하면 **인벤토리도** 갱신돼야 한다(아이템이 돌아옴). 하지만 MVI 규칙상 **View는 자기 Model만** 안다 — Inventory View가 EquipmentModel을 부를 수 없다.

```
IEquipmentService.OnChanged (장착/해제 성공 시 1회 발행)
   ├─▶ EquipmentModel 구독 → Refresh (장비창 갱신)
   └─▶ InventoryModel 구독 → Refresh (착용 필터 재적용 → 표시 갱신)
```

System 레이어 서비스가 변경을 통지(plain `event Action`), 두 Presentation Model이 각자 구독해 재조회. View끼리 직접 안 엮이고, 한 이벤트로 두 창이 일관되게 동기화된다. 장착 의도도 인벤토리 패널 → `InventoryModel.EquipItem` → `IEquipmentService.EquipAsync`(Presentation→System, 허용)로 흐른다.

---

### ItemActionPanel 팝업을 공용으로 추출 — 두 번째 소비자가 생긴 시점

슬롯 클릭 → 오른쪽에 액션 패널(Addressable 로드 + 백드롭 + 위치 계산 + 닫기)은 원래 Inventory 안에 있었다. 장비창도 똑같이 필요해지자(해제 버튼), **두 번째 소비자가 생긴 시점에** 공용 컨트롤러로 뺐다.

```
ItemActionPanelController (GUI/Common)
   OpenAsync(canvas, slotRect, configure)   ← 로드·배치·재진입가드·닫기
   Close()
호출처가 버튼 구성만 주입:
   Inventory : panel.Bind(id, onUse, onEquip, null, canUse, canEquip, false)
   Equipment : panel.Bind(id, null, null, onUnequip, false, false, true)
```

패널은 `itemId`만 다루지만, 장비창의 `onUnequip` 클로저가 그 슬롯의 `EquipmentType`을 **캡처**해 정확한 슬롯을 해제한다. (1번째 소비자일 때 미리 추상화하지 않고, 2번째에 추출 — YAGNI와 DRY의 균형.)

---

### 인벤토리 + 장비 열고 닫기 연동 — 쌍 토글 vs 독립 닫기

요구가 미묘했다: I키로 열면 **둘 다** 뜨고, 둘 다 떠 있으면 I키로 **둘 다** 닫히고, 하지만 각자 X로 닫으면 **따로** 닫힌다.

```
[I키 / HUD 버튼]  = 쌍 토글 (인벤토리 상태 기준)
   인벤토리 열림? ─YES→ 둘 다 닫기
              └─NO → 인벤토리 + 장비 둘 다 열기
[K키]            = 장비 단독 토글
[각 창 X 버튼]    = 자기 SetActive(false) (독립)
   예) 인벤토리만 X → 장비 잔류 → I키 → 인벤토리만 다시 열림(장비 유지)
```

`InventoryViewController`가 `EquipmentViewController.Show/Hide`를 호출해 "쌍"을 묶고, 독립 닫기는 각 View가 자기 GameObject만 끈다. 입력 점유(`UiInputCaptureBehaviour`)는 refcount라 둘 다 열려도 안전하게 누적/복구.

---

## 트러블슈팅 (이번 작업의 실제 디버깅)

### 서비스 생성자에 의존성 추가 = DI 호스트 4곳이 조용히 깨짐

`ProgressionService` 생성자에 `IEquipmentService`를 추가하자(합산 합류), **단위 테스트는 통과**하는데 통합/E2E 6개가 *타임아웃*으로 실패했다. 원인은 `IProgressionService`만 등록하던 **테스트용 DI 호스트 4곳**(DungeonResultConsumer 통합·DungeonResultReward E2E·GameStart E2E·RoomLifecycle)이 새 의존성을 못 풀어 컨슈머가 죽은 것 — 보상이 지급 안 돼 대기가 만료됐다.

교훈: **서비스 생성자 변경 후엔 단위만 보지 말고 통합·E2E까지** 돌린다. 실 스택 호스트엔 장비 체인을, Fake 호스트엔 스텁(0 modifier)을 등록해 해소. (메모리에 "공유계약/서비스 생성자 변경 후 전체 회귀" 규율로 남김.)

### "입력 테스트 9개 실패"는 사용자 입력 변경 탓이 아니었다

`InputRouterTests` 9개가 *모든 액션 미발화*로 실패. 사용자가 `.inputactions`에 Equipment 키를 더한 직후라 그 탓으로 의심했지만, 추적해 보니 **테스트 Setup이 `_actions.Player.Enable()`를 안 했다**. `InputRouter`가 (설계 변경으로) 맵 활성화를 `GlobalInputInitializer`에 위임하게 바뀐 뒤, 그 초기화가 없는 유닛 테스트가 미반영된 것(자매 `InputContextTests`엔 `Player.Enable()` 있음). → Setup에 한 줄 추가로 10/10.

교훈: "최근 바꾼 것" 탓으로 단정하지 말고 `git diff`로 원인 파일을 좁힌다. 입력 라우터는 내가 안 건드렸다.

### "미인증 거부" 테스트가 순서에 따라 실패 — 토큰 누수

`EquipmentE2ETests`의 "미인증 호출은 거부된다" 2개가 실패. `E2ETestBase`가 `AccessToken`을 **SetUp에서 리셋하지 않아**, 같은 픽스처의 앞선 테스트가 로그인해 둔 토큰이 다음 테스트로 누수 → 미인증 테스트가 인증 상태로 실행됐다. 기존 인벤토리 E2E는 미인증 테스트가 _알파벳순으로 먼저_ 실행돼 우연히 안 걸렸을 뿐. → SetUp 토큰 리셋(전 Https E2E 공통 개선).

### UI를 열어도 WASD로 캐릭터가 움직임 — 차단 컴포넌트 누락

인벤토리/장비창을 열었는데 이동이 됐다. 로비(DungeonRoomLobbyView)는 막혔다. 차이는 `UiInputCaptureBehaviour`(활성 동안 `IInputContext.EnterUi`로 Player 맵 OFF, refcount) 부착 여부였다 — 로비엔 있고 인벤토리/장비엔 없었다. → 두 창에 동일 패턴 적용(Model의 `BeginUiCapture/EndUiCapture` 경유, GUI는 System 직접 비참조).

### stale 도커 이미지가 2.4 밸런스 회귀를 숨기고 있었다

장비 드랍을 위해 socketserver 이미지를 **2.4 작업(몬스터 Defense 반영) 이후 처음 리빌드**하자, 무관한 사망 E2E 1개가 깨졌다. 원인: `MeleeDamage = max(1, AD − Def)`라 slime AD5 − Lv1 Def5 = **1뎀/히트** → HP100을 깎으려면 100히트 → 사망 대기 타임아웃. 내 장비 변경이 아니라, *배포 안 된 2.4 변경*이 리빌드로 드러난 것. 별도 작업으로 분리(전투 밸런스는 별도 결정).

교훈: stale 이미지 가드가 계속 경고하면 무시하지 말 것 — 옛 서버를 검증해 회귀를 숨긴다.

---

## 아직 미완성인 것 (TODO)

```
Equipment.prefab 슬롯/버튼·ItemActionPanel unEquipButton 인스펙터 할당 + 플레이 검증 (Unity 작업)
장비 아이콘 스프라이트 (ItemDisplayCatalog entry는 등록, 아이콘은 디자이너 할당)
개별 인스턴스/강화·랜덤옵션 (3.6) — 지금은 스택형(YAGNI)
외형 동기화 (다른 플레이어에게 착용 장비 보이기)
2.4 슬라임 데미지 밸런스 회귀 → 사망 E2E (별도 작업)
클라 wrapper/System은 ClientCodegen 자동생성 — proto 추가 시 재실행 필요
```

---

## 핵심 키워드 정리

| 키워드                    | 한 줄 설명                                                                          |
| ------------------------- | ----------------------------------------------------------------------------------- |
| 정의/소유/착용 3분리      | 카탈로그(코드)=정의 / inventory_items=소유 / user_equipments=착용, 변하는 축이 다름 |
| EquipmentType 공통화      | Shared.Gameplay 단일 enum(8슬롯), proto는 정수 1:1 → 경계 캐스팅 매핑               |
| 합산 합류점 단일          | GetStatsAsync 한 곳에 장비 Σ → SocketServer·전투·스탯창 무수정 반영                 |
| 합산 로직 응집            | Σ는 장비 도메인(GetEquippedStatsAsync)이 소유, Progression은 더하기만               |
| 착용=표시 필터            | 장착해도 DB 소유 유지, 인벤토리 표시에서 착용 itemId만 제외(스택1 매칭)             |
| OnChanged 크로스 갱신     | 서비스 이벤트 1개를 두 Model이 구독 → 인벤토리·장비 동시 갱신, MVI 유지             |
| ItemActionPanelController | 2번째 소비자(장비) 시점에 패널 로직 공용 추출(YAGNI↔DRY 균형)                       |
| 쌍 토글/독립 닫기         | I키=둘 다, K키=장비 단독, 각 X=독립 / 입력 점유는 refcount                          |
| 생성자 변경 회귀          | IEquipmentService 추가 → DI 호스트 4곳 미등록(단위는 통과, 통합·E2E로 잡음)         |
| stale 이미지 함정         | socketserver 미리빌드가 2.4 밸런스 회귀를 은닉 → 가드 경고 무시 금지                |

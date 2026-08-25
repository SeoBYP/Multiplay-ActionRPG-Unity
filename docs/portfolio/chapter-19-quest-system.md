# 19. 퀘스트 — 새 권위를 만들지 않고 기존 funnel에 얹기

> **한 줄** — 퀘스트는 새로운 권위도, 새로운 저장소도, 새로운 지급 경로도 만들지 않았다. **서버가 이미 검증하고 있는 킬 경로 한 곳**에 진행 훅을 얹고, 보상은 검증된 서비스들을 조합했다. 그래서 추가된 것은 사실상 **한 줄**이다.
>
> **범위** 진행 권위 · 파생 상태 · 조합 보상 · 캐시 판단 · 훅 위치 선택
> **하이라이트** 훅을 어디에 달지를 **깨질 테스트 호스트 수로 계산**한 것 (6절)

---

## 1. 진행을 클라가 보고하면 안 되는 이유

가장 쉬운 설계는 클라가 `ReportKill(monsterId)`을 부르는 것이다. 그러면 **클라가 킬을 위조해 퀘스트를 무한 진행**시킨다. 보상이 exp·gold·item이므로 곧바로 경제 핵이 된다.

그런데 이 프로젝트에는 **이미 서버가 인정한 "진짜 킬 1회"** 가 있었다 — Main 획득을 B-lite로 막을 때 만든 킬 클레임이다([16](./chapter-16-main-loot-path.md)).

```
클라: ClaimMonsterExp(mapId, slotId)          ← 클라가 보내는 건 "슬롯"뿐
        ▼
서버: MainSpawnClaimService.ClaimExpAsync
        ├─ ValidateSlot(map, slot)              스폰 데이터에 존재하는가
        ├─ 쿨다운 통과?  ── 실패 → 0            파밍률 상한
        ├─ AddExpAsync(...)                      (기존)
        └─▶ IQuestService.ReportKillAsync(...)   ← 추가한 한 줄
                 Accepted·미완료·KillMonster·TargetId 일치 퀘스트의 progress++
```

**`ReportKillAsync`는 서버 내부 호출이고 gRPC 표면에 없다.** 클라가 부를 방법이 없다. 게다가 **monsterId조차 클라가 정하지 못한다** — 서버가 슬롯에서 스폰 데이터를 읽어 얻는다(`MainSpawnClaimService.cs:110`).

> **일반화** — "새 기능의 권위를 어떻게 지킬까"의 답이 항상 새 검증은 아니다. **이미 검증을 통과한 지점**을 찾으면 그 위에 얹는 것으로 끝난다. 검증은 한 번만 하고, 그 결과를 여러 소비자가 나눠 쓴다.

## 2. "완료"는 저장하지 않는다

`UserQuest`가 저장하는 건 **`Status`(Accepted/Claimed) + `Progress`** 둘뿐이다. "완료" 컬럼은 없다.

```
행 없음                      → NotAccepted
Status == Claimed            → Claimed
Accepted, Progress <  Req    → Accepted   (진행 중)
Accepted, Progress >= Req    → Completed  (수령 가능)
```

완료는 `Progress ≥ RequiredCount`로 **매번 파생**한다. **저장하는 상태가 적을수록 불일치가 없다** — completed 컬럼을 두면 progress와 따로 놀 여지가 생기고, 그 순간 "진행은 3/3인데 미완료" 같은 상태가 가능해진다.

`RequiredCount`는 정적 카탈로그에 있으므로 완료 판정은 **서비스 책임**이다. 엔티티의 `AddProgress(amount, required)`·`Claim(required)`는 호출자가 required를 주입받는다 — **도메인 엔티티가 정적 데이터를 참조하지 않게** 하려는 것이다.

## 3. 보상은 조합, 수령은 선마킹

퀘스트는 자기 보상 저장소가 없다. 이미 있는 서비스를 **조합**한다 — 상점이 지갑·인벤토리를 조합한 것과 같다([18](./chapter-18-wallet-shop.md)).

```
ClaimReward(questId)
  검증: Accepted + Progress ≥ Required + !Claimed
  ① Status = Claimed 를 먼저 영속        ← 선마킹
  ② 그 다음 지급
       exp  → ProgressionService.AddExp
       gold → WalletService.Add
       item → InventoryService.GrantItem
```

**순서가 전부다.** Claimed를 먼저 쓰면 지급 도중에 재요청이 와도 "이미 수령"으로 막힌다. 지급이 실패하면 보상은 못 받지만 **재수령도 막힌다.**

같은 판단을 세 번째 하고 있다 — 던전 보상의 claim-first([14](./chapter-14-dungeon-clear-loop.md)), 상점의 차감 선행([18](./chapter-18-wallet-shop.md)), 그리고 여기. **"이중 지급보다 미지급이 낫다"** 는 경제 보수성이다.

## 4. 클라 카탈로그 미러를 만들지 않았다

아이템은 클라가 `ItemDisplayCatalog`로 정의를 미러한다 — 아이콘이 **시각 자산**이라 서버가 들고 있을 이유가 없기 때문이다([15](./chapter-15-loot-drop-inventory.md) 5절).

퀘스트는 반대로 갔다.

```
GetQuests → QuestCatalog.All × UserQuest 병합
          → QuestInfo { id, name, desc, objective, target, required,
                        progress, status(4-state), reward }   ← 정의까지 통째로
```

**수가 적고 전부 텍스트**라 미러를 유지하는 비용이 이득보다 크다. 서버가 정의와 상태를 병합해 내려주면, 새 퀘스트는 **서버 카탈로그에 한 줄 추가**하는 것으로 클라에 자동 반영된다. 클라 배포가 필요 없다.

> **같은 문제, 반대 답** — 판단 기준은 "미러 동기화 비용 vs 매번 전송 비용"이다. 아이콘은 전자가 싸고, 텍스트 몇 줄은 후자가 싸다. **원칙을 기계적으로 적용하지 않은 자리**다.

## 5. 캐시를 붙이지 않는 것도 결정이다

인벤토리·지갑·방은 전부 Cache-Aside + Delete를 쓴다. 퀘스트 저장소는 **캐시를 안 붙였다.**

```
퀘스트 창  가끔 연다        (read-rare)
진행       킬마다 쓴다       (write-heavy)
```

Cache-Aside는 **read-heavy일 때 이득**이다. 쓰기마다 DEL해야 하면 **캐시가 적중하기 전에 또 무효화**된다 — 캐시 관리 비용만 내고 이득은 없다. 그래서 `AsNoTracking` DB 직읽기가 더 단순하고 빠르다.

> **캐시는 기본값이 아니라 접근 패턴에 맞을 때만 붙인다.** 다른 도메인이 다 쓴다고 따라 붙이면 비용만 는다.

## 6. 훅 위치를 "깨질 테스트 수"로 골랐다

시드 퀘스트 3종 중 `CollectItem` 타입은 **목표 타입 구조만 두고 진행 훅을 달지 않았다.** 기능을 못 만들어서가 아니라 **비용을 계산했기 때문**이다.

```
KillMonster 훅
   → MainSpawnClaimService 한 곳의 생성자만 변경
   → 그 서비스를 수동 조립하는 테스트 = 1개

CollectItem 훅
   → InventoryService.GrantItemAsync (모든 획득 funnel) 에 IQuestService 주입 필요
   → 그 서비스를 수동 조립하는 DI 호스트 = 6곳 이상
     (LootGrant 통합 · DungeonResult 통합 · 각종 E2E …)
```

직전 챕터에서 **생성자에 의존성 하나를 추가했다가 DI 호스트 4곳이 조용히 깨진 일**을 겪었다([17](./chapter-17-equipment-system.md) 8절). 그때 배운 것을 여기서 **사전에 적용**했다.

> **"어느 funnel에 얹느냐" = "몇 곳이 깨지느냐"** 다. MVP 가치 대비 리스크가 커서 가장 좁은 funnel 하나만 골랐다. enum·정의·UI는 이미 있으니 나중에 훅 한 줄만 붙이면 된다.
>
> 실제로 `CollectItem`은 지금도 훅이 없다 — 대신 카탈로그 무결성 테스트(`CatalogIntegrityTests`)가 "목표 아이템이 items.json에 실재하는지"를 고정해 **정의가 썩는 것만은 막고 있다.**

## 7. 클라가 못 하는 것은 계약에도 없다

```
서버 ──gRPC QuestInfo──▶ System QuestService (proto 은닉, enum→도메인)
   ──QuestData──▶ Presentation QuestModel (MVI)
   ──QuestEntryModel (string/bool/int 만)──▶ GUI Quest View
```

MVI 레이어 규칙에서 막힌 지점이 있었다 — **`Game.GUI`는 `Game.System`을 참조할 수 없다.** 그래서 `QuestEntryModel`이 System 타입(`QuestRewardData`)을 그대로 노출하면 View가 읽는 순간 위반이다.

해결은 **View가 쓰는 것을 전부 primitive로 낮추는 것**이었다.

```
보상  → IReadOnlyList<string> RewardLines   (Model 이 포맷한 문자열)
상태  → CanAccept / CanClaim / IsClaimed    (불리언)
```

도메인 enum과 DTO는 Presentation 안에 가둔다. **View에 무엇을 노출할지가 곧 레이어 경계**다.

그리고 클라 서비스에는 **진행 RPC가 없다** — `GetQuests`/`Accept`/`Claim` 셋뿐이다. 1절에서 진행을 서버 내부로 둔 결정이 계약 표면에 그대로 드러난다.

## 8. 그 이후 — NPC 대화가 합류하면서 생긴 비대칭

원본의 "다음"은 *"NPC를 퀘스트 수주/턴인 창구로, `TalkToNpc` 목표의 진행원으로 합류"* 였다. 실제로 구현됐다 — `NPCDialogueInteractable`·`DialogueCameraController`·`NPCCharacterAgent`가 있고, `quest.proto`에 **`ReportTalk`가 추가**됐다.

그런데 여기서 **1절의 원칙이 한 곳 깨졌다.**

```
KillMonster   클라 → ClaimMonsterExp(slot)  → 서버가 슬롯 검증 → 서버 내부 ReportKill
                                                                  ▲ gRPC 표면에 없음

TalkToNpc     클라 → ReportTalk(npcId)  ──────────────────────────▶ 진행 +1
                                          ▲ gRPC 표면에 있고,
                                            "실제로 그 NPC와 대화했는가"를 검증하지 않는다
```

`ReportTalkAsync`가 확인하는 것은 인증·퀘스트 상태·`TargetId` 일치뿐이다. **근접 검증도 쿨다운도 없다** — 클라가 NPC 근처에 가지 않고도 호출할 수 있다.

**피해 범위는 제한적이다.** `AddProgress`가 `RequiredCount`를 상한으로 두고, 보상은 Claimed 선마킹으로 1회만 나간다. 즉 얻는 건 "NPC까지 걸어가는 시간 절약"이지 무한 파밍이 아니다. 킬 위조(무제한 경제 이득)와는 급이 다르다.

그래도 기록해 둘 가치가 있다 — **이 챕터가 세운 "클라는 진행을 건드릴 수 없다"가 더 이상 전면적으로 참이 아니다.**

### 2026-08-25 후속 — "근접 검증을 넣으면 된다"는 틀렸다

위 문단은 원래 *"서버가 NPC의 위치를 알고 있으므로 근접 검증을 넣으면 된다"* 로 끝났다. 실측해 보니 **전제가 둘 다 사실이 아니었다.**

| 근접 검증에 필요한 것 | 실제 |
|---|---|
| NPC 위치 | 서버에 **없다**. NPC는 씬에 배치된 `NPCDialogueInteractable`(`[SerializeField] npcId`)이고 위치 카탈로그가 없다. 서버가 아는 건 `quests.json`의 `targetId` 문자열뿐 |
| 플레이어 위치 | 서버가 **모른다**. Main 씬은 소켓 미연결이라 위치를 보낼 채널 자체가 없다 |

대조군으로 인용한 `ClaimMonsterExp`조차 위치를 검증하지 않는다 — `(mapId, slotId)`가 카탈로그에 있는지 확인하고 Redis 쿨다운으로 **파밍률 상한**을 걸 뿐이다. 즉 이 프로젝트가 Main에서 쓰는 방어 교리는 *"위치 증명"이 아니라 "카탈로그 존재 + 레이트 상한"* 이었다. 비대칭의 정체는 "ReportTalk만 검증이 빠졌다"가 아니라 **Main 전체가 클라 권위**라는 구조였다.

**그래서 목표를 바꿨다.** 대화 진행 치팅은 운영상 막을 실익이 없다(상한 1회·보상 1회). 대신 *"정상 요청만 서버에 오게 한다"* 로:

```
① NPC.hasQuest == false ─────────────▶ 통신 0회            ← 저작 플래그(조기 차단)
② true → 퀘스트 상태 갱신
③ 이 npcId 대상 TalkToNpc가 Accepted ─아니면▶ 보고 생략     ← 실제 판단(서버가 준 상태)
④ 맞으면 ReportTalk → 성공 시 상태 재갱신
```

서버는 카탈로그에 대화 목표가 없는 `npcId`면 **DB를 읽지 않고** 0을 반환한다(예전에는 저장소를 먼저 읽고 매칭했다). 실패를 에러로 만들지는 않았다 — 퀘스트 없는 NPC와 대화하는 것은 정상 행동이기 때문이다.

**남는 한계는 정직하게 남긴다.** "안 걸어가도 대화 퀘스트를 완료할 수 있다"는 그대로다. 그건 `ReportTalk`의 결함이 아니라 Main이 클라 권위라는 구조의 단면이고, 해소 조건은 **Main의 서버 권위 승격**이다.
(※ 코드 경로상 확인. 실제 악용 재현은 **미실측**)

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 검증된 funnel에 얹는다 | 새 진행원(대화 등)이 같은 자리에 합류 |
| 저장 상태는 최소로, 나머지는 파생 | 상태 불일치 가능성 자체를 없애는 패턴 |
| 선마킹 후 지급 | 경제 도메인 전반의 at-most-once |
| 캐시는 패턴에 맞을 때만 | 도메인별 저장 전략 판단 기준 |
| 훅 위치 = 블래스트 반경 | 변경 범위를 미리 세는 습관 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-19-quest-system.md](../learning-log/chapter-19-quest-system.md)

# 챕터 19 학습 로그 — 퀘스트(Quest) 시스템

> 4.4. 수주 → 진행 → 완료 → 보상수령의 풀 루프. **진행은 서버 권위**(클라가 "킬했다"를 보고하지 않는다), 보상은 기존 도메인(Progression·Wallet·Inventory)의 **조합**, 중복 수령은 **Claimed 선마킹**으로 막는다.
> 핵심: 퀘스트는 새로운 권위를 만들지 않는다 — 이미 서버가 검증하는 **킬 클레임 경로 한 곳**에 진행 훅을 얹고, 보상은 검증된 지급 서비스들을 **오케스트레이션**할 뿐이다.

---

## 설계 결정과 근거

### 진행은 클라 보고가 아니라 "서버가 이미 검증하는 킬"에 얹는다

가장 쉬운(그리고 틀린) 설계는 클라가 `ReportKill(monsterId)`를 RPC로 보내는 것이다. 그러면 클라가 킬을 위조해 퀘스트를 무한 진행시킨다 — 보상이 exp/gold/item이므로 곧 경제 핵이다.

이 프로젝트는 이미 Main 획득을 **B-lite 서버 검증**(챕터 16)으로 막아뒀다: 클라는 "어느 슬롯을 죽였다"(mapId, slotId)만 보고하고, 서버가 스폰 데이터로 슬롯을 검증하고 per-user 쿨다운으로 파밍률을 상한한다. 이 **`ClaimMonsterExp`(킬 클레임)가 곧 "서버가 인정한 진짜 킬 1회"**다. 퀘스트 진행은 거기에 한 줄 얹으면 된다 — 별도 진행 RPC를 만들지 않는다.

```
클라: ClaimMonsterExp(map, slot)              ← 클라가 보내는 건 "슬롯" 뿐
        ▼
서버: MainSpawnClaimService.ClaimExpAsync
        ├─ ValidateSlot(map, slot)             (스폰 데이터에 존재?)
        ├─ exp 쿨다운 통과?  ── 실패 → 0(파밍 차단)
        ├─ AddExpAsync(...)                     (기존)
        └─▶ IQuestService.ReportKillAsync(userId, slot.MonsterId)   ← 신규 한 줄
                 └─ Accepted·미완료·KillMonster·TargetId==monsterId 퀘스트 progress++
```

`ReportKillAsync`는 **서버 내부 호출**이고 gRPC 표면에 없다. 클라가 진행을 건드릴 방법이 없다. monsterId조차 클라가 못 정한다 — 서버가 슬롯 → 스폰 데이터에서 읽는다. 진행 위조 = 불가능.

> 트레이드오프: MVP는 Main 킬 경로(`ClaimExpAsync`)만 훅했다. 던전 맵클리어 킬은 per-monster 청구가 아니라 범위 밖. 새 진행원이 생기면 그 funnel에 같은 한 줄을 추가하는 구조다.

### "완료"는 상태가 아니라 파생이다

`UserQuest`에 저장하는 건 **Status(Accepted/Claimed) + Progress** 둘뿐이다. "완료"를 별도 컬럼으로 두지 않았다 — `Progress ≥ RequiredCount`(카탈로그)로 매번 파생한다. 저장하는 상태가 적을수록 불일치가 없다(progress와 completed가 따로 놀 여지를 없앤다).

UI가 보는 4-상태는 서비스가 합성한다:

```
행 없음                         → NotAccepted
Status==Claimed                 → Claimed
Status==Accepted, Prog<Req      → Accepted   (진행 중)
Status==Accepted, Prog≥Req      → Completed  (보상 수령 가능)
```

`RequiredCount`는 정적 카탈로그(`QuestCatalog`)에 있으므로 완료 판정은 **서비스 책임**(엔티티는 카탈로그를 모른다). 엔티티의 `AddProgress(amount, required)`·`Claim(required)`는 호출자가 required를 주입한다 — 도메인이 정적 데이터에 의존하지 않게.

### 보상 = 조합 도메인, 수령 = Claimed 선마킹 후 지급(at-most-once)

퀘스트는 자기만의 보상 저장소가 없다. 보상(exp/gold/item)은 이미 있는 서비스들을 **조합**한다 — 상점(챕터 18)이 지갑·인벤토리를 조합한 것과 같은 패턴.

```
ClaimReward(questId):
  검증: Accepted + Progress≥Required + !Claimed   (아니면 실패)
  ① Status=Claimed 먼저 마킹·영속        ← 선마킹
  ② 그 다음 지급:
       exp  → IProgressionService.AddExp
       gold → IWalletService.Add
       item → IInventoryService.GrantItem
```

순서가 핵심이다. **Claimed를 먼저 영속한 뒤 지급**한다. 지급 도중/직후 재요청이 와도 이미 Claimed라 "이미 수령"으로 막힌다 → 중복 보상 불가(at-most-once). 지급이 실패하면 보상은 못 받지만 재수령도 막힌다 — "이중 지급보다 미지급이 낫다"는 경제 보수성(던전 보상 멱등과 동일 사상).

### GetQuests = 전체 카탈로그 × 상태 병합 → 클라 카탈로그 미러가 없다

아이템은 클라가 `ItemDisplayCatalog`로 정의를 미러한다(아이콘·이름이 시각 자산이라). 퀘스트는 그러지 않았다 — **수가 적고 텍스트뿐**이라, 서버가 `GetQuests`에서 전체 카탈로그 × 유저 상태를 병합해 def(이름/설명/보상)까지 통째로 내려준다.

```
GetQuests → repeated QuestInfo { id,name,desc,objective,target,required,
                                 progress, status(4-state), reward }
            = QuestCatalog.All  ×  UserQuest(병합)   (미수주 포함 전체)
```

→ 클라는 퀘스트 카탈로그를 들고 있을 필요가 없다. 새 퀘스트는 서버 `QuestCatalog`에 한 줄 추가하면 클라가 자동으로 받는다. (아이템과 달리 미러 동기화 부담이 없다.)

### DB-only — 캐시를 안 붙이는 것도 결정이다

다른 도메인(인벤/지갑/방)은 Cache-Aside + Delete를 쓴다. 퀘스트 저장소는 **Redis 캐시를 안 붙였다.** 접근 패턴이 다르기 때문이다 — 퀘스트 창은 가끔 열고(read-rare), 진행은 킬마다 쓴다(write-heavy). 캐시는 read-heavy일 때 이득인데, write마다 DEL해야 하면 캐시 적중 전에 또 무효화된다. 그래서 DB 직읽기(`AsNoTracking`)가 더 단순하고 빠르다. 캐시는 "기본값"이 아니라 패턴에 맞을 때만 붙인다.

`user_quests`((UserId,QuestId) 복합키), upsert는 키로 tracked 조회 후 `SetValues`(없으면 Add) — detached 입력을 안전 반영.

### 블래스트 반경을 1곳으로 — CollectItem 훅을 일부러 보류했다

시드 3종 중 `quest_potion_collect`(CollectItem)는 **목표 타입 구조만** 두고 진행 훅을 달지 않았다. 이유는 비용/리스크다.

KillMonster 훅은 `MainSpawnClaimService` **한 곳**의 생성자만 바꾼다 → 그 서비스를 수동 조립하는 테스트는 1개뿐(고치기 쉬움). 반면 CollectItem 진행은 `InventoryService.GrantItemAsync`(모든 획득 funnel)에 `IQuestService`를 주입해야 하는데, 직전 챕터(도감)에서 겪었듯 그 생성자를 바꾸면 **그 서비스를 수동 조립하는 DI 호스트 6+곳**(LootGrant/DungeonResult 통합·E2E 등)이 한꺼번에 깨진다. MVP 가치 대비 리스크가 커서 **의도적으로 보류**했다(YAGNI + 블래스트 반경 관리). enum·정의·UI는 다 있으니 훅 한 줄만 후속으로 붙이면 된다.

> 교훈(직전 챕터에서 학습): **생성자에 의존성 1개 추가 = 그 타입을 수동 조립하는 모든 테스트 호스트가 깨진다.** 그래서 "어느 funnel에 얹느냐"가 곧 "몇 곳이 깨지느냐"다. 가장 좁은 funnel(MainSpawnClaim 1곳)을 골랐다.

### 클라: 기존 View 스캐폴드 재사용 + MVI 레이어 규칙 준수

UI는 새로 만들지 않고 이미 저작돼 있던 마스터-디테일 스캐폴드(`Quest` 창 + `QuestSlot`/`QuestConditionSlot`/`QuestRewardSlot`)에 `Bind`만 추가해 `QuestModel`(MVI)에 배선했다. HUD `btn_Quest`도 기존 버튼을 그대로 썼다(도감과 달리 버튼이 이미 있었다).

MVI 레이어 규칙에서 막힌 지점: **`Game.GUI`는 `Game.System`을 참조하면 안 된다.** 그래서 `QuestEntryModel`(Presentation)이 System 타입(`QuestRewardData`)을 그대로 노출하면 View가 그걸 읽는 순간 위반이 된다. 해결: View가 쓰는 건 전부 **string/bool/int**로만 노출했다 — 보상은 `IReadOnlyList<string> RewardLines`(Model이 포맷한 문자열), 상태는 `CanAccept`/`CanClaim`/`IsClaimed` 불리언. 도메인 enum/DTO는 Presentation 안에 가둔다.

```
서버 ──gRPC QuestInfo(proto)──▶ System QuestService(proto 은닉, enum→도메인)
   ──QuestData──▶ Presentation QuestModel(MVI) ──QuestEntryModel(string/bool만)──▶ GUI Quest View
```

진행(ReportKill)은 클라에 RPC가 없으므로 System 서비스는 GetQuests/Accept/Claim 3개뿐이다 — "클라가 못 하는 것은 계약에도 없다".

---

## 무엇을 만들었나 (요약)

| 레이어 | 추가 |
|--------|------|
| Domain | `QuestObjectiveType`·`QuestStatus`·`QuestDef`/`QuestReward`·`QuestCatalog`(시드 3)·`UserQuest`(불변식) |
| Application | `IQuestService`/`QuestService`(Accept/ReportKill/ClaimReward/GetQuests) · `IQuestRepository` · 결과/뷰 타입 |
| Infrastructure | `QuestRepository`(DB-only upsert) · `UserQuestConfiguration` · 마이그레이션 `AddUserQuests`(멱등 raw SQL) |
| 진행 훅 | `MainSpawnClaimService.ClaimExpAsync` → `ReportKillAsync` (생성자 +IQuestService) |
| 계약 | `quest.proto`(GetQuests/AcceptQuest/ClaimQuestReward) + `QuestGrpcService` |
| 클라 | System `Game.System.Quest` · Presentation `QuestModel` · GUI 스캐폴드 배선 · `QuestViewController`(HUD 토글) · DI |

## 검증

- **서버 373/373**(단위 + Testcontainers 통합 + E2E): QuestService 9 · QuestGrpc 4 · QuestRepository 통합 4 · 킬훅 2.
- **클라 E2E 풀루프**(`QuestE2ETests`, Docker): 수주 → `ClaimMonsterExp` 슬롯 1/2/3로 슬라임 3킬 → Completed → `ClaimQuestReward` → **골드 +100이 지갑에 반영** → Claimed. + 미완료 수령 거부 · 중복 수주 거부 · 미인증 RpcException. 전체 PlayMode 117/117.
- **플레이 검증 통과**(인게임 수주→처치→보상 수령 정상).

## 다음

NPC/대화(4.5) — NPC를 퀘스트 **수주/턴인 창구**로, `TalkToNpc` 목표 타입의 진행원으로 합류시킨다. (지금은 퀘스트 창에서 직접 수주.)

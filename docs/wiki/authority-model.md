# 클라이언트 vs 서버 권위 판단 기준 (Authority Model)

> **목적**: "이 동작/값을 **클라가 소유하나, 서버가 소유하나**"를 기능마다 매번 재논쟁하지 않도록 **판단 기준을 박제**한다.
> 새 기능(전투/아이템/스킬/상호작용)을 설계할 때 먼저 §1의 4축에 대입하고, §2(권위 vs 코드 위치)를 떠올린다.

---

## 0. 권위 결정 규칙 (먼저 읽는다 — 위반 시 재설계)

권위(authority)는 **기능마다 명시적으로 결정**한다. 아래를 어기면 사망/HP/판정 같은 핵심에 치팅 구멍이 조용히 생긴다(실제 사례: 플레이어 HP 클라 권위 → C_PlayerDead 미송신 시 불사).

1. **기본값 = 서버 권위.** 코옵/PvP에서 **공유·경쟁되는 상태**(HP·사망·위치·적중·데미지·보상·재화)는 **서버가 소유**한다. 클라 권위는 **(a) 솔로(충돌 대상 0, §2 context)** 이거나 **(b) 순수 연출/입력(③)** 이거나 **(c) 사용자가 명시 승인한 단순화**일 때만 허용한다.
2. **"문서에 적혀 있음" ≠ "결정됨".** 권위를 클라로 두는 선택은 **반드시 사용자 승인 + 결정 출처(날짜/근거)** 를 남긴다. 근거 없이 클라 권위로 두지 않는다. 기존 문서의 클라 권위 항목도 출처가 없으면 *가정*으로 보고 재확인한다.
3. **"클라가 *할 수 있다*" ≠ "클라가 *소유해야 한다*".** PvE라 클라가 계산할 수 있어도, 코옵에서 서버가 이미 그 값의 출처(데미지 발행·판정)라면 **서버 권위가 정합**이다. "할 수 있다"를 권위 근거로 쓰지 않는다.
4. **서버 권위라도 클라 예측은 유지**한다(③ 손맛). 권위 = *충돌 시 누가 이기나*(tiebreaker)일 뿐, 클라가 즉발 연출/예측을 못 한다는 뜻이 아니다(§2).

→ 새 값을 설계할 때: **"코옵/PvP에서 공유·경쟁되나?"** 가 예면 서버 권위가 기본. 클라로 두려면 위 (a)/(b)/(c) 중 하나 + 사용자 승인을 명시한다.

---

## 1. 판단 4축

| 축 | 질문 | 어디가 소유 | 이유 |
|----|------|------------|------|
| **① 치팅 영향** | 클라가 이 값을 조작하면 게임·경제가 붕괴하나? | **서버 권위** | 신뢰 경계는 서버. 조작이 곧 부정 이득이면 클라를 믿지 않는다 |
| **② 일관성** | 모든 플레이어가 **같은 값**을 봐야 하나? | **서버가 진실 + 브로드캐스트** | 각자 계산하면 화면이 갈린다. 공유 대상은 단일 진실원 필요 |
| **③ 반응성** | 지연(RTT)이 손맛·조작감을 해치나? | **클라 즉발**(로컬 연출/예측) | 입력→피드백 사이 왕복은 손맛을 죽인다. 연출은 안 기다린다 |
| **④ 결정론/공유공식** | 양쪽이 **같은 입력 → 같은 출력**을 보장하나? | **공유 코어**(양쪽 각자 계산, 전송 최소) | 같은 함수+데이터면 네트워크로 안 보내도 일치한다 |

**핵심 한 줄**: **수치·판정·보상 = 서버 / 연출·입력 = 클라 / 결정론 가능한 건 공유 코어로 미전송.**

---

## 2. 권위 ≠ 코드 위치 (가장 자주 오해하는 지점)

> **"서버 권위"는 *충돌이 생기는 공유 상황에서 누가 이기느냐*(tiebreaker)의 문제지, *전투 코드가 어디 사느냐*의 문제가 아니다.**
> 클라도 전투 코드를 **가질 수 있고, 이미 가질 준비가 돼 있다** — 그 토대가 **`Shared.Gameplay`**(netstandard, UnityEngine 의존 0).

`HitboxMath`·`GameplayEffectMath`·`SkillTimeline`을 엔진 비의존으로 만든 이유가 바로 이것: **같은 전투 수식을 클라에서도, 서버에서도 돌릴 수 있게**(서버=ProjectReference, 클라=`Plugins/Shared.Gameplay.dll`, 동일 ns).

```
        ┌──────────────── Shared.Gameplay (결정론 코어, 단일 소스) ────────────────┐
        │   HitboxMath.Overlaps · GameplayEffectMath.Aggregate · SkillTimeline ...  │
        └───────▲────────────────────────────────────────────────▲─────────────────┘
        (클라가 실행)                                       (서버가 실행)
   ┌────────────┴───────────┐                    ┌───────────────┴────────────┐
   │ 솔로 PVE: 클라가 권위   │                    │ 코옵: 서버가 권위           │
   │ 로컬 계산 → 즉시 적용   │                    │ 서버 판정 → 브로드캐스트     │
   └────────────────────────┘                    └─────────────────────────────┘
                같은 함수 — 실행 위치와 "권위 주체"만 context에 따라 다르다
```

### context별 권위 (같은 4축, 다른 답)

| 상황 | 그 대상을 누가 공유? | ①치팅·②일관성 축 | 권위 주체 | 데미지 계산 위치 |
|------|----------------------|------------------|-----------|------------------|
| **솔로 PVE** (오픈월드 맛보기) | 나 혼자 | **작동 안 함**(충돌 대상 없음) | **클라** | 클라 로컬(`Shared.Gameplay`) → 즉발 |
| **코옵 던전** | 여러 명이 같은 몬스터 | **작동함** | **서버** | 서버(공유 일관성·치팅 방지) |

→ 솔로는 충돌 대상이 없어 ①② 축이 *죽으니* 클라 권위가 맞고, 코옵은 ①②가 *살아나니* 서버 권위. **모순이 아니라 같은 기준의 context 적용**이다.
(주의: 솔로라도 보상·진행 *영속*은 서버가 검증/기록 — ① 치팅이 살아있는 별개 축.)

---

## 3. 본 프로젝트 매핑 (실제 코드 기준)

현재 던전은 **코옵(SocketServer 상시 연결)** 이라 아래는 코옵 기준. 솔로 PVE(M5)는 §2의 "클라 권위" 열로 읽는다.

| 도메인 | 소유(코옵) | 근거(축) | 코드 위치 |
|--------|-----------|---------|-----------|
| 플레이어 **이동** | 클라 입력 즉발 + 서버 릴레이 | ③ | `C_Move`(원본 timestamp 그대로 릴레이) |
| 플레이어 **자기 HP**(던전 코옵) | **서버 권위** (결정 2026-06-11, §4) | ①② | 🚧 `Actor.Gas` HP 승격 작업 중 — 기존 클라 결정론은 *부채였음* |
| 플레이어 **자기 HP**(Main 솔로) | **클라 권위** | §2 솔로(충돌 0) | 로컬 시뮬 |
| 몬스터 **HP·전멸·사망** | **서버 권위** | ①② | `ActorStore.DamageMonster`·`TryMarkCleared` |
| **적중 판정**(hitbox) | **서버 재계산** + 공유 공식 | ①④ | `CombatHandler` + `HitboxMath.Overlaps` |
| **데미지 수치** | **서버 산정** | ① | `CombatEffectCatalog` |
| **던전 클리어 감지** | **서버 1회 발화** | ①② | `DungeonProgress.TryMarkCleared` → `S_DungeonClear` |
| **보상 산정·지급** | **서버**(GameServer 도메인, 영속) | ① | `DungeonResultConsumer`(B 트랙) |
| **스폰 좌표** | **공유 데이터 + 양쪽 계산**(미전송) | ④ | `SpawnResolver`(서버·클라 미러) |
| **연출**(스윙 애니/HitStop/사운드/데미지텍스트) | **클라 즉발** | ③ | `PlayerCharacterAgent`·`HitStopController`·`DamageNumberView` |

---

## 4. 플레이어 HP 권위 — 결정 이력 (2026-06-11 승격)

**결정(2026-06-11)**: **던전(코옵) 플레이어 HP = 서버 권위**로 승격한다. Main 솔로는 클라 권위 유지(§2 솔로 = 충돌 대상 0).

### 왜 승격하나 (이전 "클라 결정론"은 가정/부채였음)
- 던전에서 **모든 데미지의 출처가 서버**다 — 몬스터→플레이어(`Room.Tick`가 `monster_attack_dmg` 발행)·플레이어→대상(`CombatHandler` hitbox 판정). 서버가 "누가 누구에게 얼마"를 **이미 안다**. 단지 HP에 누적을 안 했을 뿐(`PlayerActor`에 HP 필드 부재였음).
- 기존 "클라 결정론"은 §0-2 위반: **사용자 명시 결정 없이** 문서에 가정으로 박혀 있었다. 결과 = **C_PlayerDead 미송신/`S_ApplyEffect` 무시 시 불사 핵**(①치팅 구멍).
- "PvE라 클라가 *할 수 있다*"는 맞지만 §0-3: 코옵에서 서버가 출처면 **서버 권위가 정합**(몬스터 HP와 대칭, ②일관성). PvP는 서버 권위가 *필수*이고 같은 hitbox 기계를 재사용한다.
- ③ 손맛은 안 깨짐: 클라가 `S_ApplyEffect`를 **즉발 적용(예측)**, 서버 HP가 진실(정정). 권위 = tiebreaker(§2).

### 승격 설계 골자 (구현 시 채움)
- 서버 `Actor.Gas` HP/`MaxHp` 추가 → 서버가 발행하는 데미지를 **자기도 누적**(GameplayEffectMath, 몬스터 HP와 동일) → **서버가 HP≤0 감지 → S_PlayerDead 직접 발화**(C_PlayerDead는 클라 예측 트리거로 격하/제거).
- **회복 동기**: ✅ 구현됨(2.5.1 증분2). `ConsumeItem` gRPC(GameServer 검증·차감) → `PlayerConsumedMessage`(Redis) → SocketServer `PlayerConsumedConsumer`가 `DungeonProgress.ApplyPlayerEffect(+heal)` + `S_ApplyEffect` 브로드캐스트. 회복 **수치 단일소스** = 클라 `ConsumableCatalog` SO 저작 → bake → `ConsumableEffectCatalog`(서버 검증·적용). 교리 = [gas-architecture.md §2.5](gas-architecture.md), 상세 codemap §2.6c.
- **max HP 출처**: 서버가 플레이어 base/max HP를 알아야 함(Progression/스탯 → 던전 입장 시 주입).

### 몬스터 HP는 그대로 서버 권위
몬스터는 여러 플레이어가 공유하는 서버 소유 NPC. ②일관성 + ①치팅 방지가 본질. (플레이어 HP 승격으로 **이제 둘 다 서버 권위 = 일관**.)

---

## 4b. Main 획득 권위 — B-lite 서버 검증 (결정 2026-06-13)

**문제(HP 승격과 같은 핵)**: Main(싱글)은 클라가 몬스터 스폰·킬·드랍을 로컬 소유하고 `GrantItem(itemId, qty)` gRPC 로 영속 보상을 직접 지정한다. 서버 가드 = 인증 + 호출당 수량상한(≤99) + catalog 검증뿐 — **호출 빈도/킬 증명이 없어** 클라가 몬스터 무한 스폰→무한 GrantItem = **무한 파밍(만렙 핵)**. §0-2("문서에 적힘 ≠ 결정됨") + §0-3("할 수 있다 ≠ 소유해야") 위반. (구 결정 "싱글이라 클라 신뢰+가드 수용" = [loot-drop.md §1.4](loot-drop.md) → **본 절로 폐기**.)

**근본 원리**: 클라가 저작한 이벤트("내가 잡았다")는 서버가 그 콘텐츠를 **소유(또는 데이터 보유+검증)**하지 않으면 검증 불가. rate-limit 단독은 파밍 *속도*만 늦춤(반창고).

**결정 = B-lite (서버가 map 데이터 보유 → 클레임 검증, 실시간 AI 는 클라 유지)**:
```
map/spawn 데이터(dungeon + Main) = SO 저작→bake→Shared (데이터 진실원 교리 §2.5, 서버가 보유)
클라(Main): 슬롯 기반 LocalMonster 스폰·렌더(손맛 유지) → 킬 → ClaimKill(mapId, slotId)
서버(GameServer): ① slot ∈ map? ② per-user 쿨다운 경과?(Redis (userId,slotId)→lastClaimedAt)
                 → 서버 권위 DropTableRoll → GrantItemAsync → 타임스탬프 기록.  거부 시 0.
```
- **클라는 보상 내용을 못 정함** — 슬롯만 지목. roll·정원·쿨다운·grant 전부 서버 → 파밍률이 **맵 설계치(정원/쿨다운)로 상한**, 무한 불가.
- **`GrantItem(itemId,qty)` 는 Main 경로에서 제거**(치팅 진입점 봉쇄). 던전 `LootGrantConsumer`(서버권위)는 무관.
- Main 은 여전히 소켓 세션 아님(gRPC) — 서버는 실시간 AI 를 안 돎, **map 데이터 + 클레임 상태만** 보유(부분 서버 상태).
- 안 한 것(YAGNI): Main 서버 권위 풀 시뮬(B-full) = co-op 오픈월드 필요 시. **상세 설계·증분 = [main-spawn-claim.md](main-spawn-claim.md)** (스키마·proto·서버 로직·테스트).

---

## 4c. 스탯 전파 — GameServer가 "합산 결과"를 메시지로, SocketServer는 DB 직접 접근 안 함 (결정 2026-06-14, 2.4)

**문제**: 던전 전투 데미지를 스탯 기반(서버 권위 재계산)으로 승격하려면(2.4) SocketServer 의 `CombatHandler` 가 플레이어 AttackPower/Defense 를 알아야 한다. 스탯은 GameServer 도메인(progression·장비·버프). **SocketServer 가 어디서 스탯을 얻나?**

**결정 = GameServer 가 합산해 게임시작 메시지로 스냅샷 전달. SocketServer 는 PostgreSQL 직접 접근 안 함(애초에 못 함 — EF/Npgsql 참조 0, Redis만).**

```
[GameServer]  progression 조회(Cache-Aside: Redis→PostgreSQL) → LevelTable.StatsAt(level)
              (+미래 장비 modifier + 버프) = 합산 결과
   └─ GameStartRequestedMessage.PlayerInfo { +AttackPower +Defense +MaxHealth } 적재 발행
              │ stream:game:start (Redis Stream, 기존 경로 — 필드만 additive 추가)
              ▼
[SocketServer] GameStartRequestedConsumer → Room.AddPlayer → PlayerActor 에 스탯 세팅
              → CombatHandler 가 그 권위 스탯으로 데미지 재계산(DB·계산로직 불필요)
```

**왜 (탄탄한 근거 2개 — 성능 아님, 초기화 1회뿐이라도 적용):**
1. **데이터 소유/스키마 결합 회피.** `user_progressions` 스키마·EF·마이그레이션은 GameServer 소유. SocketServer 가 직접 읽으면 그 스키마가 의도 안 한 공개 계약이 되어, GameServer 의 마이그레이션([[character-swap-direction]] 등)이 SocketServer 를 조용히 깬다 = 분산 모놀리식. 추가로 SocketServer 는 **인터넷 노출 엣지 서버**라 DB 자격증명 부여 = 공격면 확대.
2. **최종 스탯 = 다단계 합산이라 권위가 하나여야 함(결정적 이유).** 스탯 = `f(Level) + 장비 modifier + 버프`. 장비/버프는 GameServer 소유 도메인(Shared 아님, SocketServer 접근 불가). 둘이 따로 계산하면 갈라짐 = 서버 권위가 막으려는 바로 그 문제. → **원칙: 입력(Level·장비·버프)을 넘기지 말고 "계산된 답(최종 AttackPower)"을 넘긴다.** SocketServer 는 *어떻게* 나왔는지 몰라도 권위 숫자만 적용. (기존에 SpawnIndex·MapId 를 메시지로 받지 직접 계산 안 하는 것과 동형.)

**함정(명시)**: 지금은 스탯=레벨뿐이고 `LevelTable`은 Shared.Infrastructure라 SocketServer 도 참조 가능 → "레벨만 넘기면 SocketServer 가 StatsAt 호출" 이 가능해 *보인다*. 하지만 **3.2 장비가 들어오면 깨진다**(장비 modifier 를 SocketServer 가 못 봄). 레벨-only 로 짜고 3.2 에서 재설계하느니 처음부터 "합산 결과 전달". 

**언제는 DB 직접 읽기가 맞나**: 데이터가 SocketServer 소유일 때(자기 런타임 상태). 소유권이 기준 — progression/장비/버프는 GameServer 소유라 해당 안 됨.

---

## 5. 데미지 숫자(Floating Text) 표시 — context-무관 View + 갈아끼우는 Source

플로팅 텍스트는 **순수 연출(③)** 이라 "숫자 + 위치"만 있으면 된다. 누가 그 숫자를 만들었는지는 몰라도 된다 → **View는 멍청하게(dumb) 두고, 데미지 *이벤트*를 추상화해 Source만 교체**한다.

```
[DamageNumberView]  ← (worldPos, amount, kind) 받아 떠오르며 fade, 오브젝트 풀
        ▲ 구독
  OnDamageDealt(targetInstanceId, worldPos, amount) 이벤트
        ▲ 발행 (Source는 context에 따라 교체 — View 재작성 0)
   ├─ 코옵(지금):   ISocketPacketState가 S_MonsterState의 (이전HP − 새HP) 델타로 발행  ← A안
   └─ 솔로PVE(M5):  로컬 전투 resolver(Shared.Gameplay)가 계산해 발행
```

### 결정: 숫자는 "서버 응답값"(코옵), 연출은 "입력 즉발"
- 코옵에서 데미지 *숫자*는 **서버 응답값**(①②). 시전자도 숫자는 **예측하지 않는다** — 클라는 데미지 *공식*을 (이 context에선) 서버에 위임했기 때문. 연출(스윙·HitStop)은 입력 즉발(③)이라 **시전자는 즉시 손맛**, 숫자만 ~1 RTT 뒤. 그 숫자는 **모두 동일**(②).
- **구현(A안, 현재)**: 클라가 보관 중인 **이전 HP − 새 HP**(`SocketMonsterSnapshot.Hp`)로 데미지를 유도 → `OnDamageDealt` 발행 → View. **패킷 무변경.** 막타는 `S_MonsterDead`라 HP 없음 → **마지막 알던 HP로 근사**(오버킬 손실 허용).
- **승격(B안)**: 정밀 막타·크리티컬·흡혈 표시가 필요해지면 응답에 `Damage`(+`AttackerId`) 명시 필드 추가(공개 계약 변경).
- **솔로 PVE(M5)**: 같은 `DamageNumberView` + 같은 `OnDamageDealt` 계약에 **로컬 resolver Source**만 연결. View·이벤트 재사용, Source만 교체.

---

## 6. 안 하는 것 (과설계 경계 — YAGNI)

- **클라 데미지 예측 + reconcile**(코옵): PvP 아님 → 불필요. 서버 응답을 그대로 표시.
- **fixed-point / 롤백 네트코드**: PvP 전용. 코옵은 서버 권위 + 보간으로 충분.
- **모든 값 서버 왕복**: 연출·이동 입력까지 서버를 기다리면 손맛이 죽는다 → ③에 해당하는 건 클라 즉발 유지.
- **솔로 PVE를 위해 전투 코드를 클라에 따로 또 짜기**: `Shared.Gameplay`가 단일 소스 → 같은 코어를 클라가 실행하면 됨(중복 구현 금지).

---

## 7. 씬/컨텐츠별 서버·클라 역할 매트릭스 (한눈에)

> §3가 "도메인별 권위(코옵 기준)"라면, 여기는 **씬(게임 컨텐츠)별로 GameServer·SocketServer·Client가 각각 뭘 하나**를 박제. 새 기능은 *어느 씬·컨텐츠인지* 먼저 보고 이 표의 행에 끼워 넣는다.

**3주체 한 줄 정의**
- **GameServer** = *DB에 남는 것* — 인증·도메인 CRUD(로비·채팅·인벤토리·진행)·보상 지급. **모든 씬 공통**(영속·검증).
- **SocketServer** = *코옵 실시간 월드 권위* — 이동·전투·몬스터·드랍·줍기 중재. **던전(코옵)에서만**. Title/Main 미관여.
- **Client** = *입력·연출·표시* — 모든 씬. **싱글(Main 오픈월드)에선 로컬 권위**(몬스터·전투·드랍)까지 떠안음.

### ① Title 씬 (인증)
| 컨텐츠 | GameServer | SocketServer | Client |
|---|---|---|---|
| 로그인·회원가입·토큰·자동로그인 | ✅ JWT 발급·검증·세션·Refresh | — | 로그인 UI·토큰 보관·송신 |

### ② Main 씬 (OutGame — 로비/소셜 + 싱글 오픈월드 맛보기)
| 컨텐츠 | GameServer | SocketServer | Client |
|---|---|---|---|
| 방 목록·생성·입장(로비) | ✅ gRPC CRUD + `SubscribeRoom` 스트림 | — | UI·Intent |
| 채팅 | ✅ Redis Streams 중계 | — | UI |
| 인벤토리 조회 | ✅ 영속(`GetInventory`) | — | 표시 |
| 게임시작 → 세션 생성 | ✅ Outbox→세션 IP:Port | (방 생성 수신) | 전이 UI |
| **싱글 몬스터·전투·이동** | — | — | **Client 로컬 권위**(`Shared.Gameplay`) |
| **싱글 드랍 roll·바닥·줍기** | — | — | **Client 로컬** |
| **싱글 아이템 지급(영속)** | ✅ `GrantItem` gRPC(가드: catalog·수량상한) | — | 요청·표시 |
| 진행/Exp 영속 | ✅ 검증·기록 | — | 표시 |

### ③ Dungeon 씬 (InGame — 코옵 실시간)
| 컨텐츠 | GameServer | SocketServer | Client |
|---|---|---|---|
| 플레이어 이동 | — | ✅ 릴레이(원본 ts) | 입력 즉발·원격 보간 |
| 적중·데미지·몬스터 HP·사망 | — | ✅ 서버권위(`HitboxMath`·`DamageMonster`) | 입력·연출·예측 |
| 몬스터 sim(AI·이동) | — | ✅ `RoomTickService`·`MonsterAiMath` | 보간 렌더 |
| 플레이어 자기 HP·사망 | — | 🚧 **서버권위 승격 중**(`Actor.Gas` HP, 데미지 누적, 0 감지→`S_PlayerDead`) — 결정 2026-06-11 §4 | 즉발 예측 + 서버 정정 |
| **드랍 roll·바닥·줍기** | — | ✅ 월드 권위·경쟁중재 | 바닥 렌더·줍기 의도 |
| 클리어/실패 감지 | — | ✅ 1회 발화 | 표시 |
| **보상·아이템 지급(영속)** | ✅ Consumer→Progression/Inventory | (Redis Stream 발행) | 표시 |
| 세션 IP:Port | ✅ 관리 | (자기 주소 advertise) | 접속 |

### 핵심 규칙 (이 표가 강제하는 것)
- **SocketServer는 던전(코옵)에서만.** Title/Main은 SocketServer 미관여 — Main 싱글은 **Client 로컬 + GameServer 지급(gRPC)** 으로 끝낸다(통신 최소).
- **영속(DB에 남는 것)은 어느 씬이든 GameServer.** 단 *호출자*만 다름: 던전=SocketServer가 Stream으로, Main 싱글=Client가 gRPC로.
- **같은 컨텐츠라도 씬에 따라 권위 주체가 바뀐다** — 몬스터/전투/드랍: 던전=SocketServer, Main=Client. (§2 context 적용, 모순 아님.)
- 루트/드랍 상세 = [loot-drop.md](loot-drop.md).

---

## 참고
- 공유 결정론 코어: `Shared.Gameplay`(서버 ProjectReference / 클라 `Plugins/Shared.Gameplay.dll`) — codemap §2.6
- 결정 로그: [codemap.md](codemap.md) §2.7(서버 권위 적중)·§2.9(클리어)·§2.6(SkillTimeline)·§2.3(결정론 스폰)
- Character 아키텍처(두 축·GAS·Driver): [character-architecture.md](character-architecture.md)
- 전투/상태머신 규칙: [.claude/rules/unity-gameplay-state.md](../../.claude/rules/unity-gameplay-state.md)
- 패킷/네트워킹: [packets.md](packets.md), [.claude/rules/networking.md](../../.claude/rules/networking.md)

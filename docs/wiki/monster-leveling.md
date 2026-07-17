# AC-E: 몬스터 레벨링 · 등급 Variant · 드롭 정리 설계

> **계기**: AC-C1c 측정에서 몬스터 피해가 **1,1,2,2,3,5** 로 `max(1,..)` 바닥에 눌린 것을 확인(2026-07-17).
> **결정**(사용자, 2026-07-17): 레벨 = **맵 기본 + 스폰별 override** · Variant = **등급(Normal/Elite/Boss)** · 드롭 = **9마리 전수 + 레벨 스케일 + goblin 제거**.
> 관련 = [combat-diagnostics.md](combat-diagnostics.md) · 진행 = plan.md M5.
>
> **⚠️ 최종형 정정(AC-G, 2026-07-17)** — 이 문서의 "등급 = 배율" 부분은 **폐기됐다**(§3·§4 의 Tier 인자·§4.3 등급배율).
> 등급은 `monsters.json` 의 **분류 필드**(문자열 "Normal"/"Elite"/"Boss")일 뿐 스탯에 곱해지지 않으며,
> 강한 개체는 **변종 ID 를 직접 저작**한다(`leviathan` hp 500 / `leviathan_boss` hp 3000 — 스폰은 monsterId 하나만 본다).
> **레벨 스케일(§2)은 그대로 유효.** 폐기 경위 = plan.md AC-G · codemap.

---

## 0. 한 줄 요약

**세 요청은 한 문제다.** 몬스터에 레벨이 없어 스탯이 고정이고, 플레이어만 선형 성장해 몬스터가 무해해졌다.
레벨을 넣으면 밸런스가 구조적으로 풀리고, 등급·드롭이 그 위에 얹힌다.

---

## 1. 진단 — 측정으로 확인된 구조적 실패

| | 성장 |
|---|---|
| 플레이어 | `HP = 100+20(L-1)` · `AP = 10+3(L-1)` · **`DEF = 5+2(L-1)`** |
| 몬스터 | **없음** — `monsters.json` 의 `maxHp`, `abilities.json` 의 `baseDamage` 가 고정 |

피해 = `max(1, base − DEF)` 이므로:

```
L1  (DEF 5)  : bat 3 · demon 7 · arachnya 9 · gargoyle 17 · leviathan 35 · slam 85   정상
L6  (DEF 15) : bat 1 · demon 1 · arachnya 1 · demon_girl 1 · centaur 5 · slam 75
L19 (DEF 41) : leviathan_attack(40) 조차 1                    ← 일반 공격 전멸
L44 (DEF 91) : slam(90) 도 1                                  ← 전 몬스터가 영원히 1
```

**C1c 실측과 정확히 일치**(내 AP25/DEF18 = L6+방어구 / 상대 AP16/DEF9 = L3 맨몸):
`centaur(20)−18=2` ✓ · `creepy_demon(12)−9=3` ✓ · `arachnya(14)−9=5` ✓.

**드롭도 구멍**: `drop-tables.json` 에 `creepy_demon` 뿐이고, `goblin` 은 `monsters.json` 에 없는 **유령 테이블**. → 9마리 중 7마리가 아무것도 안 떨군다.

---

## 2. 스케일 공식 — 왜 곱셈도 단순가산도 아닌가

**요건**: 레벨이 올라도 각 몬스터의 **역할**이 보존돼야 한다(bat=긁는 피해 / slam=치명타).

| 안 | 식 | 문제 |
|----|-----|------|
| 곱셈 | `base × (1+0.35(L-1))` | **slam L20 = 688 vs 플레이어 HP 480 → 즉사.** base 가 큰 어빌리티에서 폭발 |
| 단순 가산 | `base + 4(L-1)` | bat 은 상대적으로 세지고 slam 은 약해짐 → **모든 몬스터가 중간으로 수렴**(역할 붕괴) |
| **비례 가산** ★ | `base + (2 + 0.2·net₁)(L-1)` | — |

**비례 가산의 유도** — "체감 난이도 불변" = 순피해가 플레이어 HP 에 비례:

```
목표:  net(L) / HP(L) = net(1) / HP(1)          (net = base − DEF)
HP(L) = 100 + 20(L-1) = HP(1)·(1 + 0.2(L-1))
  ⇒  net(L) = net₁ · (1 + 0.2(L-1))
  ⇒  base(L) = net(L) + DEF(L) = net₁(1+0.2(L-1)) + 5 + 2(L-1)
  ⇒  base(L) = base₁ + (2 + 0.2·net₁)(L-1)        ← 증가폭이 net₁ 에 비례
```

검산 (`net₁ = base₁ − 5`):

| 어빌리티 | base₁ | 증가폭/L | L6 base | L6 순피해 | HP 200 대비 | L1 대비 |
|---|---|---|---|---|---|---|
| vampire_bat | 8 | 2.6 | 21 | 6 | 3.0% | 3.0% ✓ |
| arachnya | 14 | 3.8 | 33 | 18 | 9.0% | 9.0% ✓ |
| leviathan_slam | 90 | 19.0 | 185 | 170 | 85% | 85% ✓ |

**모든 몬스터가 자기 역할을 그대로 유지**한다. 증가폭 하한이 2(=플레이어 DEF 성장)라 **바닥에 눌리는 일이 원천적으로 없다**.

> **위치 결정**: `Shared.Infrastructure/Monsters/MonsterLevelScaling.cs`.
> 이 식은 **플레이어 레벨 곡선을 정의상 참조**한다 → 곡선이 있는 `Progression/LevelTable` 옆이 맞다.
> `Shared.Gameplay/StatCombatMath`(순수 산식)와 달리 **테이블 의존**이라 Gameplay 에 두면 역참조가 된다.
>
> ⚠️ **정정(AC-F1)**: 초판은 곡선 상수(DEF 5/+2, HP비 0.2)를 **여기 하드코딩**하고 "곡선 바꾸면 여기도 같이 바꿔라"는
> 주석을 달았다 — 그게 바로 SO 교리가 막으려는 **수동 동기화 함정**이었다.
> 지금은 상수가 하나도 없다: `base(L) = net₁ · HP(L)/HP(1) + DEF(L)` 로 **테이블을 직접 읽는다**(곡선이 비선형이어도 자동 추종).
> ~~등급 배율도 `switch` 하드코딩 → `MonsterScalingCatalog`(SO bake) 로 옮겼다~~ → 그 배율 자체가 **AC-G 에서 폐기**(변종 ID 직접 저작).

**HP 스케일**: 플레이어 AP 가 `10+3(L-1)` = L6 에 2.5배 → 킬 타임 유지하려면 몬스터 HP 도 같은 비율.
`maxHp(L) = maxHp₁ · AP(L)/AP(1) = maxHp₁ · (1 + 0.3(L-1))`

---

## 3. ~~등급 Variant (Normal/Elite/Boss)~~ → **폐기(AC-G)**

> ⚠️ 아래 배율 표는 구현됐다가(AC-F2) **같은 날 접었다.** 배율 방식의 실비용: ① tier enum 을 서버·클라 양쪽에 미러링(드리프트 위험) ② 스폰에 필드 2개(level+tier) ③ "이 몬스터가 왜 센지"를 몬스터 테이블과 배율 테이블 **두 곳에서** 찾아야 함.
> **최종형**: 변종이 각자 ID·스탯을 직접 갖는다(`undead_axemaster` 170 / `undead_axemaster_elite` 340 / `leviathan_boss` 3000). `tier` 는 표시·연출 분기용 분류로만 남았다(보스 체력바·등장 연출 후보). 아래 표는 **당시 기록**.

레벨과 **직교**한다 — 레벨은 "어느 던전 대역인가", 등급은 "그 대역 안에서 얼마나 강한가".

| 등급 | HP | 피해 | Exp | 드롭 |
|---|---|---|---|---|
| Normal | ×1.0 | ×1.0 | ×1.0 | 기본 |
| Elite | ×2.0 | ×1.3 | ×3 | 확률 ×2 + 상위 티어 개방 |
| Boss | ×6.0 | ×1.6 | ×10 | 장비 확정 |

- **HP 를 크게, 피해를 작게** 올린다 — 피해를 크게 올리면 즉사가 되고, HP 를 올리면 "오래 버티는 위협"이 된다(액션 RPG 관례).
- 저작: `MonsterSpawnDef.Tier`(0=Normal). **스폰별**이라 같은 `creepy_demon` 을 잡몹으로도 엘리트로도 쓸 수 있다.

---

## 4. 컴포넌트 배치 · 흐름

> ⚠️ **정정(AC-G)**: 아래 흐름의 `T`(등급) 인자와 `spawn.tier` 는 제거됐다. 현행 시그니처:
> `MonsterLevelScaling.Hp(maxHp₁, L)` · `Damage(base₁, L)` · `Exp(exp₁, L)` · `DropTableCatalog.Roll(id, rng, level)`.
> 등급 강도는 **변종 ID 의 저작값**이 담당하고, `MonsterState.Tier` 는 카탈로그(monsterId 행)에서 읽는 분류다.

```
spawn-layouts.json                     monsters.json           abilities.json        drop-tables.json
  map.monsterLevel  ──┐                  maxHp(base₁)            baseDamage(base₁)     monsterId → drops
  spawn.level (0=맵) ─┼─▶ 유효 L        ExpReward                                     
  spawn.tier        ──┴─▶ 등급 T                                                       
        │                                                                              
        ▼                                                                              
  MonsterLevelScaling (Shared.Infrastructure — 플레이어 곡선 참조)                     
        ├─ Hp(maxHp₁, L, T)                                                            
        ├─ Damage(base₁, L, T)                                                         
        └─ Exp(exp₁, L, T)                                                             
```

### 4.1 스폰 — 레벨은 **스폰 시 1회 확정**

```
Room.SpawnMonsters(defs, bounds, layout)
   │   L = def.Level > 0 ? def.Level : layout.MonsterLevel     ← 스폰 우선, 없으면 맵 기본
   │   T = def.Tier
   ▼
MonsterState { Level=L, Tier=T, MaxHp=Scaling.Hp(def.MaxHp, L, T), Hp=MaxHp }
```

> 매 틱 재계산하지 않는다 — 스탯은 스폰 순간의 값이 진실. (레벨업하는 몬스터는 없다.)

### 4.2 피해 — 기존 산식은 그대로, **입력만 스케일**

```
Room.TickMonsters
   └─▶ StatCombatMath.MeleeDamage( Scaling.Damage(ability.BaseDamage, m.Level, m.Tier), 0, target.Defense )
                                   └────────── 여기만 바뀐다 ──────────┘
```

`StatCombatMath` 는 **손대지 않는다** — 산식(`max(1, base+AP−DEF)`)은 옳고, 틀린 건 base 였다.

### 4.3 드롭 — 레벨·등급이 롤에 들어간다

```
CombatHandler.SpawnDrops(room, monster)
   └─▶ DropTable.Roll(monsterId, monster.Level, monster.Tier)
         ├─ gold  수량 × (1 + 0.2(L-1))          ← 레벨 비례
         ├─ 확률  × 등급배율
         └─ Boss  → 장비 확정
```

---

## 5. 드롭 저작 방침 (9마리 전수)

강도 순으로 역할을 나눈다. **약한 몬스터 = 소모품/골드 위주, 강한 몬스터 = 장비 확률↑**.

| 몬스터 | base | 역할 | 드롭 방향 |
|---|---|---|---|
| vampire_bat | 8 | 잡몹 | gold 소액 · 포션 저확률 |
| creepy_demon | 12 | 잡몹 | gold · 포션 · 초급 장비 저확률 (기존 유지) |
| arachnya | 14 | 잡몹 | gold · 포션 |
| demon_girl | 16 | 중급 | gold · 포션 · 액세서리 저확률 |
| wild_centaur | 20 | 중급 | gold · 방어구 |
| gargoyle | 22 | 중급 | gold · 방어구/방패 |
| undead_axemaster | 28 | 상급 | gold 다량 · 무기 |
| leviathan | 40/90 | 보스 | 장비 확정 + 액세서리 |
| test_brute | 9999 | **테스트 전용** | 드롭 없음(테스트 픽스처 오염 방지) |

**`goblin` 테이블 삭제** — `monsters.json` 에 없는 유령이라 롤이 절대 일어나지 않는다.

> ✅ **저작 완료(E5) + AC-G 반영**: 8마리 전수 + `goblin` 유령 제거 완료(`test_brute` 는 픽스처라 의도적 제외). **변종(`*_elite`·`leviathan_boss`)은 각자 자기 ID 의 드롭 테이블**을 갖는다 — 등급 확률 배율이 없으므로 테이블이 없으면 아무것도 안 떨군다.

---

## 6. 증분 계획

| # | 증분 | 검증 |
|---|------|------|
| **E1** ✅ | `MonsterLevelScaling`(Shared.Infrastructure) + 단위테스트 — **코드만, 배선 없음** | 단위(역할 보존·바닥 없음·경계) |
| **E2** ✅ | `MonsterSpawnDef.Level/Tier` + `MapSpawnLayout.MonsterLevel` 저작 + 스폰 시 확정 | 단위(맵 기본/override) · 기존 테스트 무변경 확인 |
| **E3** ✅ | 피해·HP·Exp 배선(`Room.TickMonsters`·`SpawnMonsters`) | 단위(레벨별 피해) · **Docker E2E** |
| **E4** ✅ | 드롭 9마리 전수 + 레벨/등급 롤 + goblin 제거 | 단위(롤 분포·유령 부재) |
| **E5** ✅ | 클라 SO 저작(`MonsterCatalogDefinition`·spawn) + Export 왕복 | Unity 컴파일 · EditMode |

> ✅ **전 증분 완료(2026-07-17)** + 후속 **AC-F**(상수 하드코딩 제거·던전 5개 L1→L30)·**AC-G**(등급→ID) — plan.md 참조.

- **E1 을 먼저** 한다 — 순수함수라 리스크 0 이고, 나머지가 전부 여기 의존한다.
- 각 증분은 **그 자체로 동작 보존**(codemap §2.62 교훈 — 데이터 선반영이 증분 경계를 깨뜨린 사례).

---

## 7. 안 하는 것 (YAGNI)

- **레벨대별 별도 프리팹/외형** — 에셋 작업이 크고, 등급 배율로 충분히 체감된다. 필요해지면 확장점.
- **동적 레벨 스케일링**(플레이어 레벨 추종) — 던전 대역이 의미를 잃는다. 콘텐츠 설계와 충돌.
- **몬스터 AttackPower 스탯** — 지금은 `base` 가 곧 공격력. 스탯을 나눌 이유가 아직 없다.

# Actor 통합 전투 · 전투 진단 · 몬스터 레벨링/밸런스

`feat/actor-combat-ability-so` → `main` · **23 커밋** · 218 files (+12,736 / −1,253)

---

## 왜 이 PR인가

시작은 "몬스터 공격 모션이 안 나온다"였고, 끝은 **"L19부터 모든 몬스터가 1 데미지"라는 구조적 밸런스 실패를 측정으로 찾아 고친 것**이다.

중간에 **측정 도구를 먼저 만들어**(AC-C) 체감을 데이터로 바꿨고, 그 데이터가 *"틱레이트는 건드리지 마라"* 와 *"진짜 문제는 몬스터에 레벨이 없는 것"* 을 알려줬다.

---

## 1. AC / AC-B — Actor 통합 + Ability SO 단일 저작

플레이어와 몬스터를 **ActorId 하나로 통합**했다(양수=플레이어 / 음수=몬스터 / 0=환경).

- **발동 파이프 통합**: `S_AbilityActivated{ActorId, SkillId}` 한 패킷으로 플레이어·몬스터 연출을 흡수.
- **하드코딩 switch 제거**: `int → 어빌리티` 매핑이 **데이터**(`AbilityDefinition.networkId`)로 이동 → **스킬 추가에 서버 코드 수정이 필요 없다**.
- **데미지 출처 일원화**: 수치는 effect가 아니라 `ability.baseDamage`가 소유. effect는 CC 전용 라벨로 축소.
- **보스 다중 스킬 실증**: 몬스터 하나에 어빌리티 2개 저작 → **코드 변경 0**.

**의도적으로 통합하지 않은 것**: 생명주기(스폰/디스폰). 이 결정이 나중에 서버 분리(Monster Server / Dungeon Server)의 seam이 된다 — 설계 `docs/wiki/actor-combat-architecture.md` §2.4·§9.

## 2. AC-C — 전투 진단, 그리고 "고치지 않기로" 한 결정

> 계기: *"던전에서 체력 동기화가 살짝씩 느린 것 같다"*

**체감은 증상이지 원인이 아니다** → 먼저 측정 도구를 만들었다.

| | 내용 |
|---|---|
| **C1a** 서버 트레이스 | `[CombatTrace]` — path·formula·base/AP/DEF·final·gate. **기본 Off** |
| **C1b** 클라 링버퍼 | `CombatTraceRecorder`(512→4096, 구조체·무할당) + `CombatTraceJoin`(스윙 병합) |
| **C1b'** 에디터 창 | `Tools/Combat/Combat Trace` — 요약 3탭·이벤트 목록·상세·CSV |
| **C1c** 측정 세션 | 실제 플레이 + CSV |

### 코드 리딩으로 찾은 결함 2건 (측정 전에 고칠 근거가 충분했다)

- **D1 송신 직렬화 없음** → **AC-C2**: 세션당 `Channel`(Bounded 1024) + 단일 소비자 `SendLoop`.
- **D2 dirty-flag 스테일 고착** → **AC-C3**: `S_MonsterState.Seq` + 클라 스테일 드롭. (+ hotfix로 즉시 봉합)

### 측정 결과 — C2b 불필요 판정

```
송신→HP 반영  avg 37ms (max 50)     ← 체감의 본체는 RTT(39ms)
발동→HP 반영  avg 14ms              ← 서버 처리·하행은 이미 촘촘
스테일 드롭   8마리 전부 0           ← C2 의 FIFO 가 역전을 원천 제거
gate 거부     0건
```

**틱레이트·클라 예측(C2b)을 건드릴 근거가 없다.** 설계 문서에 *"C1c 결과 없이 C2b에 손대지 않는다"*고 못박아둔 게 정확히 이 결과를 위한 것이었다 — 측정 없이 갔으면 멀쩡한 틱레이트를 만졌을 것이다.

**데미지 검수도 통과**: `max(1, base+AP−DEF)` 역산이 콤보 3단계 모두 일관(AP=25/16), 누적피해 = 스윙별 final 합 완전 일치.

## 3. AC-E/F/G — 몬스터 레벨링 · 밸런스 · 드롭

### 측정이 드러낸 구조적 실패

```
플레이어:  DEF = 5+2(L-1)  ← 선형 성장
몬스터:    base 고정        ← 성장 없음
                ▼
L19 (DEF 41): leviathan_attack(40) 조차 1 데미지
L44 (DEF 91): 전 몬스터가 영원히 1
```

C1c 실측(1,1,2,2,3,5)과 정확히 일치했다.

### 해결 — 스케일 공식

```
목표:  net(L)/HP(L) = net₁/HP(1)          ← 체감 난이도 불변
  ⇒  base(L)  = net₁ · HP(L)/HP(1) + DEF(L)
     maxHp(L) = maxHp₁ · AP(L)/AP(1)
```

**상수가 0개다** — `LevelTable`(이미 SO 저작)을 직접 읽어 곡선이 비선형으로 바뀌어도 자동 추종.

대안을 왜 버렸나:
- **곱셈** — slam(90)이 L20에 **688** vs 플레이어 HP 480 → 즉사
- **단순 가산** — 약한 몬스터는 세지고 강한 몬스터는 약해져 **전부 중간으로 수렴**(역할 붕괴)

### 던전 5개 진행 곡선

| 던전 | 레벨 | Exp | 구성 |
|---|---|---|---|
| dungeon_01 슬라임 동굴 | L1 | 100 | Normal ×8 |
| dungeon_02 슬라임 소굴 | L6 | 300 | Normal ×10 + Boss ×1 |
| dungeon_03 폐허 성채 | L12 | 700 | Normal ×7 + Elite ×1 |
| dungeon_04 심연의 회랑 | L20 | 1500 | Normal ×4 + Elite ×2 + Boss ×1 |
| dungeon_05 용의 둥지 | L30 | 3000 | Elite ×3 + Boss ×2 (잡몹 없음) |

### 등급 = ID (AC-G, 리뷰 지적 반영)

AC-F2에서 만든 배율 테이블을 **하루 만에 접었다.** 배율 방식은 ① enum을 서버·클라 미러링 ② 스폰에 필드 2개 ③ "왜 센지"를 두 곳에서 찾기를 요구했다.

```
[전]  spawn{ monsterId:"leviathan", tier:2 } + monster-scaling.json{ tier:2, hp×6 }
[후]  monsters.json{ monsterId:"leviathan_boss", tier:"Boss", maxHp:3000 }
      spawn{ monsterId:"leviathan_boss" }        ← ID 하나만
```

`tier`는 남았지만 **분류일 뿐 스탯에 곱해지지 않는다**(표시·연출 분기용).

### 드롭

9마리 중 **7마리가 아무것도 안 떨구고** 있었고, `goblin`은 `monsters.json`에 없는 **유령 테이블**이었다. → 전수 저작 + 유령 제거 + 변종별 테이블. `test_brute`는 픽스처라 의도적 제외.

## 4. AC-H — Main 몬스터 체력바

던전(서버 권위)과 Main(클라 권위)은 HP 권위가 달라 합칠 수 없다. 체력바는 **"누가 권위인가"를 알 필요가 없으므로** `IMonsterHealth` 계약만 보게 했다 → 컴포넌트 하나를 양쪽 프리팹에 공용.

## 5. 인프라 수정 (작업 중 발견)

- **stale-image guard 영구 오탐** — `Shared` 전체를 gameserver 의존으로 봤는데 `Shared.Packet`은 SocketServer 전용. csproj ProjectReference 폐포에서 유도하도록 교체 + `*.json` 필터 추가(abilities.json 같은 임베드 카탈로그를 놓치던 false negative).
- **화석 csproj 7개 삭제** — asmdef 없는 05-30 유물. Unity는 asmdef의 **내부 `name`**으로 csproj를 만든다(파일명 아님).
- **죽은 클라 검증 명령 교정** — `dotnet build Client\Game.Main.csproj`가 asmdef 재편 이후 계속 깨져 있었다. `dotnet`은 이 프로젝트의 클라 검증에 구조적으로 부적합(Unity 패키지 소스가 다른 컴파일러 설정에서 터지고, sln은 MSB5004) → **Unity가 유일한 권위**로 CLAUDE.md 교정.
- **테스트 병렬 플래키** — `CombatTrace`가 static이라 클래스 병렬 실행 시 남의 로그가 섞였다. 어셈블리 단위 병렬 해제(전체 90ms라 비용 0).

---

## 검증

| | 결과 |
|---|---|
| SocketServer.Tests | **209/209** |
| Shared.Gameplay.Tests | **50/50** |
| 솔루션 빌드 | **0 오류** |
| Unity 컴파일 | **0 오류** |
| EditMode | **192/192** |
| PlayMode (anim · Main 체력바) | **3/3 · 3/3** |
| Docker E2E `SocketE2ETests` | **31/31** |

### 회귀 테스트를 실측으로 검증했다

"통과하지만 못 잡는 테스트"를 만든 적이 있어(C3-hotfix), 이후 **수정을 임시로 빼서 실패를 확인**하는 절차를 붙였다:

- AC-C3 → `Expected: 18, But was: 30` (= D2 증상 그 자체)
- 사망 체력바 → `Expected: 0, But was: 12`
- 모든 액터 트레이스 → `Expected: 2, But was: 1` (= "내 스윙만 보임")
- AC-E3 → 스케일 우회 시 3건 실패, `L1은_저작값_그대로다`만 통과(항등이라 정상)

**AC-G에선 테스트가 내 저작 실수를 잡았다** — `leviathan` base를 65로 착각해(그건 arachnya) boss를 390으로 적어 **원본(500)보다 약한 보스**가 됐다. 데이터 저작에도 계약 테스트가 필요하다는 사례.

---

## 알려진 한계 / 후속

- **`dungeon_03`~`05`에 `visualPrefab` 없음** — 맵 배경 에셋 작업 필요. 지금은 전투만 가능.
- **`tier` 연출 미사용** — 분류는 저장되지만 클라가 아직 읽지 않는다(보스 체력바·등장 연출 후보).
- **몬스터→플레이어 지연 관측 불가** — 플레이어 HP 반영 시점을 기록하지 않는다(C1c 후속).
- **AC-D2** — 플레이어→플레이어만 산식을 경유하지 않는다(`flat`). 트레이스에 그대로 드러나므로 밸런스 결정만 남음.
- **GameServer의 `Logging:LogLevel`도 죽은 설정** — Serilog를 코드에서만 구성해 설정·환경변수로 레벨 조정이 불가. 부트스트랩 구조 변경이 필요해 미수정.

## 설계 문서

- `docs/wiki/actor-combat-architecture.md` — Actor 통합, 6축 통합 맵, 서버 분리 seam
- `docs/wiki/ability-so-authoring.md` — Ability SO 단일 저작
- `docs/wiki/combat-diagnostics.md` — 진단 2축, D1/D2, 측정 결과
- `docs/wiki/monster-leveling.md` — 스케일 공식 유도, 등급, 드롭 방침
- `docs/wiki/codemap.md` §2.62~§2.75 — 결정 로그

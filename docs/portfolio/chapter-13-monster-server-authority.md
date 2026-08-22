# 13. 몬스터 — 서버가 소유하는 것, 클라가 그리는 것

> **한 줄** — 몬스터는 "누구의 것도 아닌 객체"라 소유자를 정해야 한다. **서버가 시뮬하고 클라는 보간만** 하도록 나눴고, 그 과정에서 매니저를 방마다 만들지 않기 위해 **틱 서비스 하나**로 모았다. 클라 보간은 정석(Entity Interpolation)의 **단순화본**이며, 그 사실과 승격 경로를 명시해 둔다.
>
> **범위** NPC 권위 · 단일 틱 루프 · 저작-런타임 분리 · 원격 엔티티 보간 · 양방향 전투
> **검증** `MonsterAiMathTests` · `MonsterSpawnLayoutTests` · SocketServer 단위 + 2인 시각 검증

---

## 1. 권위를 어디까지 서버로 가져올까

이 시점의 플레이어 HP는 **클라 결정론**이었다. 몬스터도 같은 방식으로 클라가 시뮬하면 통일성은 있지만 두 가지가 무너진다.

- **일관성** — 클라마다 다른 몬스터를 본다. 내 화면에서는 죽었는데 상대 화면에서는 살아 있다.
- **치팅 표면** — 몬스터 HP·위치를 클라가 계산하면 조작 지점이 클라 수만큼 생긴다.

그래서 몬스터부터 서버 권위로 올렸다.

| 대상 | HP 권위 (이 시점) | 이동 |
|---|---|---|
| 플레이어 | 클라 결정론 | 클라 입력 → 서버 릴레이 |
| **몬스터** | **서버 권위** | **서버 시뮬 + 클라 보간** |

몬스터는 **서버가 소유한 객체**다. 입력을 넣는 사람이 없으므로 권위를 나눌 이유 자체가 없다. 클라에는 **몬스터 AI도 물리도 두지 않는다.**

> **이 비대칭은 목적지가 아니라 단계였다** — 이후 부채로 드러났다. 던전에서 **모든 데미지의 출처가 서버인데 HP만 클라가 가지면 불사 핵**이 가능하다(`C_PlayerDead`를 안 보내거나 `S_ApplyEffect`를 무시하면 된다). 그래서 던전 플레이어 HP도 서버 권위로 승격했다 — 서버가 데미지를 누적해 HP 0을 직접 감지하고 `S_PlayerDead`를 발행한다. → [21](./chapter-21-connection-liveness-hp-authority.md) · 정본 [authority-model §4](../wiki/authority-model.md)
>
> 같은 구멍이 나중에 **아군 오사** 형태로 한 번 더 나타난다 — 클라 HP만 깎고 서버는 죽음을 모르던 경로([29](./chapter-29-multiplayer-sync-invisible-failures.md) 3절).

## 2. 매니저를 방마다 만들지 않는다

가장 흔한 설계는 `Room`마다 `MonsterManager`를 붙이는 것이다. 방이 수십 개면 매니저도 수십 개다.

```
RoomTickService (BackgroundService, 고정 10Hz)   ← 단 하나
   └ 모든 Room 순회
        └ Room.TickMonsters(dt, nowMs)            ← 상태는 Room 이 이미 갖고 있다
             └ MonsterAiMath.Step(...)            ← 수식만 순수 함수로 분리
```

판단 기준은 **"새 상태를 만드는가"** 였다.

- **상태**는 이미 `Room`에 있다(플레이어 상태와 같은 자리, [08](./chapter-08-socket-movement.md) 2절). 매니저를 만들면 상태가 두 곳으로 갈라진다.
- **필요한 것은 스케줄링뿐**이고, 스케줄링은 한 곳에 모을수록 좋다 — 틱 주기·스레드·예외 처리가 한 군데다.
- **수식만 순수 함수로 뗐다** — `MonsterAiMath.Step`은 상태와 플레이어 배열을 받아 다음 상태를 반환한다. Idle/Patrol/Chase/Attack 분기를 서버를 띄우지 않고 단위 테스트할 수 있다.

```csharp
// RoomTickService.cs:17
private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);  // 10Hz
```

> **왜 10Hz인가** — 나중에 "전투가 느린 것 같다"는 체감이 생겼을 때 이 값을 의심했다. 계측해 보니 **틱레이트는 원인이 아니었고**, 진짜 원인은 다른 두 곳이었다. 측정 없이 상수를 바꾸지 않은 판단 과정은 [26](./chapter-26-measured-combat-cleanup.md)에 있다.

## 3. 저작과 런타임을 가른다

```
[저작]   Map Editor 씬에 마커 배치
           MonsterSpawnMarker / PatrolPointMarker / MapBoundsMarker
             │  SaveAndExport
             ▼
         spawn-layouts.json         ← 데이터가 진실원
             │
[런타임]  서버: 파싱 → MapSpawnLayout(Bounds, Monsters[Patrol])
         클라: 읽지 않는다 — 받은 위치에 인스턴스화 + 보간만
```

**클라가 레이아웃을 아예 모른다**는 게 요점이다. 스폰 위치를 클라도 안다면 "서버가 안 보낸 몬스터"를 클라가 만들 수 있고, 그러면 1절의 권위가 무너진다. 클라는 **서버가 말해준 것만 존재한다고 믿는다.**

## 4. 몬스터가 맵 밖으로 새는 문제 — 매 틱 Clamp

추격 중에 플레이어를 따라 절벽이나 벽 밖으로 나가는 문제는 AI 분기마다 막으면 반드시 빈틈이 생긴다. 대신 **출구를 하나로 만들어 그 지점에서 한 번 잡았다.**

```csharp
var next = Step(state, players, dt);
next.Position = bounds.Clamp(next.Position);   // 어떤 분기로 왔든 여기를 통과한다
```

경계는 `MapBounds`(center/size)로 저작된다. 순수 함수 안에 있으므로 **"이 입력이면 항상 이 위치"** 가 성립하고, 경계 행동까지 단위 테스트로 고정된다.

> 이건 "검증을 분기마다 넣지 말고 합류 지점에 넣는다"는 패턴이다. 같은 판단이 나중에 퀘스트 진행([19](./chapter-19-quest-system.md))과 루팅 지급([15](./chapter-15-loot-drop-inventory.md))에서도 반복된다 — **훅은 funnel에 건다.**

## 5. 원격 엔티티를 어떻게 그리나 — Entity Interpolation

원격 플레이어와 몬스터는 클라가 **소유하지 않는다**. 그래서 입력도 물리도 AI도 두지 않고 **받은 스냅샷을 부드럽게 따라가기만** 한다. 이 기법의 이름이 **Entity Interpolation**이다.

```
넷코드 3대 기법
  Client-Side Prediction   내 캐릭터를 즉시 움직인다
  Server Reconciliation    서버 응답으로 내 예측을 보정한다
  Entity Interpolation     남의 캐릭터·NPC 를 보간해 그린다   ← 여기
```

원격 플레이어와 몬스터는 본질이 같다 — **둘 다 "남이 만든 상태"** 다. 그래서 같은 구현을 공유한다.

```
S_Spawn*        → 엔티티 인스턴스화
S_*State/Move   → 보간 타깃 갱신
S_*Dead/Left    → 디스폰
```

### 현재 구현은 정석의 단순화본이다 (그리고 그걸 알고 쓴다)

정석은 스냅샷을 **버퍼에 쌓고 "약간 과거" 시점을 두 스냅샷 사이에서 시간 비율로 보간**한다. 현재는 **최신 스냅샷 하나를 향해 지수 감쇠**하는 lerp-to-latest다.

| | 정석 Snapshot Interpolation | 현재 구현 |
|---|---|---|
| 스냅샷 보관 | 타임스탬프된 N개 버퍼 | **최신 1개** |
| 렌더 시점 | 고정 지연(예: `cl_interp 0.1s`) 만큼 과거 | 지연 없음 |
| 보간 방식 | 두 스냅샷 사이 시간 비율 | 목표값으로 지수 감쇠 |
| 패킷 손실 | 2개 보유로 견딤 / 짧게 외삽 | 다음 스냅샷까지 마지막 위치로 수렴 |

**트레이드오프** — lerp-to-latest는 코드가 짧고 상태를 안 들지만, 패킷 간격과 프레임레이트에 의존해 지터가 생기면 고무줄(rubber-band)처럼 보이고 시간적으로 부정확하다. **저속 Co-op의 시각용으로는 충분**하고, 빠른 PvP나 정밀 동기화로 가면 버퍼 기반이 정석이다.

### 승격 경로 (필요해지면)

```
서버 ──snapshot(t, pos, rot)──▶ SnapshotBuffer  [t0]─[t1]─[t2]─[t3]

매 프레임:
  renderTime = now - interpolationDelay        // 예: -100ms "과거를 그린다"
  renderTime 을 감싸는 두 스냅샷 A,B 선택
  alpha = (renderTime - A.t) / (B.t - A.t)
  pos   = Lerp(A, B, alpha)                     // 시간 비율 정확 보간
  소모된 스냅샷 제거; 버퍼가 마르면 짧게 외삽 후 정지
```

- **고정 지연이 지터를 흡수한다.** 대신 그만큼 원격을 과거로 그린다 — 크면 굼뜨고 작으면 끊긴다(튜닝 대상).
- **토대는 이미 깔려 있다** — [08](./chapter-08-socket-movement.md)에서 이동 패킷의 타임스탬프를 서버가 덮어쓰지 않고 **클라 원본 그대로 릴레이**하기로 한 것이 이것 때문이다. 스냅샷에 원본 시각이 살아 있으므로, 그 값을 쓰기 시작하면 정식 보간으로 올라간다.
- ⚠️ **구분** — 이 버퍼는 *원격 표현(렌더)* 용이다. 피격 판정을 과거로 되감는 **Lag Compensation**은 *서버 측 위치 히스토리*라는 별개 기록이 필요하다.

**출처** — [Gabriel Gambetta, Entity Interpolation](https://www.gabrielgambetta.com/entity-interpolation.html) · [Valve, Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) · [Gaffer On Games, Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/)

## 6. 양방향 전투 — 판정은 서버, 표현은 클라

```
플레이어 → 몬스터
  C_Attack(트리거) → CombatHandler 가 시전자 위치·yaw 로 hitbox 재판정(권위)
     → Room.DamageMonster (GameplayEffect 로 HP 차감)
     → HP ≤ 0 : 제거 + S_MonsterDead / 그 외 : S_MonsterState

몬스터 → 플레이어
  MonsterAiMath.Step 이 aggro 타깃을 반환
     → Attack 페이즈 + 쿨다운 경과 시
     → 최근접 플레이어에게 S_ApplyEffect{monster_attack_dmg}
```

**양쪽 데미지를 모두 GameplayEffect로 표현**한 것이 중요하다. "플레이어가 때리는 것"과 "몬스터가 때리는 것"을 다른 코드로 만들면 밸런스 수식이 두 벌이 된다. 하나의 모델로 통일해 두면 나중에 공격력·방어력·레벨 스케일링을 **한 곳에 넣는 것으로 양쪽에 적용**된다.

## 7. 그 이후 — 이 챕터의 TODO는 어떻게 됐나

| 당시 TODO | 결말 |
|---|---|
| 데미지 산식 GAS 스탯화(고정값 → 공격력/방어력) | ✅ `AttackPower`/`Defense` Attribute + Effect 카테고리로 이관 |
| 원격 피격자 HP 라우팅 | ✅ 플레이어 HP 서버 권위 승격으로 흡수([21](./chapter-21-connection-liveness-hp-authority.md)) |
| 보스/특수 몬스터 | ✅ `leviathan_boss` — 카탈로그·드롭테이블·레벨 스케일링에 존재 |
| (당시 없던 축) 몬스터 레벨링 | ✅ `MonsterLevelScaling` + `MonsterLevelDamageTests`([26](./chapter-26-measured-combat-cleanup.md)) |
| 몬스터 웨이브/스폰 페이즈 | ❌ **미착수** — 데이터에 `wave` 필드는 있으나 wave 0만 스폰. [plan §4.1.6](../wiki/plan.md) |
| 원격 보간 버퍼 승격 | ❌ 미착수 — 여전히 lerp-to-latest (5절 그대로) |

> 이 챕터를 만들며 부딪힌 인프라 버그 하나(컨테이너 재시작 직후 Redis `LOADING`에 컨슈머가 영구 사망)는 개별 수정 대신 **복원력 중앙화**로 해결했다 → [05](./chapter-05-game-start-e2e.md) 7절.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 상태는 방에, 스케줄링은 하나 | 부활·유예 정리·전투 전부가 같은 틱 위에서 동작([24](./chapter-24-coop-revive.md)) |
| 수식만 순수 함수로 | 서버·클라 공유 결정론 코어(`Shared.Gameplay`)의 원형 |
| 검증은 합류 지점에 | 퀘스트 훅·루팅 지급의 funnel 설계([19](./chapter-19-quest-system.md)·[15](./chapter-15-loot-drop-inventory.md)) |
| 데미지는 전부 GameplayEffect | 레벨 스케일링을 한 곳에 넣어 양방향 적용([26](./chapter-26-measured-combat-cleanup.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-13-monster-server-authority.md](../learning-log/chapter-13-monster-server-authority.md)

# 챕터 13 학습 로그 — 몬스터 (서버 권위 NPC + AI 틱 + 클라 보간)

> M3. 서버가 소유하는 NPC(몬스터)의 스폰·이동(AI)·양방향 전투·클라 렌더.

## 처음 알았던 것 vs 피드백으로 수정된 것

### 몬스터 HP를 어디서 소유할까 — 의도된 비대칭

**내가 처음 생각한 것:**
플레이어 HP가 클라 결정론이니, 몬스터도 통일성 있게 클라가 시뮬레이션하면 되지 않을까?

**피드백:**
몬스터는 **서버 소유 NPC**다. 클라가 몬스터 HP/위치를 시뮬하면 클라마다 다른 몬스터를 보게 되고(일관성 붕괴), 치팅 표면도 커진다.

**올바른 설계 — 의도된 비대칭:**

| 대상 | HP 권위 | 이동 |
|------|---------|------|
| 플레이어 | 클라 결정론(기존 유지) | 클라 입력 → 서버 릴레이 |
| 몬스터 | **서버 권위** | **서버 시뮬 + 클라 보간** |

몬스터는 "서버가 소유한 객체"라 권위를 서버로 일원화하는 게 자연스럽다. 클라엔 **몬스터 AI도 물리도 없다** — 받은 스냅샷을 보간만 한다.

---

### 방마다 MonsterManager를 둘 것인가 — 단일 틱 서비스

**내가 처음 만들려던 것:**
`Room`마다 `MonsterManager`를 붙여 각자 AI를 돌린다.

**피드백:**
방이 수십 개면 매니저도 수십 개. 과분리다. 게다가 몬스터 상태는 이미 방에 속한 데이터다.

**올바른 설계:**
```
RoomTickService (BackgroundService, 10Hz)   ← 단 하나
   └ 모든 Room 순회
        └ Room.TickMonsters(dt, nowMs)       ← 몬스터 상태는 Room이 동거
             └ MonsterAiMath.Step(...)        ← AI '수식'만 순수 함수로 분리
```

- **상태(데이터)** 는 `Room`에 둔다 — 매니저 신설 안 함.
- **수식(로직)** 만 순수 `MonsterAiMath`로 떼어 **단위 테스트** 가능하게 한다(Idle/Patrol/Chase/Attack 분기).
- 단일 틱 서비스라 스레드/스케줄링이 한 곳에 모인다.

---

### 스폰/패트롤/맵경계를 코드에 박을까 — 저작-런타임 분리

**내가 처음 생각한 것:**
몬스터 스폰 좌표·순찰 경로를 서버 코드에 상수로 박는다.

**올바른 설계 — 데이터 저작:**
```
[저작]    Map Editor 씬에 마커 드래그
            MonsterSpawnMarker / PatrolPointMarker / MapBoundsMarker
              │  SaveAndExport (write-back)
              ▼
          spawn-layouts.json  (클라/서버 양본 — 동일 데이터)
              │
[런타임]   서버: SpawnLayoutTable.Parse(Stream) → MapSpawnLayout(Bounds, Monsters[Patrol])
          클라: 미사용 (받은 위치에 인스턴스+보간만)
```

기획 데이터를 코드에서 분리 → 디자이너가 씬에서 마커로 저작. 서버는 파싱만, 클라는 런타임에 레이아웃을 안 본다(받은 스냅샷만 보간).

---

### 몬스터가 맵 밖으로 새어나가는 문제 — 매 틱 Clamp

**증상(예상):**
Chase 중 플레이어를 쫓다 절벽/벽 밖으로 나가버림.

**해결:**
`MonsterAiMath.Step` 결과를 **매 틱 `bounds.Clamp`** 한다. 경계는 `MapBounds`(center/size)로 저작.

```csharp
// 순수 함수 안에서 이동 후 항상 경계로 되돌림
var next = Step(state, players, dt);
next.Position = bounds.Clamp(next.Position);  // 무경계면 가드로 통과
```

순수 함수라 "이 입력이면 항상 이 위치" — 단위 테스트로 경계 행동까지 고정.

---

### 클라는 몬스터를 어떻게 그릴까 — RemoteDriver 패턴 재사용

원격 **플레이어**를 그리던 `RemoteDriver`(스냅샷 → transform 보간, FSM/Motor 없음)와 몬스터는 본질이 같다: **남이 만든 상태를 보간만 한다.**

```
S_SpawnMonster → MonsterSpawner가 MonsterEntity 인스턴스화
S_MonsterState → ISocketPacketState.OnMonsterMoved → MonsterEntity 보간 타깃 갱신
S_MonsterDead  → 디스폰
```

`MonsterEntity`는 `RemoteDriver`류(Lerp 보간) — 클라엔 AI/물리가 없다는 설계를 코드로 강제.

---

### 양방향 전투 — 누가 무엇을 판정하나

```
플레이어 → 몬스터
   C_Attack(트리거) → CombatHandler가 시전자 위치/yaw로 hitbox 재판정(권위)
      → Room.DamageMonster (CombatEffectCatalog → GameplayEffectMath.Aggregate로 HP 차감)
      → HP 0 이하: 제거 + S_MonsterDead / 그 외: S_MonsterState

몬스터 → 플레이어
   MonsterAiMath.Step이 aggro 타깃 인덱스 반환
      → Attack 페이즈 + 쿨다운(AttackCooldownMs) 경과 시
      → 최근접 플레이어에 S_ApplyEffect{monster_attack_dmg} 발행 (RoomTickService가 nowMs 전달)
```

양쪽 모두 **데미지는 GAS 이펙트**로 표현(`CombatEffectCatalog`/`GameplayEffectMath`). 전투 수치 모델을 하나로 통일.

---

## 트러블슈팅

### 컨테이너 재시작 후 게임이 영영 시작 안 되던 버그

**증상:** Docker 재시작 후 방을 만들어도 게임이 시작 안 됨.

**원인:** Redis가 부팅 직후 `LOADING` 상태일 때 컨슈머가 예외로 죽으면 **영구 사망**(재구독 안 함). `GameStartRequestedConsumer`가 그렇게 죽어 있었다.

**해결:** 복원력을 **`ResilientStreamConsumer`로 중앙화**(재연결·재구독·백오프) → 3개 컨슈머를 이관. 개별 컨슈머가 복원 로직을 중복 구현하지 않게 됨. (plan §9.10 / codemap §2.8)

---

## 아직 미완성인 것 (TODO)

```
몬스터 웨이브/스폰 페이즈 (4.1.6)
보스/특수 몬스터 (4.8)
원격 피격자 HP 라우팅
   → 현재 monster_attack_dmg는 EffectReceiver 로컬 라우팅만,
     원격 ASC 라우팅은 공유 시계(CA-3 후속)와 함께
데미지 산식 GAS 스탯화 (현재 CombatEffectCatalog 고정값 → M5에서 공격력/방어력 기반)
```

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| 의도된 비대칭 | 몬스터 HP=서버 권위 / 플레이어 HP=클라 결정론 (NPC는 서버 소유) |
| 단일 RoomTickService | 방별 매니저 대신 하나의 10Hz 틱이 전 방 순회 (과분리 회피) |
| MonsterAiMath | AI '수식'만 순수 함수로 분리 → 단위 테스트 (Idle/Patrol/Chase/Attack) |
| 저작-런타임 분리 | Map Editor 마커 → spawn-layouts.json → 서버 파싱 (클라 런타임 미사용) |
| bounds.Clamp | 매 틱 경계로 되돌려 몬스터 이탈 방지 (순수 함수 내) |
| RemoteDriver 패턴 | 남의 상태를 보간만 — 원격 플레이어/몬스터(MonsterEntity) 공통 |
| GAS 이펙트 데미지 | 플레이어↔몬스터 양방향 모두 GameplayEffect로 수치 표현 |
| ResilientStreamConsumer | Redis 컨슈머 복원력 중앙화 (LOADING/재연결 영구사망 방지) |

# 챕터 13 학습 로그 — 몬스터 (서버 권위 NPC + AI 틱 + 클라 보간)

> M3. 서버가 소유하는 NPC(몬스터)의 스폰·이동(AI)·양방향 전투·클라 렌더.

## 설계 결정과 근거

### 몬스터 HP를 어디서 소유할까 — 의도된 비대칭

플레이어 HP는 클라 결정론(기존)인데, 몬스터도 같은 방식으로 클라가 시뮬하면 통일성은 있다. 그러나 클라가 몬스터 HP/위치를 시뮬하면 클라마다 다른 몬스터를 보게 되고(일관성 붕괴) 치팅 표면도 커진다. 그래서 의도적으로 **비대칭**을 택했다:

| 대상 | HP 권위 | 이동 |
|------|---------|------|
| 플레이어 | 클라 결정론(기존 유지) | 클라 입력 → 서버 릴레이 |
| 몬스터 | **서버 권위** | **서버 시뮬 + 클라 보간** |

몬스터는 "서버가 소유한 객체"라 권위를 서버로 일원화하는 게 자연스럽다. 클라엔 **몬스터 AI도 물리도 없다** — 받은 스냅샷을 보간만 한다.

---

### 몬스터 상태/AI를 어디에 둘까 — 단일 틱 서비스

대안은 `Room`마다 `MonsterManager`를 붙여 각자 AI를 돌리는 것. 하지만 방이 수십 개면 매니저도 수십 개(과분리)이고, 몬스터 상태는 이미 방에 속한 데이터다. 그래서 단일 틱 서비스로 모았다:
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

### 스폰/패트롤/맵경계를 어떻게 개발해 나가야 하나? — 저작-런타임 분리

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

### 원격 엔티티는 어떻게 그릴까 — Entity Interpolation

원격 플레이어와 몬스터는 클라가 **소유하지 않는다**(서버 권위). 그래서 클라에는 입력·물리·AI를 두지 않고 **서버 스냅샷을 받아 보간만** 한다. 이 기법의 정식 명칭이 **Entity Interpolation**(원격 엔티티 보간)이다 — 넷코드 3대 기법(Client-Side Prediction=내 캐릭터 / Server Reconciliation=내 예측 보정 / **Entity Interpolation=남의 캐릭터·NPC**) 중 마지막. 이 패턴을 **참고해 구현**했다.

**왜 보간 전용인가**: 서버 권위 모델에서 클라의 역할은 "표현"이다. 원격 엔티티에 시뮬레이션(AI/물리)을 넣으면 클라마다 결과가 갈려 서버 권위와 충돌한다. 그래서 받은 위치·회전을 부드럽게 따라가는 보간만 둔다. 원격 플레이어와 몬스터가 본질이 같아(둘 다 "남이 만든 상태") 동일 구현을 공유한다.

```
S_Spawn*      → 엔티티 인스턴스화
S_*State/Move → 보간 타깃 갱신
S_*Dead/Left  → 디스폰
```

#### 현재 구현은 Entity Interpolation의 단순화본

정식 기법은 스냅샷을 **버퍼**에 쌓고 "약간 과거" 시점을 두 스냅샷 사이로 보간하지만, 현재는 **최신 스냅샷 1개로 목표 위치를 향해 지수 감쇠(lerp-to-latest)** 하는 단순화본이다.

| 항목 | 정식 Snapshot Interpolation | 현재 구현 |
|------|------------------------------|-----------|
| 스냅샷 보관 | **버퍼**(타임스탬프된 N개) | 최신 1개만 |
| 렌더 시점 | **과거 고정 지연**(예: Valve `cl_interp 0.1s`) | 지연 없음, 최신값으로 즉시 보간 |
| 보간 | 두 스냅샷 사이 **시간 비율** 보간 | 목표값으로 지수 감쇠 |
| 타임스탬프 | 적극 사용 | 미사용(이동 패킷 TimeStamp는 릴레이만) |
| 패킷 손실 | 2개 보유로 견딤 / 짧게 외삽 | 다음 스냅샷까지 마지막 위치로 수렴 |

**트레이드오프**: lerp-to-latest는 코드가 짧고 무비용이지만 패킷/프레임레이트 의존이라 지터에 고무줄(rubber-band)이 생기고 시간적으로 부정확하다. **저속 Co-op 시각용으론 충분**, 빠른 PvP/정밀 동기화로 가면 버퍼 기반으로 승격이 정석.

#### 업그레이드 경로 — 버퍼 기반 보간

```
서버 ──snapshot(t,pos,rot)──▶ SnapshotBuffer (시간순 누적)
                                  [t0]──[t1]──[t2]──[t3]
매 프레임:
   renderTime = now - interpolationDelay   // 예: -100ms, "과거를 그린다"
   renderTime을 감싸는 두 스냅샷 A,B 선택
   alpha = (renderTime - A.t) / (B.t - A.t)
   pos = Lerp(A, B, alpha)                  // 시간 비율 정확 보간
   renderTime보다 오래된 스냅샷 소모(제거); 버퍼 마르면 짧게 외삽 후 정지
```

- **고정 지연**이 네트워크 지터를 흡수(대신 그만큼 원격을 과거로 그림 — 시각 지연↑). delay가 크면 굼뜨고 작으면 끊김 → 튜닝 대상.
- 버퍼에 ≥2개면 패킷 1개 유실돼도 안 멈춤. 마르면 외삽(Valve 기준 ~250ms 초과 시 부정확).
- [챕터 8](./chapter-08-socket-movement.md)에서 **이동 패킷 TimeStamp를 서버가 안 덮어쓰고 클라 원본 릴레이**한 게 이 보간의 *준비된 토대*다(스냅샷에 원본 시각이 살아있음). 그 값을 쓰기 시작하면 정식 보간으로 승격된다.
- ⚠️ 구분: 이 버퍼는 **원격 표현(렌더)** 용. 피격 판정을 과거로 되감는 **Lag Compensation**은 *서버측 위치 히스토리*라는 별개 기록이 필요(전투가 서버 권위 hitbox라 정밀화 시 별도 도입).

#### 출처

- **Gabriel Gambetta — Entity Interpolation**: <https://www.gabrielgambetta.com/entity-interpolation.html> (이 패턴의 입문 기준)
- Valve — Source Multiplayer Networking: <https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking>
- Gaffer On Games — Snapshot Interpolation: <https://gafferongames.com/post/snapshot_interpolation/>

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
| Entity Interpolation | 원격 엔티티를 서버 스냅샷으로 보간(넷코드 3대 기법 중 남의 캐릭터·NPC 담당). 현재는 lerp-to-latest 단순화본, 정식은 버퍼+과거지연 보간 |
| GAS 이펙트 데미지 | 플레이어↔몬스터 양방향 모두 GameplayEffect로 수치 표현 |
| ResilientStreamConsumer | Redis 컨슈머 복원력 중앙화 (LOADING/재연결 영구사망 방지) |

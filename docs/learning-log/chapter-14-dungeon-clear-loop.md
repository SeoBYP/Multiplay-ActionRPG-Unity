# 챕터 14 학습 로그 — 던전 클리어 루프 + Exp 보상 (DoD 완성)

> M4. 클리어/실패 판정 → Exp 보상 지급 → 결과 UI → 로비 복귀까지 "한 판"을 닫는다.

## 설계 결정과 근거

### 클리어/실패 발화의 동시성 — Interlocked 단일 outcome

순진하게 `if (!room.IsCleared) { room.IsCleared = true; ... }` 식 bool 플래그로 두면 두 문제가 있다: ① 틱 스레드와 패킷 핸들러가 동시에 분기에 들어오면 **두 번 발화**. ② "전멸=클리어"와 "전원 다운=실패"가 거의 동시에 성립하면 bool 두 개가 둘 다 켜질 수 있다. 그래서 단일 outcome으로 묶었다:

```
Room._outcome  (int, Interlocked.CompareExchange)
   None → Cleared  (TryMarkCleared: 전멸 최초 1회만 성공)
   None → Failed   (TryMarkFailed: 전원 다운 최초 1회만 성공)
   → 둘은 배타. 먼저 성립한 쪽만 발화, 나머지는 CompareExchange 실패로 무시
```

클리어와 실패가 **상호 배타 + 정확히 1회**임을 락 없이 보장.

---

### 전멸은 SocketServer가 아는데, 보상은 누가 주나 — 책임 경계

전멸을 감지한 SocketServer가 그 자리에서 DB에 Exp를 쓰는 게 가장 짧은 길이다. 하지만 SocketServer는 **실시간 전용**(TCP/틱)이고, 영속·도메인(Progression)은 **GameServer 책임**이다. 두 서버는 **직접 RPC 금지** — Redis Streams로만 통신한다. 그래서 흐름을 나눴다:

```
[SocketServer]  몬스터 전멸 → Room.TryMarkCleared
                  → S_DungeonClear(1820) 방 브로드캐스트   (클라 즉시 결과 표시용)
                  → DungeonClearMessage{RoomId,MapId,Participants}
                       → stream:game:dungeon:result        (보상 처리용 이벤트)
[GameServer]    DungeonResultConsumer (Consumer Group)
                  → 보상 산정 + 영속
```

"즉시 보여줄 것(브로드캐스트)"과 "정확히 처리할 것(영속, 이벤트)"을 **두 경로로 분리**. 실시간성과 정합성을 각자 최적화.

---

### 같은 결과가 두 번 들어오면 Exp 두 배? — 멱등 지급

**문제:**
Consumer Group은 재시작·재처리 시 같은 메시지를 다시 줄 수 있다(at-least-once). 그대로 `AddExp`하면 보상이 중복된다.

**해결 — RoomId 멱등(claim-first):**

```csharp
// Redis SET NX 로 "이 RoomId는 내가 처리했다" 선점 — 성공한 1회만 지급
if (await redis.StringSetAsync($"reward:claimed:{roomId}", "1", when: When.NotExists))
    foreach (var p in participants) await progression.AddExp(p, expReward);
// 선점 실패 = 이미 처리됨 → 스킵 (at-most-once)
```

분산 결과 처리에서 **at-least-once 전달 + at-most-once 효과**를 claim-first로 맞춤.

---

### Exp를 어디에 영속할까 — 별도 도메인 분리

`users` 테이블에 `level`/`exp` 컬럼을 추가하는 게 간단하다. 하지만 미래에 **캐릭터 교체(원신식)** 를 넣으면 Exp/Level은 계정이 아니라 **캐릭터 귀속**이 되어야 한다 — UserProfile에 박으면 그때 마이그레이션 지옥이다. 그래서 처음부터 분리했다:

```
user_progressions  (users 1:1, Lv/Exp/UpdatedAt)   ← 별도 테이블
   UserProgression 엔티티 (AddExp) + IProgressionRepository/Service
   Cache-Aside + Delete, lazy get-or-create, AsNoTracking 폴백
```

지금은 user_id 1:1이지만 **테이블을 분리해 둠**으로써 캐릭터 시스템 도입 시 진입점만 바꾸면 되게 했다(YAGNI와 미래 대비의 균형).

---

### 보상 범위를 어디까지 — Exp 전용 (YAGNI)

**결정:** 보상 = **Exp만**. 인벤토리/아이템 드랍은 범위 제외.

던전→Exp 매핑도 DB `DungeonId`를 새로 만들지 않고 **Shared 카탈로그(MapId 키)** 로 해결:

```
spawn-layouts.json 의 expReward:100 (dungeon_01)
   → SocketServer가 S_DungeonClear.RewardExp에 실어 보냄(클라 표시)
   → DungeonResultConsumer가 SpawnLayoutTable.Get(MapId).ExpReward로 지급(서버 권위)
```

MapId가 이미 메시지로 흐르고 정적 기획데이터라, **DB 스키마 추가 없이** 관통. (서버 간 직접 참조 금지 원칙도 지킴)

---

### 결과 화면을 Model이 직접 띄울까 — MVI

**올바른 설계 (View는 자기 Model만 안다):**

```
S_DungeonClear → DungeonClearPacketHandler
   → ISocketPacketState.MarkDungeonCleared(rewardExp)  (이벤트 발행)
   → InGameModel.OnDungeonCleared → Dispatch(DungeonCleared)
   → InGameReducer → InGameState.IsDungeonCleared=true, RewardExp
   → GameHud.Render(state): dungeonClearView.SetActive(true) + SetReward(exp)
   → ReturnToLobby (기존 던전→Main 복귀 재사용)
```

패킷/네트워크 타입은 `GameHud`에 노출하지 않는다 — `InGameModel`이 도메인 State로 변환, View는 primitive(`long rewardExp`)만 받는다.

---

## 트러블슈팅

### 던전 입장이 간헐적으로 안 되던 버그 — long-lived DbContext stale

**증상:** 게임 시작했는데 클라가 던전에 못 들어가고 로비 상태만 계속 받음.

**원인:** `SubscribeRoom` 같은 **스트리밍 RPC가 한 스코프 = 한 DbContext를 수십 초 유지**. 추적 쿼리는 EF identity map의 예전 엔티티를 그대로 반환 → 다른 스코프가 DB에 쓴 `Playing` 전이를 **그 스트림이 끝날 때까지 못 읽음**. SendLoop이 계속 `Starting`을 읽어 `UpdateEvent`만 보냄.

**해결:** cache-aside DB 폴백을 **`AsNoTracking`** 으로. "이벤트는 ID+다시 읽어라 트리거일 뿐, 최신 상태는 항상 DB에서" 원칙을 코드로 강제. (networking 규칙에 박제)

### 캐릭터가 "움직이다 갑자기 멈춤" — 고프레임레이트 deadlock

검증 중 발견(상세는 별도): `CharacterMotor` 속도 램프가 `controller.velocity` 기반이라, 고fps(300fps)에서 첫 프레임 변위가 `CharacterController.minMoveDistance(0.001)`보다 작아 컨트롤러가 이동을 무시 → velocity 0 → 램프가 0에서 못 벗어나는 교착. **램프를 `m_speed`(직전 의도속도) 기반으로** 바꿔 해소.

---

## 아직 미완성인 것 (TODO)

```
DungeonFailed.prefab 아트 + 결과 패널 다듬기
레벨업 산식 / 스탯 성장 (현재 Exp 적립만, M5)
아이템 보상 / 루트 테이블 (범위 제외 → M5 인벤토리 합류 시)
```

---

## 핵심 키워드 정리

| 키워드              | 한 줄 설명                                                                       |
| ------------------- | -------------------------------------------------------------------------------- |
| Interlocked outcome | 클리어/실패를 배타·정확히 1회로 (CompareExchange, 락 없이)                       |
| 즉시 vs 정확 분리   | S_DungeonClear(브로드캐스트, 즉시 표시) / DungeonClearMessage(이벤트, 영속 처리) |
| 책임 경계           | 실시간=SocketServer, 영속/도메인=GameServer, 결합은 Redis Streams                |
| 멱등 지급           | RoomId Redis SET claim-first → at-least-once 전달을 at-most-once 효과로          |
| user_progressions   | Exp/Lv 별도 테이블 (UserProfile 컬럼 금지, 캐릭터 귀속 대비)                     |
| MapId Exp 카탈로그  | DB DungeonId 없이 Shared spawn-layouts.expReward로 보상 산정                     |
| MVI 결과 흐름       | 패킷→PacketState→Model(Reducer)→State→GameHud, View는 자기 Model만               |
| AsNoTracking 폴백   | long-lived 스트리밍 스코프의 stale 엔티티 방지 (DB가 단일 진실)                  |

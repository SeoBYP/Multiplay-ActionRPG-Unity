# 14. 던전 클리어 루프 — 한 판을 닫는다

> **한 줄** — 몬스터를 다 잡으면 끝나는 게 아니라, **정확히 한 번** 끝나야 하고 **정확히 한 번** 보상이 지급돼야 한다. 클리어와 실패는 동시에 성립할 수 있고, 이벤트는 두 번 배달될 수 있다. 그 둘을 각각 `Interlocked`와 **claim-first 멱등**으로 닫았다.
>
> **범위** 결과 판정 동시성 · 책임 경계 · 멱등 지급 · 스키마 분리 · 결과 UI
> **의미** 이 챕터로 **DoD(2인 접속 → 방 → 던전 → 협력 처치 → 클리어 → 보상 → 로비 복귀)** 가 닫혔다

---

## 1. 클리어와 실패는 동시에 성립할 수 있다

순진한 구현은 이렇다.

```csharp
if (!room.IsCleared) { room.IsCleared = true; Broadcast(...); }   // ❌
```

두 가지가 깨진다.

- **이중 발화** — 틱 스레드(몬스터 전멸 감지)와 패킷 핸들러(플레이어 사망 보고)가 동시에 분기에 들어온다. 검사와 대입 사이에 다른 스레드가 끼어든다.
- **동시 성립** — 마지막 몬스터를 잡으면서 마지막 생존자가 죽으면 **"전멸=클리어"와 "전원 다운=실패"가 둘 다 참**이 된다. bool 두 개면 둘 다 켜진다.

두 번째가 더 중요하다. 이건 경쟁 조건이 아니라 **모델링 오류**다 — 서로 배타적인 두 결과를 독립된 bool 두 개로 표현했기 때문에 생긴다.

```csharp
// Room.cs:54 — 결과는 하나의 값이다
private int _outcome;   // 0=None, 1=Cleared, 2=Failed

public bool TryMarkCleared()
    => Interlocked.CompareExchange(ref _outcome, 1, 0) == 0;   // None 일 때만 성공

// 실패도 같은 슬롯을 두고 경쟁한다 (Room.cs:446)
    => Interlocked.CompareExchange(ref _outcome, 2, 0) == 0;
```

**하나의 슬롯을 두고 경쟁시키면 배타성과 1회성이 동시에 나온다.** 락도 필요 없다. 먼저 도착한 쪽만 성공하고 나머지는 조용히 실패한다.

> 이 값은 나중에 부활 기능이 붙을 때 **재사용**됐다 — "실패가 확정된 뒤에는 부활 불가"를 `_outcome == Failed` 하나로 판정한다(`Room.cs:465`). 상태를 하나로 모아 두면 나중에 붙는 규칙도 그 하나만 보면 된다. → [24](./chapter-24-coop-revive.md)

## 2. 전멸을 아는 쪽과 보상을 주는 쪽이 다르다

전멸을 감지한 SocketServer가 그 자리에서 DB에 Exp를 쓰는 게 가장 짧다. 하지만 SocketServer는 **실시간 전용**이고 영속·도메인은 GameServer 책임이며, 둘은 **직접 RPC를 하지 않는다**([01](./chapter-01-architecture.md)).

그래서 **경로를 둘로 나눴다.**

```
[SocketServer] 전멸 감지 → Room.TryMarkCleared()
    ├─▶ S_DungeonClear(1820) 방 브로드캐스트         ── "즉시 보여줄 것"
    └─▶ DungeonClearMessage{RoomId, MapId, 참가자}
           → stream:game:dungeon:result               ── "정확히 처리할 것"
                    │
[GameServer]  DungeonResultConsumer (Consumer Group)
                    → 보상 산정 + 영속
```

**즉시성과 정합성은 서로 다른 요구다.** 결과 화면은 지금 떠야 하고(수십 ms), 보상은 정확해야 한다(중복 금지·유실 금지). 한 경로로 처리하면 둘 중 하나가 손해를 본다 — 브로드캐스트를 DB 커밋 뒤로 미루면 화면이 늦고, 반대로 하면 정합성이 흔들린다.

## 3. at-least-once 전달을 at-most-once 효과로

Consumer Group은 재시작·재처리 시 **같은 메시지를 다시 준다**([05](./chapter-05-game-start-e2e.md) 3절의 Outbox도 마찬가지다). `AddExp`는 멱등이 아니므로 그대로 두면 보상이 두 배가 된다.

```csharp
// DungeonResultConsumer.cs:42 — 처리했다는 사실을 먼저 원자적으로 선점한다
bool claimed = await _redis.SetAddAsync(RedisKeys.DungeonResultProcessed(), message.RoomId);
if (!claimed) return;                      // 이미 처리됨 → 스킵
await _redis.KeyExpireAsync(RedisKeys.DungeonResultProcessed(), ProcessedTtl);
// ... 여기서부터 지급
```

**claim-first**가 핵심이다. 지급하고 나서 기록하면 그 사이에 죽었을 때 중복이 되고, 먼저 선점하면 최악의 경우 **미지급**(더 안전한 실패)이 된다. 분산 처리에서는 **"두 번 주는 것"보다 "안 주는 것"이 낫다** — 후자는 복구할 수 있지만 전자는 되돌리기 어렵다.

> ⚠️ **현재 구현의 한계** — 처리 기록이 **RoomId 하나당 키가 아니라 Set 하나**이고, TTL(24h)이 **Set 전체**에 걸린다(추가할 때마다 갱신). 24시간 동안 던전 결과가 하나도 없으면 Set이 통째로 만료되고, 그 뒤 아주 오래된 메시지가 재배달되면 이중 지급이 가능하다. 재배달 경로가 실재한다는 점도 걸린다 — PEL 자동 회수(`XAUTOCLAIM`)가 없어 미ACK 메시지가 남는다([05](./chapter-05-game-start-e2e.md) 10절). RoomId별 키(`SET NX EX`)로 바꾸면 각자의 수명을 갖는다. (**미실측** — 코드 경로상 확인)

## 4. 스키마를 미리 나눈 이유

`users` 테이블에 `level`/`exp` 컬럼을 붙이는 게 간단하다. 그런데 이 프로젝트는 **캐릭터 교체(원신식)** 를 방향으로 두고 있다. 그러면 Exp/Level은 계정이 아니라 **캐릭터에 귀속**돼야 한다.

```
user_progressions  (지금은 users 와 1:1, Lv/Exp/UpdatedAt)
   UserProgression 엔티티 + IProgressionRepository/Service
   Cache-Aside + Delete · lazy get-or-create · AsNoTracking 폴백
```

**지금 필요한 기능은 전혀 늘리지 않았다.** 다만 값이 **어느 테이블에 사는지**만 미리 갈랐다. 나중에 캐릭터가 생기면 이 테이블의 키를 바꾸면 되지만, `UserProfile`에 컬럼으로 박아 뒀다면 데이터 이관이 필요하다.

> **YAGNI와 충돌하는가** — 판단 기준은 "기능을 미리 만드는가"와 "되돌리기 비용이 큰 결정을 미루는가"를 나누는 것이었다. 캐릭터 시스템은 **만들지 않았고**(YAGNI 준수), 테이블 분리는 **나중에 바꾸기 비싼 쪽**이라 지금 골랐다.

## 5. DB에 `DungeonId`를 만들지 않은 이유

던전별 Exp 보상을 어디에 둘 것인가. DB에 던전 테이블을 만들면 자연스러워 보이지만 그러지 않았다.

```
spawn-layouts.json 의 expReward (dungeon_01: 100)
  ├─▶ SocketServer  : S_DungeonClear.RewardExp 에 실어 보냄       (클라 표시용)
  └─▶ GameServer    : SpawnLayoutTable.Get(MapId).ExpReward       (지급, 서버 권위)
```

이유는 셋이다.

- **`MapId`가 이미 메시지로 흐른다** — 새 식별자를 만들 이유가 없다.
- **정적 기획 데이터**다 — 런타임에 안 바뀌는 값을 DB에 두면 조회 비용만 는다.
- **표시와 지급이 같은 소스를 본다** — 두 서버가 같은 카탈로그를 읽으므로 "화면에 100인데 90이 들어오는" 불일치가 구조적으로 불가능하다.

## 6. 결과 화면도 MVI를 지킨다

```
S_DungeonClear (패킷)
  → ISocketPacketState.MarkDungeonCleared(rewardExp)   이벤트 발행
  → InGameModel.OnDungeonCleared → Dispatch
  → InGameReducer → InGameState { IsDungeonCleared, RewardExp }
  → GameHud.Render(state) : 패널 활성 + SetReward(exp)
  → ReturnToLobby (기존 던전→Main 복귀 경로 재사용)
```

`GameHud`는 **패킷 타입을 모른다.** `InGameModel`이 도메인 State로 변환하고, View는 `long rewardExp` 같은 primitive만 받는다. 패킷이 바뀌어도 View는 안 바뀐다.

## 7. 곁가지로 잡은 버그 둘

**① 던전 입장이 간헐적으로 안 되던 문제** — 스트리밍 스코프의 EF 추적 캐시가 옛 방 상태를 계속 반환하고 있었다. 이 프로젝트에서 가장 비싼 버그였고, 전말은 [07](./chapter-07-db-cache.md) 3절에 있다.

**② 캐릭터가 움직이다 갑자기 멈추는 문제 — 프레임레이트가 만든 교착**

```
CharacterMotor 의 속도 램프가 controller.velocity 를 읽어 다음 속도를 계산
    ↓ 300fps 에서 한 프레임 변위 = 아주 작음
CharacterController.minMoveDistance(0.001) 미만 → 컨트롤러가 이동을 무시
    ↓
controller.velocity == 0
    ↓
램프 입력이 0 → 다음 프레임도 0 → 영원히 못 벗어남 (교착)
```

**출력을 입력으로 되먹이는 구조**여서 생긴 문제다. 프레임이 빨라질수록 한 프레임의 변위는 작아지는데, 엔진에는 "너무 작으면 무시" 임계값이 있다. 램프의 기준을 **실측 속도가 아니라 직전 의도 속도(`m_speed`)** 로 바꿔 되먹임 고리를 끊었다.

> **교훈** — 프레임레이트는 성능 변수가 아니라 **입력 변수**다. 60fps에서만 테스트하면 이런 버그는 절대 나오지 않는다. 그리고 "엔진이 무시할 만큼 작은 값"이 존재한다는 사실은 문서를 읽기 전에는 알 수 없다.

## 8. 그 이후

| 당시 TODO | 결말 |
|---|---|
| 레벨업 산식 / 스탯 성장 | ✅ `LevelTable`(SO 저작 → bake) + `ILevelCurve`/`LevelTableCurve` + 테스트. 몬스터 레벨링도 이 곡선을 직접 읽는다([26](./chapter-26-measured-combat-cleanup.md)) |
| 아이템 보상 / 루트 테이블 | ✅ 범위 밖으로 뒀다가 인벤토리와 함께 합류([15](./chapter-15-loot-drop-inventory.md)) |
| `DungeonFailed` 결과 패널 다듬기 | ✅ 결과 UI 완성 |

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 결과는 단일 슬롯(`_outcome`) | 부활 가능 여부 판정이 이 값 하나로 해결([24](./chapter-24-coop-revive.md)) |
| 즉시 표시 / 정확 처리 경로 분리 | 루팅·퀘스트 보상도 같은 형태([15](./chapter-15-loot-drop-inventory.md)·[19](./chapter-19-quest-system.md)) |
| claim-first 멱등 | 분산 지급 전반의 기본형 |
| 정적 기획 데이터는 공유 카탈로그로 | 데이터 전면 SO 저작 → bake 파이프라인의 시작점([20](./chapter-20-content-pipeline-addressables.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-14-dungeon-clear-loop.md](../learning-log/chapter-14-dungeon-clear-loop.md)

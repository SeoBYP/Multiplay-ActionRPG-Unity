# 05. 게임 시작 — 서로를 호출하지 않는 두 서버가 한 판을 여는 법

> **한 줄** — "방 시작" 버튼 하나가 두 서버를 가로지른다. 직접 호출하면 간단하지만 **한쪽이 죽으면 같이 죽는다.** 대신 이벤트 왕복으로 묶고, 그 과정에서 생기는 **이중 쓰기 문제를 Transactional Outbox**로 닫았다.
>
> **범위** 서버 간 결합 · Outbox 패턴 · Consumer Group 운영 · 접속 주소 공지 · 멱등 재시도
> **검증** `DungeonLobbyE2ETests` · `SocketE2ETests`(Docker 대상 gRPC 로비 → TCP 입장 전 구간)

---

## 1. 직접 RPC를 쓰지 않은 이유

```
GameServer ──[gRPC 직접 호출]──▶ SocketServer      ← 되긴 된다
```

되긴 되는데, 세 가지가 따라온다.

- **장애 전파** — SocketServer가 죽어 있으면 "방 시작" RPC 자체가 실패한다. 로비 기능이 인게임 서버의 가용성에 묶인다.
- **라우팅** — SocketServer가 여러 대가 되는 순간 "어느 인스턴스에 보낼 것인가"를 GameServer가 알아야 한다.
- **강결합** — 두 서버의 배포·스케일이 서로를 신경 써야 한다.

이벤트로 바꾸면 GameServer는 **"게임 시작이 요청됐다"는 사실만 기록**하고 손을 뗀다. 누가 언제 처리하는지는 GameServer의 관심사가 아니다.

## 2. 전체 흐름 — 요청과 응답이 각각 독립 이벤트

```
[클라]                  [GameServer]                      [SocketServer]
  │                          │                                  │
  ├─ SubscribeRoom ─────────▶│  (gRPC 스트림 연결 유지)          │
  │                          │                                  │
  ├─ StartRoom ─────────────▶│                                  │
  │                          │ ① room.Status = Starting         │
  │                          │ ② Outbox 기록  ← ①과 같은 트랜잭션 │
  │                          │                                  │
  │                          │ (OutboxPublisher가 1초마다 배치 발행)
  │                          ├── stream:game:start ────────────▶│
  │                          │   {RoomId, PlayerInfos, MapId}   │
  │                          │                                  ├─ CreateRoom()
  │                          │                                  ├─ PlayerState 초기화
  │                          │◀── stream:game:session:ready ────┤
  │                          │    {RoomId, Host, Port}          │
  │                          │                                  │
  │                          │ GameSessionReadyConsumer:        │
  │                          │  세션 생성 → room.Status = Playing│
  │                          │  → 구독자에게 publish            │
  │◀─ GameStartedEvent ──────┤                                  │
  │   {RoomInfo, host, port} │                                  │
  │                                                             │
  └──────────── TCP 접속 (host:port) ──────────────────────────▶│
```

요점은 **요청과 응답이 별개의 이벤트**라는 것이다. GameServer는 응답을 "기다리지" 않는다. 응답 이벤트가 도착하면 그때 다음 단계를 진행한다.

## 3. 이 챕터의 핵심 — 이중 쓰기(Dual Write) 문제

이벤트 방식에는 함정이 있다. **DB와 메시지 큐, 두 곳에 써야 하는데 둘은 같은 트랜잭션에 들어가지 않는다.**

```
방 상태를 Starting으로 저장  →  💥 여기서 프로세스가 죽으면  →  메시지는 영영 발행되지 않는다
                                  방은 Starting인데 아무도 시작을 모른다 (영구 멈춤)

메시지 먼저 발행           →  💥 여기서 죽으면  →  SocketServer는 방을 만들었는데
                                  GameServer의 방 상태는 Waiting (유령 방)
```

어느 순서로 해도 창(window)이 남는다. 해법은 순서 바꾸기가 아니라 **쓰기 대상을 하나로 만드는 것**이다.

```csharp
// OutboxRepository.cs:18 — 방 갱신과 메시지 기록을 한 DB 트랜잭션으로
await using var transaction = await context.Database.BeginTransactionAsync(ct);
context.DungeonRooms.Update(room);          // 상태 전이
context.OutboxMessages.Add(outboxMessage);  // 보낼 메시지도 "그냥 행 하나"
await context.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

메시지를 **DB 테이블에 먼저 쓴다.** 그러면 "상태가 바뀌었는데 메시지가 없는" 상태가 원천적으로 불가능해진다. 실제 발행은 별도 백그라운드 서비스가 맡는다.

```
OutboxPublisherService (BackgroundService)
  1초마다 → 미발행 메시지 20건 조회 → Redis Stream 발행 → 처리됨 표시
```

이 구조의 성질:
- **At-least-once** — 발행 후 표시 전에 죽으면 같은 메시지가 두 번 나간다. 그래서 **소비 측이 멱등해야 한다**(SocketServer의 `CreateRoom`은 이미 있으면 `null`을 반환하고 그냥 넘어간다).
- **지연 1초** — 즉시성과 맞바꾼 것. 로비→던전 전환에서 1초는 허용 범위였다.

> **트레이드오프 정리** — 분산 트랜잭션(2PC)을 도입하면 정확히 한 번을 얻지만 운영 복잡도가 급증한다. Outbox는 "**절대 유실하지 않는다 + 가끔 중복된다**"를 택하고, 중복은 소비 측 멱등성으로 처리한다. 게임 서버 규모에서는 이쪽이 압도적으로 실용적이다.

## 4. 준비 확인 — 폴링에서 이벤트로

초기 구현은 **폴링**이었다. 메시지를 발행한 뒤 SocketServer가 남긴 준비 완료 키를 100ms 간격으로 최대 10초 동안 찔렀다.

```
[v1] 발행 → while(10초) { Redis GET socket:room:{id}:ready; 100ms 대기 }  → 최대 100회 왕복
```

문제는 **RPC 하나가 남의 서버 상태에 묶인다**는 것이었다. SocketServer가 느리면 "방 시작" 응답이 그만큼 늦고, 10초를 넘기면 실패한다.

```
[v2] 발행하고 즉시 반환 → SocketServer가 stream:game:session:ready 로 응답
                        → GameSessionReadyConsumer 가 받아서 세션 생성·상태 전이·구독자 통지
```

폴링 키가 사라지고 **양방향 모두 이벤트**가 됐다. 덤으로 책임도 갈렸다 — `DungeonLobbyService`는 Outbox 기록까지, 세션 생성은 `GameSessionService`. 요청-응답 안에서 남을 기다리지 않게 되니 경계가 자연스럽게 분리됐다.

## 5. 접속 주소는 누가 아는가 — 바인드 주소 ≠ 공지 주소

초기엔 `"127.0.0.1:7777"`이 코드에 박혀 있었다. Docker에 올리는 순간 깨진다. 컨테이너 안에서 TCP는 `0.0.0.0`에 바인드해야 하는데, 그 주소를 클라에게 알려줄 수는 없기 때문이다.

```csharp
// ServerOptions — 두 주소는 목적이 다르다
public string Ip          { get; set; } = "127.0.0.1";  // 바인드 (Docker면 0.0.0.0)
public string AdvertiseIp { get; set; } = "";           // 클라에게 알려줄 주소
public string ResolvedAdvertiseIp => string.IsNullOrWhiteSpace(AdvertiseIp) ? Ip : AdvertiseIp;
```

**주소를 아는 주체는 SocketServer 자신**이므로, 준비 완료 이벤트에 `Host`/`Port`를 담아 GameServer로 보낸다. GameServer는 그것을 방에 기록해 구독자에게 전달할 뿐, SocketServer가 어디 떠 있는지 **설정으로 알 필요가 없다.** 인스턴스가 늘어나도 이 구조는 그대로 확장된다.

그리고 접속 정보는 `StartRoomResponse`가 아니라 **구독 스트림(`GameStartedEvent`)으로 간다.** 시작을 누른 방장도 이미 구독 중이므로, 모든 참가자가 **같은 한 경로**로 같은 정보를 받는다. 응답과 스트림 양쪽에 정보를 실으면 클라에 분기가 두 개 생긴다.

## 6. Consumer Group 운영 규칙 세 가지

**① 시작 위치는 `Beginning("0")`**

| | 의미 | 결과 |
|---|---|---|
| `NewMessages("$")` | 그룹 생성 이후 것만 | 재시작 창에 발행된 메시지 **영구 유실** |
| **`Beginning("0")`** | 처음부터 (ACK된 건 제외) | 재시작 시 미처리분 재처리 ✅ |

`"$"`는 프로젝트 전역 **금지 규칙**으로 못 박았다.

**② ACK는 처리 성공 후에** — `StreamAcknowledgeAsync`를 핸들러가 끝난 뒤에 호출한다. 먼저 ACK하면 처리 실패가 곧 유실이다.

**③ `NOGROUP`은 예상 가능한 상태다** — 개발 중 스트림을 지우고 재시작 없이 테스트하면 그룹이 사라져 `NOGROUP`이 뜬다. 이걸 에러로 죽이지 않고 **그룹을 다시 만들고 이어서 읽는다**(`no such key`도 같이 처리).

## 7. 복원력의 중앙화 — 컨슈머는 죽으면 부활하지 않는다

`.NET BackgroundService`는 **`ExecuteAsync`가 한 번 리턴하면 다시 시작되지 않는다.** 예외가 밖으로 새면 그 컨슈머는 프로세스가 살아 있는 채로 영구 사망한다. 초기 코드의 `_ = Task.Run(...)` 패턴은 여기에 더해 **예외를 조용히 삼켰다** — 로그도 없고, 프로세스도 살아 있고, 기능만 죽어 있는 최악의 형태였다.

실제로 컨테이너 재시작 직후 Redis가 `LOADING`(데이터 적재 중) 상태를 반환하면, 그 순간 읽던 컨슈머가 전부 영구 사망하는 버그가 있었다.

컨슈머마다 따로 고치는 대신 공통 루프 하나로 모았다.

```
ResilientStreamConsumer — 실패를 3분류한다
  ├ 취소(stoppingToken)        → 정상 종료
  ├ 스트림 읽기 실패            → 지수 백오프(1s → 최대 30s) 후 재개    ← LOADING·연결 끊김
  └ 메시지 1건 처리 실패(poison) → 그 메시지만 건너뛰고 스트림 유지
```

**한 건이 나쁜 것과 스트림이 끊긴 것은 다르다.** 이 둘을 구분하지 않으면, 독약 메시지 하나가 무한 재시도를 돌거나 일시적 장애가 컨슈머를 영구히 죽인다.

## 8. 유실을 큐에서 막지 않고 상위에서 복구한다

메시지가 어떤 이유로든 처리되지 못하면 방은 `Starting`에 멈춘다. 이 상황을 큐 레이어에서 완벽하게 방어하는 대신, **상위 레벨의 재요청**으로 풀었다.

```
StartGame 재호출 시 방 상태를 보고 분기
  Starting  → 이전 요청이 유실됐다고 보고 같은 메시지를 다시 Outbox에 기록 (상태는 그대로)
  Playing   → SocketServer가 재시작해 메모리가 비었을 수 있다 → 재발행 (CreateRoom은 멱등)
```

호스트가 스트림에 다시 붙는 것만으로도 자동 재시도가 걸린다(`SubscribeRoom`이 `Starting` 방의 호스트 재접속을 감지해 재트리거). **복구를 사람이 하는 특수 조작이 아니라 정상 경로에 심어 둔 것**이 요점이다.

## 9. 컴파일러가 잡아주지 않은 버그 둘

**`await null`**
```csharp
RoomInfo = await result.Value?.ToRoomInfo(...)   // Value가 null이면 메서드 호출 자체를 안 함
                                                 // → Task<T>가 아니라 null → await null → NRE
```
`?.`은 **호출을 건너뛰고 null을 반환**한다. `Task`를 반환하는 자리에서는 "안전한 호출"이 아니라 **NullReferenceException 예약**이다. 삼항 연산자로 명시했다.

**필드 하나를 4곳에 추가해야 하는 구조**
`SocketIp`/`SocketPort`를 추가했을 때 `Clone()`에서 빠뜨려 값이 조용히 사라졌다. Redis Hash 직렬화 엔티티는 `Clone` / `FromRedis` / `ParseFromRedis` / `ToHashEntry` **4곳이 항상 동기화**돼야 한다. 한 곳만 빠지면 "저장은 되는데 안 읽히거나" 그 반대가 된다. 이후 이 4곳 동시 수정은 프로젝트 규칙이 됐다.

## 10. 남은 것

- **✅ PEL(Pending Entry List) 자동 회수 — 2026-08-24 해소 (F4)** — ACK 전에 컨슈머가 죽으면 그 메시지는 Pending 목록에 남는데 `XAUTOCLAIM` 회수가 없어 **영구 잔류**했다. 8절의 상위 레벨 재요청이 덮고 있었지만 그건 **누군가 다시 시도해야** 복구된다는 뜻이었다.
  - 조사에서 더 나쁜 사실이 나왔다: GameServer 6개 큐의 컨슈머 이름이 **매 기동 새 GUID** 라, 재시작하면 자기 PEL 재읽기(`"0"`)도 빈 목록을 읽었다 — 즉 회수 주체가 아예 없었다. 이름을 `{prefix}-{MachineName}` 로 안정화했다.
  - 7개 큐의 복제된 소비 루프를 `RedisMessageQueueBase<T>.ConsumeGroupAsync()` 한 벌로 통합하고, 그 안에서 **유휴 구간에만**(30s 주기) `MinIdle` 60s 초과분을 `XAUTOCLAIM` 한다. 살아 있는 컨슈머가 처리 중인 메시지는 빼앗지 않는다.
  - 회수를 붙이자 **역직렬화 실패 엔트리가 독**이 됐다(매 스윕마다 같은 것을 다시 집는다) → 성패와 무관하게 ACK 하도록 함께 고쳤다.
  - **ACK 시점도 같은 날 해소** — ACK 를 봉투(`StreamMessage<T>`)에 담아 **핸들러가 성공한 뒤** 하도록 뒤집었다(at-least-once) + 재시도 상한 5회. 재실행될 핸들러 6종을 감사해 5종은 이미 멱등이었고, 비멱등이던 소비 통지에만 `ConsumeId` 를 추가했다. 보상 2경로는 ACK 시점만으로는 부족해 **DB 원장**으로 exactly-once 를 만들었다([14](./chapter-14-dungeon-clear-loop.md) 3절).
- SocketServer 다중 인스턴스 시 어느 인스턴스가 방을 맡을지의 배정 전략은 미설계(단일 인스턴스 전제).

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| Transactional Outbox | 던전 결과 보상 지급 경로가 같은 형태를 재사용([14](./chapter-14-dungeon-clear-loop.md)) |
| At-least-once + 소비 측 멱등 | 보상 중복 방지(Redis claim-first), 루팅 지급 멱등([15](./chapter-15-loot-drop-inventory.md)) |
| `ResilientStreamConsumer` | 이후 추가된 모든 Consumer가 이 루프 위에서 동작 |
| 바인드 주소와 공지 주소 분리 | Docker/로컬/MPPM 어디서도 같은 코드로 접속([mppm-testing](../wiki/mppm-testing.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-05-game-start-e2e.md](../learning-log/chapter-05-game-start-e2e.md)

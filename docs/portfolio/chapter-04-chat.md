# 04. 채팅 — 끊겨도 유실되지 않게, 방을 옮겨도 끊기지 않게

> **한 줄** — 채팅의 난이도는 "메시지를 보내는 것"이 아니라 **연결이 흔들릴 때**에 있다. 잠깐 끊긴 사이의 메시지가 사라지지 않아야 하고, 방을 옮길 때 연결 자체가 끊겨서는 안 된다. 전자는 Pub/Sub을 버려서, 후자는 **취소 토큰을 두 개로 쪼개서** 해결했다.
>
> **범위** 채팅 범위 3종 · Streams 전환 · fan-out vs 경쟁 소비 · 다채널 합류 · 방 전환 · 재연결 복구
> **검증** `ChatServiceTests` · `ChatE2ETests`(Docker 대상, Global/Room/Whisper 수신)

---

## 1. 범위(Scope)가 다르면 채널이 달라야 한다

```
Global   →  stream:chat:global              서버 전체
Room     →  stream:chat:room:{roomId}       같은 방
Whisper  →  stream:chat:user:{nickname}     특정 1명
```

하나의 채널에 다 넣고 클라가 걸러내는 방식은 **모든 유저에게 모든 메시지를 보내는 것**과 같다. 귓속말이 전 서버에 흐르고, 필터링은 클라의 선의에 달린다. 범위별로 채널을 나누면 전달 자체가 차단된다.

### 타입은 클라이언트가 정하지 않는다

```csharp
// ChatService.cs:41 — 서버가 세션 상태를 보고 결정
var chatType = !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
               currentRoom is not null                       ? ChatType.Room :
                                                               ChatType.Global;
```

클라가 `type: Global`을 보내는 구조였다면, 방에 있는 유저가 전체 채팅으로 도배하는 걸 막을 방법이 없다. **판정 근거(방 소속)를 서버가 이미 갖고 있으므로 클라에게 물어볼 이유가 없다.** 이건 이 프로젝트 전체의 권위 원칙과 같은 형태다 — 클라는 의도(대상 닉네임)만 보내고, 분류는 서버가 한다.

## 2. Pub/Sub을 버린 이유 — 유실이 기본값이다

처음엔 Redis Pub/Sub으로 만들었다. 문제는 **발행 순간 구독 중이 아니면 그 메시지는 영원히 사라진다**는 것이다. 모바일에서 터널 하나만 지나도 그 사이 대화가 통째로 비는데, 복구할 방법 자체가 없다.

| | Pub/Sub | **Streams** |
|---|---|---|
| 저장 | 없음 (휘발) | 있음 |
| 재연결 복구 | 불가능 | 마지막 ID 이후 조회 |
| 읽기 모델 | 서버가 콜백으로 push | 앱이 `XREAD`로 pull |

전환의 부수 효과가 하나 있었다 — **구독 객체가 통째로 사라졌다.** Pub/Sub은 Redis가 콜백을 호출하는 모델이라 `ISubscriber`·델리게이트·구독 채널 목록을 유저 컨텍스트가 들고 있어야 했다. `XREAD`는 앱이 직접 읽으므로 그 필드들이 전부 필요 없어진다. **모델을 바꾸면 상태도 같이 줄어든다**는 걸 여기서 봤다.

## 3. 같은 Streams인데 인터페이스를 둘로 나눈 이유

"채팅도 큐, 서버 간 통신도 큐인데 왜 둘이지?" — **소비 방식이 정반대**이기 때문이다.

```
IBroadcastChannel  (채팅)          IMessageQueue  (서버 간)
  XADD → 각자 독립 XREAD             XADD → Consumer Group
  1 메시지 → N명 전원 수신            1 메시지 → 1명만 처리 (경쟁 소비)
  fan-out                            작업 분배 + ACK
```

채팅에 Consumer Group을 쓰면 **한 명만 메시지를 받고 나머지는 못 받는다.** 반대로 서버 간 작업 분배를 fan-out으로 하면 같은 일이 N번 처리된다. 저장소가 같아도(Redis Stream) 소비 의미가 다르면 계약을 분리해야 한다.

이 분리는 지금도 유효하다 — 채팅은 `IBroadcastChannel`, 던전 결과·세션 준비·루팅 지급은 전부 `IMessageQueue` 기반 Consumer(`DungeonResultConsumer`·`GameSessionReadyConsumer`·`LootGrantConsumer`·`RoomLifecycleConsumer`)다.

## 4. 다채널 합류 — 한 명이 3개 채널을 동시에 듣는다

유저 한 명은 Global + Room + Whisper를 **동시에** 구독해야 한다. `IBroadcastChannel`은 "채널 1개 읽기"까지만 책임지므로, 그 위에 합류 계층(`ChatEventStream`)을 뒀다.

```
 Global  XREAD Task  ─┐
 Room    XREAD Task  ─┼──▶  merged Channel  ──▶  Outbound(Bounded)  ──▶  gRPC 스트림
 Whisper XREAD Task  ─┘
```

역할 분리가 핵심이다 — **`IBroadcastChannel` = 단일 채널 구현(Infrastructure), `ChatEventStream` = 다채널 집계(경계 매개체).** 합류 로직을 채널 구현 안에 넣었다면 "채널 하나를 읽는다"는 단순한 계약이 오염됐을 것이다.

## 5. 이 챕터의 핵심 — 취소 토큰을 두 개로 쪼갠 이유

방을 옮기면 구독 채널이 바뀌므로 읽기 루프를 재시작해야 한다. 처음엔 컨텍스트의 `Cts`를 취소했는데, **gRPC 연결 자체가 끊어졌다.**

원인은 하나의 토큰이 두 개의 수명을 대표하고 있었기 때문이다.

```
                          [잘못된 구조: 토큰 1개]
   방 전환 → Cts.Cancel() → 읽기 루프 종료 ✓
                          → gRPC 스트림 종료 ✗  ← 의도하지 않은 동반 사망

                          [수정: 수명을 분리]
   Cts          = 연결 전체의 수명  (로그아웃 · 앱 종료)
   ReadLoopCts  = 읽기 루프의 수명  (방 전환)
```

방 전환 흐름은 이렇게 된다.

```
1. ReadLoopCts.CancelAsync()      ← 읽기 루프만 종료
2. ReadLoopCts = new CTS          ← 새 토큰
3. ctx.CurrentRoomId = roomId     ← ★ 반드시 여기서 먼저 갱신
4. 새 채널 목록 구성 → ReadLoop 재시작
```

**3번과 4번의 순서가 버그였다.** 채널 목록을 먼저 만들면 `CurrentRoomId`가 아직 옛 방이라 **이전 방 채널을 다시 구독한다.** 방을 옮겼는데 옛 방 대화가 계속 들리는 증상이었다. 지금 코드는 취소 → 새 토큰 → **ID 갱신** → 채널 구성 순서다(`ChatSubscriptionService.cs:65-73`).

### 그리고 `finally`가 남아 있었다

읽기 루프가 끝날 때 `finally`에서 `Outbound.Writer.TryComplete()`를 호출하고 있었다. Outbound가 닫히면 gRPC 스트림도 끝난다 — **토큰을 분리해 놓고도 방 전환 때마다 연결이 끊겼다.**

```csharp
// ChatSubscriptionService.cs:107 — 종료 사유를 구분해서 닫는다
finally
{
    if (ctx.Cts.IsCancellationRequested)   // 연결 종료일 때만
        ctx.Outbound.Writer.TryComplete();
}
```

> **교훈** — 수명을 분리하면 **정리(cleanup) 코드도 같이 분리해야 한다.** 토큰만 나누고 `finally`를 그대로 두면, 분리한 의미가 정리 시점에 되돌아온다. "이 루프가 왜 끝났는가"를 묻지 않는 `finally`는 항상 최악을 가정한 정리를 한다.

같은 맥락으로 취소는 **에러가 아니다** — 정상 취소(`OperationCanceledException`)는 조용히 삼키고, 진짜 예외만 로그를 남긴다(`:97-103`). 구분하지 않으면 방을 옮길 때마다 에러 로그가 쌓여 진짜 장애가 묻힌다.

## 6. 재연결 복구 — 시각이 아니라 ID를 기준으로

"마지막으로 받은 시각 이후"로 복구하면 **클라와 서버의 시계가 다르다(Clock Skew).** 경계에 걸친 메시지가 중복되거나 사라진다.

기준은 **서버가 발급하는 단조 증가 MessageId**여야 한다. "1042 이후"에는 해석의 여지가 없다.

현재 흐름은 2단이다.

```
[전송]  DB INSERT (MessageId 채번 = 진실원)  →  Redis Stream 발행(실시간 전달)
[복구]  Redis Sorted Set(chat:all)에서 ID 조회  →  없으면 PostgreSQL에서 AsNoTracking 조회 후 재캐싱
```

실시간 경로(Stream)와 이력 경로(DB+캐시)를 나눈 게 요점이다. 스트림은 **지금 듣고 있는 사람**에게 빠르게 주고, 이력은 **다시 물어보는 사람**에게 정확하게 준다.

가시성 필터는 조회 결과에 서버가 적용한다 — Global 전부 + 내가 속한 방의 Room + 나와 관련된 Whisper만(`ChatService.cs:96-100`). 복구 API가 권한 검사의 뒷문이 되면 안 되기 때문이다.

## 7. Redis 키스페이스 충돌 — `WRONGTYPE`

방 스트림을 붙이자마자 `WRONGTYPE Operation against a key holding the wrong kind of value`가 떴다. 원인은 단순했다.

```
DungeonRoomRepository 방 데이터   game:room:{roomId}   ← Hash
스트림 채널 키                    game:room:{roomId}   ← 여기에 XADD  💥
```

**Redis는 키 하나에 타입 하나만 허용한다.** `FLUSHDB`로 지워도 방을 새로 만들면 Hash가 다시 생겨 재발했다 — 지우는 걸로는 절대 해결되지 않는 종류의 버그다.

해결은 이름이 아니라 **네임스페이스**였다.

```
game:room:{roomId}            ← Hash   (데이터)
stream:room:{roomId}          ← Stream (이벤트)
stream:chat:global / room:{id} / user:{nickname}
```

`stream:` 접두사를 강제 규칙으로 만들어 타입 충돌을 원천 차단했다. 키 설계는 문자열 짓기가 아니라 **타입 공간 설계**다.

## 8. 원인이 코드 밖에 있던 버그 둘

**`ERR unknown command 'XADD'`** — Docker의 Redis는 7.4인데 XADD가 없다고 했다. 컨테이너 안에서 `docker exec`로 테스트하면 성공했다. 원인은 **Windows에 설치돼 있던 Redis 3.x 서비스가 6379를 먼저 점유**하고 있었고, 앱은 그쪽에 붙고 있었던 것. 컨테이너 내부 검증과 호스트 앱 검증은 다른 것을 검증한다.

**요청 토큰을 백그라운드 루프에 넘김** — `Task.Run(() => ReadLoopAsync(ctx, list, ct))`에서 `ct`는 **그 RPC 요청의 토큰**이라, 요청이 끝나는 즉시 읽기 루프가 죽었다. 백그라운드 작업의 수명은 그것을 시작한 요청이 아니라 **그것이 봉사하는 대상(연결)** 에 묶여야 한다 → `ctx.ReadLoopCts.Token`. 5절의 수명 분리와 정확히 같은 실수의 다른 얼굴이다.

## 9. 남은 것

- **⚠️ 스트림에 트리밍이 없다** — `StreamAddAsync(channel, ...)`에 `maxLength`가 없고(`ChatBroadcastChannel.cs:18`) 스트림 키에 TTL도 없다. 개별 메시지 Hash와 인덱스 Sorted Set에는 TTL이 걸려 있지만(`ChatMessageRepository.cs:408-411`) **스트림 자체는 무한히 자란다.** 장기 가동 시 Redis 메모리를 잠식한다. `XADD ... MAXLEN ~ N`이 필요하다.
- **⚠️ 컬렉션 캐시의 부분 적중** — 이력 조회는 "Sorted Set에 ID가 하나라도 있으면 캐시를 신뢰하고 DB를 보지 않는다"(`ChatMessageRepository.cs:29-37`). 인덱스가 TTL로 만료됐다가 새 메시지 하나로 되살아난 상태에서 오래된 `afterMessageId`로 조회하면, **그 사이 이력이 조용히 비어서 반환될 수 있다.** 단건 캐시(`GetMessageByIdAsync`)는 미스 시 DB로 폴백하지만 컬렉션 조회는 전부-아니면-전무다. (※ 코드 경로상 확인. 실제 재현은 **미실측**)

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| fan-out과 경쟁 소비의 계약 분리 | 서버 간 이벤트 전부가 `IMessageQueue` Consumer Group로 수렴([05](./chapter-05-game-start-e2e.md)·[14](./chapter-14-dungeon-clear-loop.md)) |
| `stream:` 키스페이스 규칙 | 이후 추가된 모든 스트림 키의 강제 컨벤션 |
| 수명이 다르면 토큰도 다르다 | 소켓 세션·전투 루프의 취소 처리 전반([11](./chapter-11-socket-session-entry.md)) |
| 실시간 경로와 이력 경로의 분리 | 인벤토리·퀘스트 등 "이벤트 + 다시 읽기" 구조의 원형 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-04-chat.md](../learning-log/chapter-04-chat.md)

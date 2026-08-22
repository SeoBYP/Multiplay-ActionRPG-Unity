# 챕터 4 학습 로그 — 채팅 시스템 (Redis Streams + gRPC Streaming)

## 처음 알았던 것 vs 피드백으로 수정된 것

### ChatType 분리 — "그냥 다 하나로 하면 안 되나?"

**처음 내가 생각한 것:**
채팅은 그냥 메시지를 보내면 되는 거 아닌가? 굳이 Global/Room/Whisper로 나눠야 하는 이유를 몰랐음.

**피드백:**
채팅 타입을 분리하는 이유는 **전달 범위(Scope)** 가 다르기 때문.

| ChatType | 전달 대상 | 채널 |
|----------|-----------|------|
| Global | 서버 전체 유저 | `stream:chat:global` |
| Room | 같은 방에 있는 유저 | `stream:chat:room:{roomId}` |
| Whisper | 특정 1명 | `stream:chat:user:{nickname}` |

같은 채널에 다 몰아넣으면 모든 유저가 모든 메시지를 받게 됨.
채팅 타입을 나눔으로써 **불필요한 메시지 전달을 차단**하고 채널별로 독립 관리 가능.

**추가로 배운 것 — ChatType 자동 결정:**

클라이언트가 ChatType을 직접 보내는 게 아니라 **서버가 세션 상태를 보고 자동 결정**.

```csharp
var chatType =
    !string.IsNullOrWhiteSpace(targetUserNickName) ? ChatType.Whisper :
    userSession.CurrentRoomId > 0 ? ChatType.Room :
    ChatType.Global;
```

- `targetUserNickName` 있음 → Whisper
- 방에 입장 중 → Room
- 그 외 → Global

클라이언트가 타입을 조작할 여지를 서버에서 차단하는 설계.

---

### Redis Pub/Sub의 메시지 유실 문제

**처음 내가 구현한 것:**
채팅에 Redis Pub/Sub을 사용했음.

**피드백 — 왜 Pub/Sub이 문제인가:**

```
Pub/Sub의 특성:
- 메시지를 "발행" 시점에 구독 중인 구독자에게만 전달
- 구독자가 연결 끊긴 순간의 메시지는 영구 유실
- 재연결 시 누락 메시지 복구 불가
```

게임 채팅에서 연결이 잠깐 끊겼다 복구될 때 그 사이 메시지가 사라지면 사용자 경험이 나빠짐.

**수정 방향 — Redis Streams로 전환:**

| | Redis Pub/Sub | Redis Streams |
|--|---------------|---------------|
| 저장 | X (휘발) | O (디스크 영속 가능) |
| 재연결 복구 | 불가 | 마지막 ID 이후 조회 가능 |
| 구독자 수 | 1 메시지 → N명 즉시 전달 | 각자 독립 XREAD |
| 적합한 용도 | 유실 무관한 실시간 알림 | 메시지 이력이 중요한 채팅 |

---

### IBroadcastChannel vs IMessageQueue — 두 개를 나눈 이유

**처음 내가 헷갈린 것:**
채팅 메시지도 큐고, 서버 간 통신도 큐인데 왜 인터페이스를 두 개로 나누는지 몰랐음.

**피드백 — 전달 방식이 근본적으로 다름:**

```
IBroadcastChannel (채팅용)
- XADD로 스트림에 쓰기
- 각 구독자가 XREAD로 독립적으로 읽음
- 1개 메시지 → N명 모두 수신
- Consumer Group 없음

IMessageQueue (서버 간 통신용)
- XADD로 스트림에 쓰기
- Consumer Group으로 읽음
- 1개 메시지 → 1명만 처리 (경쟁 소비)
- 처리 확인(ACK) 필요
```

채팅에서 Consumer Group을 쓰면 1명만 메시지를 받아서 나머지는 못 받음.
채팅은 Broadcast, 서버 간 작업 분배는 Queue. 목적이 다르니 인터페이스도 분리.

---

### ChatStreamReader — "ChatBroadcastChannel이 있는데 왜 또 필요해?"

**처음 내가 헷갈린 것:**
ChatBroadcastChannel에서 이미 채널 읽기를 처리하는데 ChatStreamReader가 왜 따로 있는지 이해가 안 됐음.

**피드백:**

역할이 다름.

```
ChatBroadcastChannel
→ "단일 채널 1개" XADD / XREAD 처리
→ Infrastructure 구현체

ChatStreamReader
→ "여러 채널 N개"를 병렬로 읽어서 하나로 합침
→ Infrastructure → Application 경계의 매개체
```

유저는 Global + Room + Whisper 3개 채널을 동시에 구독해야 함.
채널마다 별도 Task를 생성해서 읽고, 하나의 merged Channel로 합쳐서 gRPC 스트림에 연결.

```
Global 채널 읽기 Task  ─┐
Room 채널 읽기 Task    ─┼→ merged Channel → gRPC 스트림 → 클라이언트
Whisper 채널 읽기 Task ─┘
```

ChatBroadcastChannel이 "단일 채널 구현", ChatStreamReader가 "다채널 집계 매개체".

---

### UserChatContext 설계 — Pub/Sub 필드 제거 이유

**처음 내 코드:**
`ISubscriber`, `OnRedisMessage` 델리게이트, `SubscribedChannels` 등 Pub/Sub 관련 필드들이 UserChatContext에 있었음.

**피드백 — Redis Streams로 전환 후 불필요:**

Pub/Sub에서는 Redis가 "콜백"을 통해 메시지를 밀어넣음 → 구독 객체가 컨텍스트에 있어야 했음.
Streams(XREAD)로 바꾸면 앱이 직접 폴링 → 콜백/구독 객체 자체가 필요 없어짐.

**수정 후 UserChatContext에 남은 것:**
```csharp
public Channel<ChatMessage> Outbound { get; }  // gRPC 스트림 버퍼
public CancellationTokenSource Cts { get; }    // 전체 연결 종료
public CancellationTokenSource ReadLoopCts { get; set; }  // ReadLoop 전용
```

---

### Cts vs ReadLoopCts — 왜 두 개로 나눴는가

**처음 내가 한 것:**
`SwitchRoomAsync`에서 `ctx.Cts.CancelAsync()`를 호출해서 방을 전환하려 했음.

**발생한 문제:**
`Cts`가 취소되면 클라이언트와의 **전체 연결**이 끊어짐.
방 전환인데 gRPC 연결 자체가 끊겨버리는 버그.

**수정 — 두 CTS 분리:**

```
Cts          → 연결 전체 종료 (로그아웃, 앱 종료 시)
ReadLoopCts  → ReadLoop만 종료 (방 전환 시)
```

방 전환 흐름:
```
1. ReadLoopCts.CancelAsync()  ← ReadLoop만 종료
2. ReadLoopCts = new CTS      ← 새 토큰 생성
3. ctx.CurrentRoomId = roomId ← 방 ID 업데이트
4. 새 채널로 ReadLoop 재시작  ← 새 토큰으로
```

gRPC 연결(Cts)은 살아있고, Redis 읽기 루프만 재시작.

**추가로 발견된 순서 버그:**

처음에 `ctx.CurrentRoomId = roomId`보다 채널 구성을 먼저 했더니 **기존 roomId로 채널이 구성되는 버그** 발생.
반드시 roomId 업데이트 → 채널 구성 순서여야 함.

---

### ReadLoopAsync의 finally 조건 — "SwitchRoom할 때 연결이 끊겼어요"

**발생한 문제:**
방 전환 시 `ReadLoopCts`가 취소되면 ReadLoop가 종료되면서 `finally`에서 `ctx.Outbound.Writer.TryComplete()`를 호출함.
Outbound가 Complete되면 gRPC 스트림도 종료 → 클라이언트 연결이 끊겨버림.

**수정 — 조건부 Complete:**

```csharp
finally
{
    // 전체 연결이 끊어질 때만 Outbound 닫기
    if (ctx.Cts.IsCancellationRequested)
        ctx.Outbound.Writer.TryComplete();
}
```

`Cts`(전체 연결 종료)일 때만 Outbound를 닫음.
`ReadLoopCts`(방 전환)만 취소된 경우는 Outbound를 닫지 않음 → 클라이언트 연결 유지.

---

### Message ID 기반 재연결 복구

**내가 이해한 것:**
재연결 시 누락 메시지를 복구하려면 기준점이 필요하다.

**피드백 — DateTime보다 MessageId가 적합한 이유:**

```
DateTime 기반 복구의 문제:
- 클라이언트와 서버 시계가 다를 수 있음 (Clock Skew)
- "2025-03-15 14:23:01.234 이후"로 조회하면 경계 메시지 중복/유실 가능

MessageId(long) 기반 복구:
- 서버가 발급, 단조 증가 보장
- "MessageId 1042 이후" = 명확한 경계
- Redis Stream ID도 타임스탬프-시퀀스 형태로 단조 증가
```

클라이언트가 마지막으로 받은 MessageId를 저장해두고, 재연결 시 그 ID를 서버에 전달.
서버는 `GetMessagesAfterAsync(afterMessageId)`로 그 이후 메시지만 조회해서 전달.

---

## 코드 리뷰에서 발견된 버그 수정 이력

### ChatBroadcastChannel — while 조건 오류

**문제:**
```csharp
// 수정 전 (버그)
while (Database.ListLength > 0)  // Stream인데 List 조건 사용
```

Streams를 쓰는데 List 조건으로 읽기 루프를 돌리고 있었음 → 처음부터 읽기가 안 됨.

**수정:**
```csharp
while (!ct.IsCancellationRequested)
```

---

### ChatBroadcastChannel — entry.ToString() 역직렬화 오류

**문제:**
```csharp
// 수정 전 (버그)
var json = entry.ToString();  // StreamEntry 전체를 문자열로
```

`StreamEntry.ToString()`은 디버그 표현 문자열 → JSON이 아니라 역직렬화 실패.

**수정:**
```csharp
var json = entry[EntryId].ToString();  // 필드 이름으로 값 추출
```

---

### ChatBroadcastChannel — lastId 인스턴스 필드로 Race Condition

**문제:**
```csharp
// 수정 전 (버그)
private string _lastId;  // 인스턴스 필드

public async IAsyncEnumerable<...> ReadAsync(...)
{
    while (!ct.IsCancellationRequested)
    {
        var entries = await Database.StreamReadAsync(channel, _lastId);
        // ...
        _lastId = entry.Id;  // 여러 유저가 공유하는 필드를 동시에 덮어씀
    }
}
```

여러 유저가 동시에 ReadAsync를 호출하면 `_lastId`를 서로 덮어써서 엉뚱한 위치부터 읽는 버그.

**수정:**
```csharp
var currentId = lastMessageId;  // 파라미터를 로컬 변수로 복사
// ...
currentId = entry.Id;  // 로컬 변수 업데이트
```

로컬 변수는 호출마다 독립적 → Race Condition 없음.

---

### ChatPublisher — broadcastChannel 미사용

**문제:**
`IBroadcastChannel<ChatMessage>`를 주입받았는데 실제로는 기존 Pub/Sub 코드가 그대로 남아있었음.

**수정:**
```csharp
public async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct)
{
    await broadcastChannel.PublishAsync(channel, message, ct);
}
```

---

### ConnectAsync ReadLoop CancellationToken 오류

**문제:**
```csharp
// 수정 전 (버그)
_ = Task.Run(() => ReadLoopAsync(ctx, list, ct));  // 요청 단발성 토큰 전달
```

`ct`는 `ConnectAsync` HTTP 요청의 토큰 → 요청이 끝나면 취소됨.
ReadLoop가 요청 종료 즉시 죽어버리는 버그.

**수정:**
```csharp
_ = Task.Run(() => ReadLoopAsync(ctx, list, ctx.ReadLoopCts.Token));
```

ReadLoop는 연결이 살아있는 동안 계속 돌아야 함 → ReadLoopCts 사용.

---

### Redis Keyspace 충돌 — WRONGTYPE 오류

**발생 상황:**
`SubscribeRoom` 요청 시 `WRONGTYPE Operation against a key holding the wrong kind of value` 오류.
`DungeonRoomBroadcastChannel.ReadAsync`와 `PublishAsync` 모두에서 발생.

**원인:**
스트림 채널 키 이름이 기존 레포지토리 데이터 키와 충돌.

```
DungeonRoomRepository 방 데이터 (Hash): "game:room:{roomId}"
RoomChannels.RoomChannel(roomId) 스트림:  "game:room:{roomId}"  ← 동일한 키!
```

방 생성 시 `game:room:1`에 Hash로 데이터 저장 → 이후 `game:room:1`에 XADD 시도 → WRONGTYPE.
`FLUSHDB`로 초기화해도 방을 새로 만들면 Hash가 다시 생겨서 반복 발생.

**확인 방법:**
```bash
docker exec gameserver-redis redis-cli KEYS "game:room:*"
# game:room:1 (Hash), game:room:1:players (Set) 이 이미 존재
```

**해결 — `stream:` 접두사로 Keyspace 완전 분리:**

```
# 수정 전: 레포지토리 키와 혼재
game:room:{roomId}           ← Hash (방 데이터)
game:room:{roomId}:events    ← Stream (스트림 채널)

# 수정 후: stream: 접두사로 분리
game:room:{roomId}           ← Hash (방 데이터)
stream:room:{roomId}         ← Stream (던전 로비 이벤트)
stream:chat:global           ← Stream (전역 채팅)
stream:chat:room:{roomId}    ← Stream (방 채팅)
stream:chat:user:{nickname}  ← Stream (귓속말)
```

**배운 것:**
Redis는 하나의 키에 하나의 타입만 허용.
스트림 전용 키는 `stream:` 접두사로 Keyspace를 명시적으로 분리해야 타입 충돌 원천 차단 가능.
레포지토리 키 설계 시 데이터 타입별로 네임스페이스를 미리 구분하는 게 중요.

---

### Redis 연결 오류 — Windows 로컬 Redis 충돌

**발생 상황:**
`ERR unknown command 'XADD'` 오류 발생.
Docker Redis는 7.4.7인데 왜 XADD가 안 되는지 의문이었음.

**원인:**
Windows에 직접 설치된 Redis 3.x 서비스가 `6379` 포트를 먼저 점유하고 있었음.
앱이 Docker Redis(7.4.7)가 아닌 로컬 Windows Redis(3.x)에 연결되고 있었던 것.

`docker exec`로 XADD 테스트가 성공한 이유:
→ Docker 컨테이너 내부에서 직접 연결하면 Windows 포트 충돌을 우회하기 때문.

**확인 방법:**
```powershell
netstat -ano | findstr :6379  # 6379 포트 점유 프로세스 확인
```

**해결:**
Windows Redis 서비스 중지 (관리자 권한 필요):
```powershell
net stop Redis
```

또는 Docker Redis를 다른 포트로 변경:
```yaml
# docker-compose.yml
ports:
  - "6380:6379"
```

**배운 것:**
Docker 컨테이너 내부에서 동작 확인과 호스트 앱에서 동작 확인은 다를 수 있음.
같은 포트를 로컬 서비스와 Docker가 경쟁할 때 Docker 포트 포워딩이 밀릴 수 있음.

---

## 현재 코드에서 아직 미완성인 것 (TODO)

| 항목 | 내용 | 우선순위 |
|------|------|----------|
| `OperationCanceledException` 분리 | ReadLoopAsync에서 정상 취소(방 전환/종료)를 Console.WriteLine 노이즈 없이 처리 | 중간 |
| `Console.WriteLine` → `ILogger` | ChatBroadcastChannel, ChatSubscriptionService, ChatService 전체 | 중간 |
| TODO 주석 제거 | ChatSubscriptionService의 Pub/Sub 관련 구식 주석 | 낮음 |
| `GameStartedEvent` IP/Port | 하드코딩 `"127.0.0.1"`, `12345` → `appsettings.json`으로 분리 | 높음 |
| Redis Stream 만료 설정 | XADD `MAXLEN` 옵션으로 스트림 크기 제한 (메모리 관리) | 중간 |
| 재연결 복구 API | 클라이언트 재연결 시 `GetMessagesAfterAsync`로 누락 메시지 조회 흐름 완성 | 높음 |
| GameServer ↔ SocketServer 통신 | `IMessageQueue` 기반 서버 간 이벤트 전달 (Consumer Group 방식) | 높음 |

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Redis Streams | XADD로 쓰고 XREAD로 읽는 지속형 메시지 스트림 (Redis 5.0+) |
| Redis Pub/Sub | 발행 시점에 구독자에게만 전달, 저장 없음, 유실 가능 |
| XADD | Redis Stream에 메시지 추가, 자동 ID 생성 |
| XREAD | 특정 ID 이후 메시지 읽기, 각 구독자가 독립 실행 |
| Consumer Group | 1개 메시지를 1명만 처리하는 경쟁 소비 방식 (서버 간 통신용) |
| IBroadcastChannel | 1 메시지 → N명 모두 수신하는 브로드캐스트 인터페이스 |
| IMessageQueue | Consumer Group 방식 큐, 서버 간 작업 분배용 |
| ChatStreamReader | 여러 채널을 병렬 XREAD 후 하나로 합쳐주는 매개체 |
| UserChatContext | 유저별 gRPC 스트림 버퍼(Outbound)와 CTS 관리 |
| Cts vs ReadLoopCts | 전체 연결 종료(Cts)와 ReadLoop 재시작(ReadLoopCts) 분리 |
| 팬아웃 (Fan-out) | 1개 메시지를 N명에게 전달할 때 발생하는 브로드캐스트 비용 |
| MessageId 기반 복구 | DateTime(Clock Skew 위험) 대신 단조 증가 ID로 재연결 기준점 설정 |
| Race Condition (로컬 변수) | 인스턴스 필드 공유 → 로컬 변수 복사로 동시성 버그 해결 |
| Docker 포트 충돌 | 로컬 서비스와 Docker가 같은 포트 경쟁 시 연결 대상이 달라질 수 있음 |
| Redis Keyspace 분리 | `stream:` 접두사로 스트림 채널 키와 데이터 키의 타입 충돌 원천 차단 |
| WRONGTYPE | Redis 키에 다른 타입으로 접근 시 발생하는 오류 (키당 하나의 타입만 허용) |

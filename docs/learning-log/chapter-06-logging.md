# Chapter 06 — 분산 로그 시스템 (Serilog + Graylog)

## 설계 배경 (Why)

서버가 하나일 때는 콘솔 로그로 충분하다. 하지만 GameServer + SocketServer가 분리되는 순간 문제가 생긴다.

```
클라이언트 → GameServer (StartRoom 요청)
              → Redis MQ 발행
                → SocketServer (Room 생성)
                  → 클라이언트 TCP 접속
```

이 흐름에서 버그가 생기면 어떻게 추적하나?
- GameServer 로그와 SocketServer 로그가 **별개 파일**
- 어느 요청이 어느 처리로 이어졌는지 **연결고리 없음**
- 서비스가 늘어날수록 로그 파일도 늘어남

**해결**: 모든 서비스에 동일한 TraceId를 흘려보내고, 중앙에서 수집한다.

---

## 아키텍처

```
GameServer (gRPC)                    SocketServer (TCP)
    │                                      │
    │  AuthInterceptor                     │  GameStart MQ 소비
    │  TraceId 생성/전파                    │  msg.TraceId 꺼냄
    │  LogContext.PushProperty             │  LogContext.PushProperty
    │                                      │
    └──────────── Redis MQ ───────────────→
                 GameStartMessage
                 { RoomId, PlayerIds, TraceId }

         ↓ (두 서비스 모두)
       Serilog
       ├── ConsoleSink   → 개발 중 터미널
       ├── FileSink      → logs/*.log (날짜별 롤링)
       └── GraylogSink   → UDP 12201
                ↓
           Graylog (Docker)
           OpenSearch + MongoDB
                ↓
          Web UI :9000
          TraceId로 전체 흐름 추적
```

---

## LogProxy 패턴 = Serilog Sink

직접 설계하면 이런 구조가 된다:

```csharp
// 내가 설계한 것
interface ILogSink { void Write(LogEvent e); }
class LogProxy : ILogger {
    List<ILogSink> _sinks;
    void Log(...) => _sinks.ForEach(s => s.Write(e));
}
class ConsoleSink : ILogSink { ... }
class FileSink    : ILogSink { ... }
class GraylogSink : ILogSink { ... }
```

Serilog가 이 패턴을 그대로 구현한 라이브러리다.

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(...)   // ConsoleSink
    .WriteTo.File(...)      // FileSink
    .WriteTo.Graylog(...)   // GraylogSink (GELF UDP)
    .CreateLogger();
```

직접 구현하면 설계 의도를 보여줄 수 있고, Serilog를 쓰면 업계 표준 지식을 보여준다. **포트폴리오에서는 설계 의도를 설명하고 Serilog로 구현**하는 게 가장 효과적이다.

---

## 핵심 구현: TraceId 전파

### 1. gRPC 요청 진입점 — AuthInterceptor

```csharp
// 클라이언트가 x-trace-id 헤더를 보내면 재사용, 없으면 신규 생성
var traceId = context.RequestHeaders.GetValue("x-trace-id")
              ?? Guid.NewGuid().ToString("N")[..8];

// 이 요청의 모든 로그에 TraceId 자동 첨부 (AsyncLocal 기반)
using (LogContext.PushProperty("TraceId", traceId))
using (LogContext.PushProperty("Method", methodName))
{
    // 서비스 레이어까지 명시적으로 전달할 수 있도록 Items에도 저장
    context.GetHttpContext().Items["TraceId"] = traceId;
    return await continuation(request, context);
}
```

**왜 Items에도 저장하나?**
`LogContext.PushProperty`는 로그 출력에만 자동 첨부된다.
Redis MQ를 통해 다른 프로세스로 TraceId를 넘기려면 **값을 명시적으로 꺼내서 메시지에 실어야** 한다.

### 2. MQ 발행 — GameStartMessage에 TraceId 포함

```csharp
// DungeonLobbyGrpcService
var traceId = context.GetHttpContext().Items["TraceId"] as string ?? "";

// DungeonLobbyService
await gameStartPublisher.PublishAsync(new GameStartMessage
{
    RoomId = roomId,
    PlayerIds = room.CurrentPlayers.ToList(),
    TraceId = traceId   // ← Redis 건너가는 TraceId
}, ct);
```

### 3. SocketServer — MQ 소비 시 TraceId 복원

```csharp
await foreach (var msg in gameStartQueue.DequeueAllAsync(cts.Token))
{
    using (LogContext.PushProperty("TraceId", msg.TraceId))
    using (LogContext.PushProperty("RoomId", msg.RoomId))
    {
        logger.LogInformation("[GameStart] Players={PlayerCount}명", msg.PlayerIds.Count);
        // 이 블록의 모든 로그에 TraceId 자동 첨부
    }
}
```

---

## 구조적 로깅 vs 문자열 보간

```csharp
// ❌ 문자열 보간 — Graylog에서 Method 필드로 검색 불가
logger.LogInformation($"gRPC 요청: {methodName}");

// ✅ 구조적 로깅 — Graylog에서 Method 필드로 필터링 가능
logger.LogInformation("gRPC 요청: {Method}", methodName);
```

구조적 로깅을 쓰면 Graylog에서 `Method:StartRoom` 같은 필드 검색이 가능하다.

**보안 주의:**
```csharp
// ❌ 인증 헤더를 로그에 남기면 토큰 노출
logger.LogWarning($"잘못된 형식: {authHeader}");

// ✅ 값은 제외, 사실만 기록
logger.LogWarning("잘못된 Authorization 형식");
```

---

## Docker Compose 구성

Graylog는 **OpenSearch(로그 저장) + MongoDB(설정 저장) + Graylog(수집/UI)** 3개가 필요하다.

```yaml
mongodb:
  image: mongo:7.0

opensearch:
  image: opensearchproject/opensearch:2.12.0
  environment:
    - discovery.type=single-node
    - DISABLE_SECURITY_PLUGIN=true

graylog:
  image: graylog/graylog:5.2
  environment:
    GRAYLOG_ELASTICSEARCH_HOSTS: "http://opensearch:9200"
    GRAYLOG_MONGODB_URI: "mongodb://mongodb:27017/graylog"
  ports:
    - "9000:9000"       # Web UI
    - "12201:12201/udp" # GELF UDP 수신
```

**Graylog 6.0 주의**: 6.0부터 자체 Data Node 아키텍처로 변경됨.
OpenSearch/Elasticsearch를 직접 연결하려면 **5.2 사용 권장**.

---

## 로그 흐름 예시

`TraceId=a3f9b21c`로 필터링하면:

```
[12:00:01 INF] TraceId=a3f9b21c Method=StartRoom  gRPC 요청: /lobby.DungeonLobby/StartRoom
[12:00:01 INF] TraceId=a3f9b21c Method=StartRoom  인증 성공
[12:00:01 INF] TraceId=a3f9b21c Method=StartRoom  StartRoom request received
[12:00:01 INF] TraceId=a3f9b21c                   GameStart MQ 발행
                    ↓ Redis MQ (프로세스 경계)
[12:00:01 INF] TraceId=a3f9b21c RoomId=1          [GameStart] Players=2명  ← SocketServer
[12:00:01 INF] TraceId=a3f9b21c RoomId=1          Room 생성 완료
```

두 서버의 로그가 하나의 TraceId로 연결된다.

---

## 시니어 리뷰

### 현재 구현의 한계

**TraceId가 TCP 세션까지 이어지지 않음:**
현재 클라이언트 TCP 접속 이후의 로그(C_Auth, PingPong 등)에는 TraceId가 없다.
완전한 추적을 위해서는 C_Auth 패킷에 TraceId를 포함시키거나,
SocketServer가 GameStart MQ의 TraceId를 Room에 저장해서 이후 처리에 전달해야 한다.

**UDP 로그 유실 가능성:**
GELF UDP는 패킷 유실 가능성이 있다. 고가용성이 필요하면 GELF TCP 또는 HTTP를 사용해야 한다.
개발/포트폴리오 환경에서는 UDP로 충분하다.

**OpenTelemetry로 발전:**
현재 구현은 수동 TraceId 전파 방식이다.
업계 표준인 OpenTelemetry(OTel)를 적용하면:
- TraceId/SpanId 자동 생성 및 전파
- gRPC 헤더에 자동으로 `traceparent` 첨부
- Jaeger/Grafana Tempo 등 표준 플랫폼과 연동

현재 구현은 OTel의 원리를 직접 구현해본 것으로 이해하면 된다.

---

## 다음 단계

- [ ] PostgreSQL 연동 (SocketServer DB 기반 유저 데이터 초기화)
- [ ] TCP 세션까지 TraceId 전파 (C_Auth 패킷에 TraceId 포함)
- [ ] OpenTelemetry 마이그레이션 검토

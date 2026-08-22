# 06. 분산 로그 — 프로세스 경계를 넘는 추적

> **한 줄** — 서버가 둘이 되는 순간 로그는 **두 개의 끊어진 이야기**가 된다. 한 요청에 TraceId를 붙여 두 서버의 로그를 한 줄로 꿰고, 중앙(Graylog)에서 그 ID로 되감을 수 있게 만들었다. 핵심 난점은 **암묵적 컨텍스트가 프로세스 경계를 넘지 못한다**는 것이었다.
>
> **범위** Sink 구조 · TraceId 전파 · 구조적 로깅 · Graylog 구성
> **한계** TCP 세션 구간 미전파 · GELF UDP 유실 가능 · OpenTelemetry 미도입

---

## 1. 왜 중앙 로그인가 — 흐름이 파일 경계에서 끊긴다

```
클라 ── StartRoom ──▶ GameServer ── Redis Stream ──▶ SocketServer ──▶ 클라 TCP 접속
                     └ gameserver-*.log            └ socketserver-*.log
                            ↑                              ↑
                       여기까지 보이고                  여기부터 다시 시작
                       "이 요청이 저 처리로 이어졌다"는 연결고리가 없다
```

서버가 하나면 콘솔로 충분하다. 둘이 되면 **"이 버그가 어디서 시작됐나"를 사람이 시각으로 맞춰야** 한다. 로그가 많아질수록 이건 불가능해진다.

필요한 건 로그를 모으는 것(수집)만이 아니라, **모은 로그에서 한 흐름만 골라낼 수 있는 열쇠**(상관 ID)다. 둘 중 하나만 있으면 쓸모가 없다 — 모으기만 하면 더 큰 건초더미가 되고, ID만 있으면 여전히 파일 두 개를 뒤져야 한다.

## 2. 구조 — 직접 설계한 것이 이미 표준이었다

"여러 출력 대상에 같은 로그를 뿌린다"를 직접 설계하면 이렇게 된다.

```csharp
interface ILogSink { void Write(LogEvent e); }
class LogProxy : ILogger {
    List<ILogSink> _sinks;
    void Log(...) => _sinks.ForEach(s => s.Write(e));   // 팬아웃
}
```

Serilog가 **정확히 이 패턴**이다. `LogProxy` → `Logger`, `ILogSink` → `ISink`.

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()                 // ★ 앰비언트 속성 자동 첨부
    .WriteTo.Console(outputTemplate: "... TraceId={TraceId} ...")
    .WriteTo.File("logs/gameserver-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Graylog(new GraylogSinkOptions { Port = 12201, TransportType = TransportType.Udp })
    .CreateLogger();
```

세 싱크가 각각 다른 독자를 담당한다 — **콘솔**은 개발 중인 나, **파일**은 컨테이너 안에서 바로 볼 수 있는 최후의 수단, **Graylog**는 두 서버를 가로질러 검색하는 용도. 하나로 합치려 하면 어느 하나가 불편해진다.

```
GameServer ─┐
            ├─ GELF/UDP 12201 ─▶ Graylog ─▶ OpenSearch(로그) + MongoDB(설정) ─▶ Web UI :9000
SocketServer┘
```

## 3. 핵심 — 암묵적 전파와 명시적 전파는 다른 문제다

TraceId 전파는 두 구간으로 나뉘고, **각각 다른 기법**이 필요하다.

### 구간 A: 프로세스 안 — 앰비언트 컨텍스트

```csharp
// AuthInterceptor.cs:22 — 클라가 보낸 것 재사용, 없으면 생성
var traceId = context.RequestHeaders.GetValue("x-trace-id") ?? Guid.NewGuid().ToString("N")[..8];

using (LogContext.PushProperty("TraceId", traceId))   // AsyncLocal 기반
{
    context.GetHttpContext().Items["TraceId"] = traceId;   // ← 왜 이게 또 필요한가?
    return await continuation(request, context);
}
```

`LogContext.PushProperty`는 `AsyncLocal`이라 **이 요청에서 파생된 모든 async 호출의 로그에 자동으로 붙는다.** 서비스·리포지토리에 TraceId 파라미터를 뚫을 필요가 없다.

### 구간 B: 프로세스 밖 — 값을 실어 보내야 한다

여기가 함정이다. **`AsyncLocal`은 Redis Stream을 건너가지 않는다.** 앰비언트 컨텍스트는 같은 실행 흐름 안에서만 유효하다.

```
[GameServer 프로세스]                          [SocketServer 프로세스]
LogContext(TraceId=a3f9)  ─── Redis ───▶   LogContext(비어 있음)
     ↑ 로그에 자동으로 붙음                       ↑ 자동으로는 아무것도 안 붙는다
```

그래서 TraceId를 **메시지 본문의 필드로 승격**시킨다. 그것이 `Items["TraceId"]`의 존재 이유다 — 로그용 컨텍스트에서 값을 다시 꺼내 데이터로 만들어야 한다.

```
GameStartRequestedMessage { RoomId, PlayerInfos, MapId, TraceId }   ← 페이로드에 실린다
        │
        ▼  SocketServer 소비 시 컨텍스트로 복원
using (LogContext.PushProperty("TraceId", msg.TraceId))
using (LogContext.PushProperty("RoomId", msg.RoomId))
{ ... }   // 이 블록 안의 모든 로그에 다시 자동 첨부
```

> **정리** — 앰비언트 컨텍스트는 **경계 안에서 편하고, 경계에서 끊긴다.** 경계를 넘길 때는 반드시 "꺼내서 → 실어서 → 다시 세운다"는 3단계가 필요하다. 이 사실을 모르면 "분명히 PushProperty 했는데 저쪽 로그엔 왜 없지?"에서 오래 헤맨다.

결과적으로 두 서버의 로그가 한 ID로 이어진다.

```
[12:00:01 INF] TraceId=a3f9b21c Method=StartRoom  gRPC 요청
[12:00:01 INF] TraceId=a3f9b21c Method=StartRoom  인증 성공
[12:00:01 INF] TraceId=a3f9b21c                   Outbox 기록
        ─────────────── Redis Stream (프로세스 경계) ───────────────
[12:00:01 INF] TraceId=a3f9b21c RoomId=1          [GameStart] Players=2명   ← SocketServer
[12:00:01 INF] TraceId=a3f9b21c RoomId=1          GameSessionReady 발행
```

## 4. 구조적 로깅 — 문자열을 만들면 검색이 죽는다

```csharp
logger.LogInformation($"gRPC 요청: {methodName}");     // ❌ 완성된 문자열 한 덩어리
logger.LogInformation("gRPC 요청: {Method}", methodName); // ✅ Method 필드가 살아서 전달
```

둘은 콘솔에서 **똑같이 보이지만** Graylog에서는 다르다. 후자만 `Method:StartRoom`으로 필터링된다. 로그를 "읽는 글"이 아니라 **"질의할 데이터"** 로 취급하는 것이 구조적 로깅이다.

같은 이유로 **로그에 무엇을 넣지 않을지도 설계**다.

```csharp
logger.LogWarning($"잘못된 형식: {authHeader}");   // ❌ 토큰이 로그·Graylog·디스크에 영구 기록
logger.LogWarning("잘못된 Authorization 형식");     // ✅ 사실만
```

인증 정보를 로그에 남기면 **로그 저장소가 새로운 공격면**이 된다. 로그는 지우기도 어렵고 여러 곳에 복제된다.

## 5. Graylog 구성에서 걸린 것

Graylog는 단독 컨테이너가 아니라 **3개 한 세트**다 — Graylog(수집·UI) + OpenSearch(로그 저장) + MongoDB(설정 저장).

버전은 **5.2로 고정**했다. 6.0부터 자체 Data Node 아키텍처로 바뀌어 OpenSearch를 직접 붙이는 구성이 깨지기 때문이다. 최신이 항상 정답이 아니라, **내 구성과 맞물리는 버전**이 정답이다.

## 6. 알려진 한계 (그리고 왜 지금은 이대로 두는가)

**① TCP 세션 구간에는 TraceId가 없다**
전파가 닿는 곳은 gRPC 요청 → Redis 메시지 → SocketServer 소비까지다. 그 이후 클라가 TCP로 붙어서 주고받는 로그(입장·이동·전투)에는 TraceId가 없다. 완결하려면 방이 TraceId를 보관하고 세션 로그에 이어 붙여야 한다.

대신 그 구간은 **`SessionId`로 묶고 있다** — 다만 이건 앰비언트가 아니다. 콘솔 출력 템플릿이 `SessionId={SessionId}`를 찍도록 돼 있지만 `LogContext.PushProperty("SessionId", ...)`를 하는 곳이 없어서, **21곳의 호출부가 메시지 템플릿에 직접 `{SessionId}`를 써넣고 있다.** 빠뜨린 로그는 그냥 빈칸이 된다. 3절에서 TraceId에 적용한 앰비언트 기법을 세션에는 적용하지 않은 셈이다.

**② GELF UDP는 유실될 수 있다**
UDP라 로그가 조용히 사라질 수 있다. 감사(audit)나 과금 로그라면 TCP/HTTP로 가야 하지만, 이 프로젝트의 로그 용도는 **디버깅**이므로 유실 몇 건보다 애플리케이션에 부하를 주지 않는 쪽이 낫다고 판단했다.

**③ OpenTelemetry 미도입** (`opentelemetry`/`traceparent` 참조 0건)
지금 구현은 OTel이 표준화한 것 — TraceId 생성·헤더 전파·컨텍스트 복원 — 을 **손으로 만든 것**이다. OTel로 가면 `traceparent` 헤더가 자동으로 붙고 Jaeger/Tempo 같은 표준 백엔드에 연결된다. 다만 지금 규모에서 얻는 것보다 도입 비용이 커서 미뤘고, **직접 구현해 본 덕에 OTel이 무엇을 대신해 주는지 알게 됐다**는 게 남았다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 구조적 로깅(필드로 질의) | 전투 계측 `CombatTrace`가 서버 구조적 로그를 그대로 활용([26](./chapter-26-measured-combat-cleanup.md)) |
| 경계를 넘길 땐 페이로드에 싣는다 | 이후 모든 서버 간 메시지가 `TraceId` 필드를 포함 |
| 실패를 조용히 두지 않는다 | 컨슈머 복원력·실패 분류([05](./chapter-05-game-start-e2e.md)) · 조용한 실패 사냥([27](./chapter-27-silent-failure.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-06-logging.md](../learning-log/chapter-06-logging.md)

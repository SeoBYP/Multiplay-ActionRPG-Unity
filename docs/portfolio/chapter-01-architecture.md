# 01. 이중 서버 아키텍처 — 왜 서버를 하나로 만들지 않았는가

> **한 줄** — 로비와 인게임은 연결 패턴·상태 보유·스케일 단위가 근본적으로 달라서, 하나의 서버로 묶으면 둘 중 하나가 반드시 손해를 본다. 그래서 **Stateless GameServer(gRPC/HTTP) + Stateful SocketServer(TCP)** 로 나누고, 둘 사이를 **Redis Streams 단방향 이벤트**로만 결합했다.
>
> **범위** 서버 토폴로지 · 프로토콜 3종 선택 · 서버 간 결합 방식 · Clean Architecture 의존 방향
> **핵심 결과** 서버 간 직접 RPC 0건 · 인게임 패킷은 4바이트 헤더만 · Application → Infrastructure 참조 0건

---

## 1. 문제 — 한 서버에 담기지 않는 두 종류의 트래픽

이 게임에는 성격이 정반대인 두 트래픽이 있다.

| | 로비 계열 (인증·방·채팅·상점) | 인게임 계열 (입장·이동·전투) |
|---|---|---|
| 연결 패턴 | 요청–응답, 필요할 때만 | 상시 연결 유지 |
| 빈도 | 사용자 행동당 1회 | 초당 수십~수백 패킷 |
| 상태 | 없음 (DB/Redis가 진실원) | 방 단위 게임 상태를 메모리에 보유 |
| 지연 허용 | 수백 ms 무방 | 수십 ms |
| 스케일 단위 | 인스턴스 (수평 확장 자유) | **방** (같은 방 플레이어는 같은 프로세스여야 함) |

마지막 줄이 결정적이다. 로비는 아무 인스턴스가 받아도 되지만, 인게임은 **같은 방의 플레이어가 반드시 같은 프로세스에 모여야** 한다. 이 둘을 한 서버에 두면 스케일 정책이 충돌한다 — 로비 부하로 인스턴스를 늘리는 순간 방이 쪼개진다.

## 2. 선택지와 기각 근거

| 안 | 내용 | 기각 사유 |
|---|---|---|
| A | 단일 서버, 전부 gRPC | 인게임 패킷마다 HTTP/2 프레임·헤더 오버헤드가 누적. 초당 수백 패킷 구간에서 페이로드보다 부대비용이 커진다 |
| B | 단일 서버, 전부 TCP 직접 구현 | 인증·방 CRUD·상점까지 자체 프로토콜을 설계해야 함. 스키마 계약(proto)·툴링(Swagger)·코드 생성을 전부 포기 |
| C | gRPC + WebSocket 추가 | gRPC 스트리밍으로 이미 커버되는 영역을 두 기술로 중복 구현 → 운영·유지보수 부담만 증가 |
| **D** | **이중 서버 (채택)** | 각 트래픽에 맞는 프로토콜·스케일 정책을 독립적으로 가져간다. 대가는 **서버 간 통신 설계**라는 새 문제 하나 |

> D의 대가를 정면으로 받은 것이 4절(Redis Streams)이다. 아키텍처 선택은 문제를 없애는 게 아니라 **다루기 쉬운 문제로 바꾸는 것**이라는 걸 여기서 배웠다.

## 3. 채택 구조

```
                     ┌──────────────────────────────────────┐
                     │            Unity Client              │
                     └───────┬──────────────────────┬───────┘
              gRPC :5132     │                      │   TCP :7777
       (인증·로비·채팅·상점)  │                      │  (입장·이동·전투)
                             ▼                      ▼
              ┌───────────────────────┐   ┌────────────────────────┐
              │  GameServer  (STATELESS)│   │ SocketServer (STATEFUL)│
              │  API → Application      │   │ Session / Room /       │
              │        ↑ Infrastructure │   │ RoomTickService(10Hz)  │
              │  HTTP :5131 = Admin API │   │ 4B length-prefix       │
              └───────┬───────────────┘   └───────────┬────────────┘
                      │                                │
                      │      Redis Streams (단방향)     │
                      ├───── stream:game:start ────────▶│
                      │◀──── stream:game:dungeon:result │
                      │                                │
                      ▼                                ▼
              PostgreSQL + Redis              (방 메모리 게임 상태)
                 진실원 · 캐시
```

**불변식 하나** — 두 서버는 서로를 **직접 호출하지 않는다**. 모든 교차는 Redis Stream 메시지다. 이걸 규칙으로 못 박아 두니(`CLAUDE.md` 금지 목록) 이후 기능이 늘어도 토폴로지가 흐트러지지 않았다.

## 4. 프로토콜을 셋 쓴 이유 (각각의 값어치)

### gRPC :5132 — 로비 계열 전부
- HTTP/1.1의 Head-of-Line Blocking을 HTTP/2 멀티플렉싱으로 해소.
- Protobuf 스키마가 **클라–서버 계약서** 역할. `.proto` 하나에서 양쪽 코드를 생성하므로 필드 불일치가 컴파일 타임에 잡힌다.
- **서버 스트리밍**을 공짜로 얻는다 → 방 목록 실시간 갱신(`SubscribeRoom`)을 폴링 없이 구현.

### TCP :7777 — 인게임 전부
- 프레이밍은 **4바이트 길이 프리픽스 + MemoryPack 페이로드**. 헤더가 4바이트면 이동 패킷 1개의 부대비용이 사실상 무시된다.
- 수신 측은 길이를 먼저 읽고 유효성을 검사한 뒤 본문을 읽는다 (`Session.cs:87`). TCP는 스트림이라 "메시지 경계"를 프로토콜이 직접 정의해야 하고, 이 검사가 없으면 잘못된 길이 하나가 이후 스트림 전체를 어긋나게 만든다.

### HTTP :5131 — 운영 도구
처음엔 이 포트를 "Swagger 테스트용"이라고만 설명했는데, 그건 **운영 환경에서 포트를 열어둘 이유가 되지 못한다**는 지적을 받았다. 지금은 역할이 명확하다 — `AdminController`(`api/admin`)가 Redis 상태 조회와 방/세션 초기화를 제공한다. 게임 프로토콜과 무관한 운영 조작은 게임 채널이 아니라 별도 평면에 있어야 한다.

> 정직하게 남기면: **헬스체크 엔드포인트는 아직 없다**(`MapHealthChecks` 미등록). 컨테이너 오케스트레이션을 붙이는 시점에 필요해진다.

## 5. 서버 간 결합 — Kafka를 쓰지 않은 이유

설계 초기에 "Redis Streams vs Kafka"를 미결로 남겼고, 최종적으로 **Redis Streams**를 택했다.

- **인프라 추가 비용 0** — 세션·캐시·방 상태로 Redis를 이미 운영 중이었다. Kafka는 이 규모에서 얻는 것보다 운영 표면적 증가가 크다.
- **Consumer Group**으로 경쟁 소비(작업 분배)와 fan-out(채팅 브로드캐스트)을 같은 인프라에서 구분해 쓸 수 있다.
- 소비 시작점을 `Beginning("0")`으로 고정 — 서버 재시작 시 미처리 메시지를 재처리한다. `NewMessages("$")`는 재시작 창에 발행된 메시지를 통째로 잃으므로 **금지 규칙**으로 명시했다.

## 6. 교정된 오해 — Clean Architecture는 DIP를 "줄이는" 게 아니다

초기에 레이어 분리 이유를 *"의존성 역전 현상을 줄이려고"* 라고 설명했는데, 이건 방향이 거꾸로다. Clean Architecture는 DIP를 **적극적으로 적용하는** 구조다. 고수준(Application)이 저수준(Infrastructure)에 의존하지 않도록, 상위가 인터페이스를 선언하고 하위가 거기에 맞춰 구현한다.

그리고 이 오해는 실제 코드에 흔적을 남기고 있었다 — `IUserRepository`가 Infrastructure에 있었다.

```
[수정 전]  Application ──────▶ Infrastructure   (인터페이스가 저수준에 있으니 참조가 아래로 흐름 = DIP 위반)
[수정 후]  Application ◀────── Infrastructure   (Application이 선언, Infrastructure가 구현)
           └ Domains/User/Repositories/IUserRepository.cs
```

`AuthService`의 `using GameServer.Infrastructure.*`를 전부 제거해 참조를 끊었다. 이후 이 방향은 프로젝트 전체의 강제 규칙이 됐고, 현재 `GameServer.Application.csproj`에 `GameServer.Infrastructure` 참조는 없다.

## 7. 남은 것 / 지금 다시 한다면

- **헬스체크 미구현** — 5131을 운영 평면으로 정의했으면 헬스체크도 여기 있어야 한다.
- **SocketServer 다중 인스턴스 라우팅 미설계** — 지금은 단일 인스턴스 전제다. 방 단위 스케일이라는 성질상 확장 시 `RoomId → 인스턴스` 라우팅 계층이 필요하다.
- **`Shared.Infrastructure` 네이밍이 오해를 부른다** — 이름과 달리 실제 내용은 임베디드 JSON 정적 기획 카탈로그(아이템·어빌리티·드롭테이블·스폰)와 메시지 계약이다. DB 어댑터가 아니므로 Application이 참조해도 DIP 위반은 아니지만, 이름이 규칙("Application은 Infrastructure를 모른다")과 정면으로 충돌해 보인다. `Shared.GameData` 계열 이름이 맞다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 두 서버는 Redis Streams로만 통신 | 게임 시작 Outbox 흐름([05](./chapter-05-game-start-e2e.md)) · 던전 결과 보상 파이프라인 |
| 인게임 = 서버가 상태를 소유 | 서버 권위 전투·몬스터·HP/마나([13](./chapter-13-monster-server-authority.md)·[21](./chapter-21-connection-liveness-hp-authority.md)·[23](./chapter-23-mana-resource-authority-ability.md)) |
| Application이 인터페이스를 선언 | 도메인 확장 시 Repository 교체·테스트 대역 주입이 항상 가능 |

> 이 챕터의 원본 학습 로그(대화 형식 Q&A) = [learning-log/chapter-01-architecture.md](../learning-log/chapter-01-architecture.md)

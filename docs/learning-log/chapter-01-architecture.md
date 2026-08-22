# 챕터 1 학습 로그 — 아키텍처 설계

## 내가 처음 답한 것 vs 피드백

### Q. 왜 서버를 둘로 나눴는가?

**내 답:**
원신(MMORPG) 참고. 기본 싱글플레이 기준이라 실시간 통신이 필요한 경우(친구 초대, 던전 입장 등)가 특수한 경우에만 해당된다. 상시 실시간 연결을 유지하면 트래픽/비용 낭비가 생기므로 Web 서버 기반으로 운영하고 필요 시에만 SocketServer에 연결한다.

**피드백:**
방향은 맞음. 추가로 알아야 할 키워드: **Stateful/Stateless 서버 분리**

- Stateless(GameServer): 요청마다 독립, 연결 유지 없음, 수평 확장 쉬움
- Stateful(SocketServer): 연결 유지, 게임 세션 상태 보유, 방 단위로 스케일 필요

포트폴리오 표현으로 다듬어야 할 부분:
> "단순히 비용 문제가 아니라, 로비와 인게임의 요구사항(지연 허용 수준, 연결 패턴, 스케일 단위)이 근본적으로 달라서 분리했다"

---

### Q. 프로토콜 선택 이유

**내 답 — REST:**
Swagger 서버 테스트용. 실제 게임 클라이언트는 gRPC 사용.

**피드백:**
"테스트 전용"만으로는 약함. 운영 환경에서 REST 포트를 왜 열어두는지 설명 필요.
→ Admin API, 헬스체크, 게임 외 웹훅 처리 등의 역할 추가 학습 필요 (TODO)

---

**내 답 — gRPC:**
HTTP/2 멀티플렉싱, Protobuf 직렬화, 양방향 스트림 지원.

**피드백:**
맞음. 강점을 더 구체적으로:
- HTTP/1.1의 Head-of-Line Blocking을 HTTP/2 멀티플렉싱으로 해결
- JSON 대비 Protobuf는 2~5배 직렬화 빠름 + 바이트 크기 작음

---

**내 답 — TCP 직접 구현:**
gRPC의 HTTP/2 오버헤드 때문에 성능이 더 나빠질 수 있어서 저수준 컨트롤을 위해 직접 구현.

**피드백:**
맞음. 추가 포인트: 인게임은 초당 수십~수백 패킷 → 헤더 오버헤드 누적 → 직접 Length-Prefix 프로토콜 설계로 최소화

---

**내 답 — WebSocket 미사용:**
이미 gRPC로 채팅/로비 커버 가능. 두 기술 쓸 필요 없음. 기술 부채 축소.

**피드백:**
맞음. 같은 역할을 두 기술로 중복 구현하는 것은 운영/유지보수 부담 증가.

---

### Q. Clean Architecture 왜 레이어를 나눴는가?

**내 처음 답:**
"의존성을 낮추고 의존성 역전 현상을 줄이기 위해서"

**피드백 — 표현이 틀렸다:**
Clean Architecture는 DIP(의존성 역전 원칙)를 "줄이는 게" 아니라 **적극적으로 적용**하는 구조다.

**올바른 표현:**
> 고수준 모듈(Application)이 저수준 모듈(Infrastructure)에 직접 의존하지 않도록, 인터페이스를 통해 의존성을 역전시킨다. 하위 구현체가 상위가 선언한 인터페이스에 맞춰 구현되는 구조.

**핵심 효과:**
PostgreSQL을 MySQL로 교체해도 Application 레이어 코드는 변경 없음. 테스트 시 Fake 구현체 주입 가능.

---

### Q. IUserRepository 인터페이스 위치?

**내 처음 답:**
Infrastructure에 있다고 함.

**피드백 — 잘못됨:**
인터페이스는 **Application 레이어**에 있어야 함.
Infrastructure에 인터페이스가 있으면 Application이 Infrastructure를 참조하게 됨 → DIP 위반.

**수정 완료:**
`GameServer.Application/Domains/User/Interfaces/IUserRepository.cs` 로 이동.
`AuthService.cs`의 using에서 `GameServer.Infrastructure.*` 전부 제거 완료.

---

## 아직 해결 안 된 것 (TODO)

- [ ] GameServer ↔ SocketServer 서버 간 통신 구현
  - Redis Streams vs Kafka 중 결정 필요
  - SocketServer 다중 인스턴스 시 Room ID 기반 라우팅 전략
- [ ] REST 역할 정의 보강 (Admin/헬스체크 용도)

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
|--------|-----------|
| Stateful/Stateless 서버 분리 | 연결 유지 여부에 따른 서버 역할 분리 |
| DIP (의존성 역전 원칙) | 상위가 인터페이스 선언, 하위가 구현 — 의존 방향 역전 |
| Head-of-Line Blocking | HTTP/1.1에서 앞 요청이 막히면 뒤도 대기하는 문제 |
| Length-Prefix 프로토콜 | TCP 스트림에서 메시지 경계를 [길이][데이터] 구조로 정의 |
| Redis Streams | Redis의 메시지 보존 + Consumer Group 지원 기능 |

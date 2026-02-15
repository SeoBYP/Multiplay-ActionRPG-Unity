# GitHub Project 운영안 (gRPC Chat 우선)

## Project
- 이름: `Phase2 - gRPC Chat`
- 목적: README 기준 Phase2 중 Chat 시스템을 먼저 완료

## Board Columns
1. Backlog
2. Ready
3. In Progress
4. Review
5. Done
6. Blocked

## Labels
- Priority: `priority:P0`, `priority:P1`, `priority:P2`
- Type: `type:feature`, `type:bug`, `type:refactor`, `type:test`, `type:docs`
- Area: `area:api`, `area:app`, `area:infra`, `area:client`, `area:test`, `area:docs`

---

## README 대비 진행상태 정리

### Done (코드 존재)
- gRPC 서버 기본 구동 (`Program.cs`)
- Auth gRPC 구현
- DungeonLobby gRPC 구현
- Redis 연결/세션 저장소
- proto: `auth`, `lobby`, `chat`, `common`

### Not Done (README 목표 대비 미완)
- `ChatGrpcService` 구현 미완 (빈 클래스)
- Chat service/repository 구현 미완
- Program.cs Chat 매핑 없음
- SubscribeChat 스트리밍 lifecycle 미구현
- 채팅 E2E 테스트 없음
- README “진행중” 항목과 실제 코드 정합성 보완 필요

---

## 이슈 백로그 (등록용)

### P0 (이번 스프린트 필수)
1. **[P0][API] Implement ChatGrpcService: SendChat/SubscribeChat**
   - Labels: `priority:P0`, `type:feature`, `area:api`
   - AC:
     - SendChat 요청/응답 동작
     - SubscribeChat server streaming 동작
     - 인증 실패 시 Unauthorized 결과 반환

2. **[P0][API] Map ChatGrpcService in Program.cs**
   - Labels: `priority:P0`, `type:feature`, `area:api`
   - AC:
     - `app.MapGrpcService<ChatGrpcService>();` 추가
     - 로컬 기동 시 ChatService list 확인 가능

3. **[P0][APP] Implement IChatService/ChatService core flow**
   - Labels: `priority:P0`, `type:feature`, `area:app`
   - AC:
     - sessionId -> user 검증
     - message 검증(빈 값/길이)
     - chatType별 분기(global/room/whisper)

4. **[P0][INFRA] Implement ChatMessageRepository + Redis Pub/Sub broadcast**
   - Labels: `priority:P0`, `type:feature`, `area:infra`
   - AC:
     - 메시지 저장(Create/Get)
     - room/global 채널 publish
     - subscribe한 클라이언트로 전달

5. **[P0][TEST] gRPC Chat E2E (2 clients)**
   - Labels: `priority:P0`, `type:test`, `area:test`
   - AC:
     - A 전송 -> B 수신 검증
     - 구독 취소 시 정상 종료
     - 실패 케이스(assert)

### P1 (MVP 직후)
6. **[P1][API] Streaming cancellation/disconnect handling hardening**
7. **[P1][APP] Message validation policy (length/rate/forbidden hook)**
8. **[P1][INFRA] Chat history storage (recent N)**
9. **[P1][CLIENT] Unity gRPC chat integration**
10. **[P1][DOCS] README progress sync (Chat completed state)**

### P2 (안정화)
11. **[P2][TEST] Load test (subscribers latency/throughput)**
12. **[P2][REFACTOR] Unified Result/Error handling for Chat**
13. **[P2][OBS] Structured logs + trace/session id**

---

## 오늘 바로 시작할 In Progress 3개
- #1 ChatGrpcService 구현
- #3 ChatService 핵심 로직 구현
- #4 ChatRepository + Redis Pub/Sub 구현

## 완료 정의 (DoD)
- grpcurl/Postman으로 Chat 송수신 재현 가능
- 최소 E2E 테스트 통과
- 예외/취소 처리 포함
- README 진행률 반영

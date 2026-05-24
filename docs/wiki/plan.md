# 작업 플랜 (현재 진행 상황)

> **새 채팅 시작 시 이 파일을 먼저 읽어라.**  
> Phase가 완료될 때마다 즉시 갱신한다.  
> 마지막 갱신: 2026-05-24 (A-1, A-2 완료)

---

## 🔴 현재 작업: Phase A — 클라이언트 소켓 연결 진입점

**목표**: `OnGameSessionReady(ip, port)` 이벤트를 받은 후 실제 TCP 소켓 연결부터 인게임 초기화까지 이어지는 흐름 구현.

**배경**: 서버는 게임 시작 → SocketServer IP:Port 알림까지 완성되어 있다. 하지만 클라이언트에서 이 IP:Port를 받아 실제로 TCP 연결하는 코드가 없다. `SocketApiClient`가 어떤 LifetimeScope에도 등록되지 않았고, InGame 씬도 없다.

### 태스크 목록

- [x] **A-1**: `ProjectLifetimeScope`에 `SocketApiClient.Install()` + `DungeonLifetimeScope` 신규 생성
- [x] **A-2**: `GameSessionConnector` 구현
  - `IDungeonLobbyService.OnGameSessionReady(ip, port, roomId)` 이벤트 구독
  - `SocketSession.ConnectAsync → AuthenticateAsync → JoinRoomAsync` 순차 실행
  - `S_Auth` / `S_PlayerJoined` 응답 후 `SceneManager.LoadSceneAsync("Dungeon")`
  - `AuthSession.UserId` (JWT sub 파싱) → `SocketConnectionInfo`에 전달
- [ ] **A-3**: 인게임 초기화
  - `S_PlayerJoined` 수신 → PlayerCharacter 스폰 (`InGameEntryPoint` 확장)
  - `S_GameStatus(InProgress)` 수신 → 인게임 UI 전환

---

## 🟡 다음 작업 (Phase B~E)

| Phase | 내용 | 선행 조건 |
|-------|------|-----------|
| **B** | 서버 AttackHandler + 클라이언트 AttackPacketHandler | Phase A 완료 |
| **C** | S_DungeonReady 패킷 (전원 입장 완료 신호) | Phase A 완료 |
| **D** | 몬스터 시스템 (SpawnMonster/MonsterState/MonsterDead) | Phase B 완료 |
| **E** | 던전 종료 & 결과 처리 (DungeonClear → 보상) | Phase D 완료 |

---

## ✅ 완료된 Phase

| Phase | 내용 |
|-------|------|
| 서버 인프라 | Clean Architecture, JWT 인증, DeviceId Binding, Token Rotation |
| 던전 로비 | gRPC 방 CRUD, SubscribeRoom Streaming |
| 게임 시작 E2E | Outbox → Redis Stream → SocketServer 방 생성 → IP:Port 알림 |
| SocketServer | C_Auth, C_PlayerJoin, C_Move/S_Move, Ping/Pong |
| 채팅 | Redis Streams, Global/Room/Whisper |
| 분산 로그 | Serilog + Graylog, TraceId 전파 |
| DB/캐시 | PostgreSQL + Redis Cache-Aside, Testcontainers 통합 테스트 |
| 클라이언트 OutGame | gRPC 로그인/로비 UI, VContainer DI, MVI 아키텍처 |

---

## 참고 파일

- 전체 현황: [`docs/wiki/status.md`](status.md)
- 패킷 규칙: [`docs/wiki/packets.md`](packets.md)
- SocketServer 규칙: [`docs/wiki/socketserver.md`](socketserver.md)
- 서버 흐름: [`docs/wiki/gameflow.md`](gameflow.md)

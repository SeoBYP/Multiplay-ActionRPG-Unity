# 구현 현황 & 다음 작업 가이드

> 마지막 업데이트: 2026-05-24  
> **새 채팅을 열 때 이 파일을 먼저 읽어라.** 프로젝트 전체 흐름과 미구현 항목이 여기 있다.

---

## 이 프로젝트가 무엇인가

**장르**: 원신 스타일 Co-op 액션 RPG (2~4인 던전)  
**목적**: 게임 서버 개발 포트폴리오

**통신 구조**:
```
Unity Client
  ├── gRPC (HTTP/2)  →  GameServer  ← 인증 / 로비 / 채팅
  └── TCP Socket     →  SocketServer ← 인게임 실시간 이동·전투
```

**서버 간 통신**: GameServer ↔ SocketServer는 직접 RPC 없이 Redis Streams로 통신.

---

## 전체 게임 흐름 (현재 구현 기준)

```
[Title 씬]
  1. gRPC Login → JWT Access/Refresh Token 발급
  2. 씬 전환: Title → Main

[Main 씬 (OutGame)]
  3. gRPC GetRooms → 방 목록 표시
  4. gRPC CreateRoom / JoinRoom
  5. gRPC SubscribeRoom (Server Streaming) → 방 이벤트 실시간 수신
  6. (Host) gRPC StartGame
     └── GameServer: Outbox 기록
         └── Redis Stream → SocketServer: GameStartRequestedConsumer
             └── RoomManager.CreateRoom + PlayerState 초기화
             └── GameSessionReadyMessage → Redis Stream
         └── GameServer: GameSessionReadyConsumer
             └── GameSession에 IP:Port 저장
             └── SubscribeRoom 스트림에 GameSessionEvent 발행
  7. 클라이언트 SubscribeRoom에서 GameSessionEvent 수신
     → DungeonLobbyService.OnGameSessionReady(ip, port) 이벤트 발행

[여기서부터 미구현]
  8. OnGameSessionReady → TCP 소켓 연결 (SocketConnector.ConnectAsync)
  9. C_Auth 전송 → S_Auth 수신
  10. C_PlayerJoin 전송 → S_PlayerJoined 수신 (전원 입장 시 S_GameStatus(InProgress))
  11. 인게임 루프: 이동(C_Move/S_Move), 공격(C_Attack/S_Attack)
  12. 던전 종료 → 결과 처리
```

---

## 서버 구현 완료 항목

### GameServer (gRPC)

| 도메인 | 구현 내용 |
|--------|-----------|
| Auth | 회원가입, 로그인, Refresh, Logout, DeviceId Binding, Token Rotation |
| DungeonLobby | 방 CRUD, SubscribeRoom Streaming, StartGame (Outbox까지) |
| GameSession | SocketServer IP:Port 저장, GameSessionReady 수신 후 Streaming 발행 |
| Chat | Global/Room/Whisper, Redis Streams + BroadcastChannel |
| User | 닉네임 설정, 프로필 관리 |

### SocketServer (TCP)

| 패킷 | 핸들러 | 상태 |
|------|--------|------|
| C_Auth → S_Auth | AuthHandler.cs | ✅ |
| C_PlayerJoin → S_PlayerJoined | RoomJoinLeaveHandler.cs | ✅ |
| C_PlayerLeave | RoomJoinLeaveHandler.cs | ✅ |
| C_Move → S_Move (브로드캐스트) | MovementHandler.cs | ✅ |
| C_Ping → S_Pong | PingPongHandler.cs | ✅ |
| C_Attack → S_Attack | **없음** | ❌ |
| S_GameStatus(InProgress) 브로드캐스트 | RoomJoinLeaveHandler 내부 | ✅ |
| S_DungeonReady | **없음** | ❌ |

### Shared.Packet (패킷 정의 + Union 등록)

| 패킷 | Union ID | 상태 |
|------|----------|------|
| C_Attack / S_Attack | 1600 / 1601 | ✅ 정의됨 (핸들러만 없음) |
| S_DungeonReady | **미정의** | ❌ |
| S_SpawnMonster, S_MonsterState, S_MonsterDead | 1810~1819 (미할당) | ❌ |

---

## 클라이언트 구현 완료 항목

### DI 등록 (LifetimeScope)

| 스코프 | 등록 항목 |
|--------|-----------|
| ProjectLifetimeScope | GameApiClient (gRPC), AuthService, DungeonLobbyService, UserProfile, StartupIntentQueue |
| OutGameLifetimeScope | InputRouter, LobbyModel, LobbyViewController |
| MainLifetimeScope | InputRouter, LobbyModel, StateFactory, StateMachineBuilder, MainSceneInitializer |

**⚠️ SocketApiClient가 어떤 스코프에도 등록되지 않았다.**  
`SocketConnector`, `SocketSession`, `SocketPacketDispatcher` 전부 DI 밖에 있다.

### Socket 클라이언트 코드 (존재하지만 DI 미등록)

| 파일 | 역할 | 상태 |
|------|------|------|
| `SocketConnector.cs` | TCP 연결/해제 | 코드 있음, DI 미등록 |
| `SocketSession.cs` | 패킷 송수신 | 코드 있음, DI 미등록 |
| `SocketPacketDispatcher.cs` | 수신 패킷 라우팅 | 코드 있음, DI 미등록 |
| `AuthPacketHandler.cs` | S_Auth 처리 | 코드 있음 |
| `PlayerJoinedPacketHandler.cs` | S_PlayerJoined 처리 | 코드 있음 |
| `MovePacketHandler.cs` | S_Move 처리 (다른 플레이어 위치 갱신) | 코드 있음 |
| `AttackPacketHandler.cs` | S_Attack 처리 | **없음** |

### 씬 구성

| 씬 | 용도 | 상태 |
|----|------|------|
| Title.unity | 로그인 화면 | ✅ |
| Main.unity | OutGame(로비) + InGame 통합 | ✅ (OutGame만 동작) |
| InGame 전용 씬 | 인게임 전용 | **없음 (Main에 합쳐야 하거나 별도 생성)** |

---

## 미구현 항목 (우선순위 순)

### Phase A — 클라이언트 소켓 연결 진입점 (가장 먼저)

`OnGameSessionReady` 이벤트가 발행된 후 실제로 TCP 소켓에 연결하는 코드가 없다.  
이것 없이는 인게임 자체가 시작되지 않는다.

```
필요한 작업:
1. InGameLifetimeScope 생성 (또는 MainLifetimeScope에 SocketApiClient 등록)
   - SocketApiClient.Install(builder) 호출 위치 결정
2. InGameEntryPoint (또는 GameSessionConnector) 구현
   - OnGameSessionReady 이벤트 구독
   - SocketConnector.ConnectAsync(ip, port) 호출
   - C_Auth { UserId, SessionId } 전송
   - S_Auth 수신 대기 → C_PlayerJoin { RoomId } 전송
3. 인게임 씬/오브젝트 초기화 (PlayerCharacter 스폰 등)
```

### Phase B — 서버: AttackHandler

패킷·Union 정의 완료. 핸들러만 없다.

```
필요한 작업:
1. SocketServer/PacketHandler/Handler/AttackHandler.cs 생성
   - 1단계: 단순 릴레이 (MovementHandler와 동일 구조)
     session.Room.Broadcast(S_Attack { ... }, session.SessionId)
   - 2단계: 서버 히트 판정 추가 (범위 검증)
2. 클라이언트: AttackPacketHandler.cs 생성
   - S_Attack 수신 → 피격 이펙트, 체력 감소 UI 처리
```

### Phase C — S_DungeonReady 패킷

전원 입장 완료 신호. 현재는 `S_GameStatus(InProgress)`만 보내고 있다.  
클라이언트가 "모두 입장 완료 → 게임 시작" 타이밍을 정확히 받을 수 없다.

```
필요한 작업:
1. S_DungeonReady 패킷 정의 + Union 등록 (Union: 1800)
2. RoomJoinLeaveHandler: MemberCount == MaxMembers 조건에서 S_DungeonReady 전송
3. 클라이언트: DungeonReadyPacketHandler 구현
```

### Phase D — 몬스터 시스템 (서버 + 클라이언트)

```
패킷 (미정의):
  S_SpawnMonster (1810) — 몬스터 초기 정보
  S_MonsterState (1811) — 몬스터 위치/상태 브로드캐스트
  S_MonsterDead  (1812) — 몬스터 사망

서버: MonsterManager, AI 틱, 히트 판정
클라이언트: MonsterEntity 스폰/이동/사망 처리
```

### Phase E — 던전 종료 & 결과 처리

```
패킷 (미정의):
  S_DungeonClear (1820)

흐름:
  SocketServer: 던전 클리어 감지
    → DungeonClearMessage → Redis Stream
    → GameServer: DungeonResultService (보상 계산)
    → GameServer: DB 저장
  클라이언트: S_DungeonClear 수신 → 결과 화면
```

---

## 기타 미완성 기술 부채

| 항목 | 우선순위 | 위치 |
|------|----------|------|
| SocketServer IP 하드코딩 → appsettings.json | 높음 | GameSessionReadyMessage |
| `DungeonRoom.DungeonId` 없음 (어떤 던전인지 구분 불가) | 중간 | DungeonRoom 엔티티 |
| Room.Leave 시 `_playerStates` 정리 누락 | 낮음 | Room.cs |
| Redis Consumer name `socket-1` 고정 → 동적 생성 | 낮음 | GameStartRequestedMessageQueue |
| `GetRooms` count/페이징 정책 미결정 | 낮음 | DungeonLobbyGrpcService |

---

## 포트폴리오 완성도

| 챕터 | 주제 | 상태 |
|------|------|------|
| 1 | Clean Architecture + 이중 서버 | ✅ |
| 2 | JWT 인증 + DeviceId + Token Rotation | ✅ |
| 3 | gRPC Streaming + Redis Pub-Sub (로비) | ✅ |
| 4 | 채팅 (Redis Streams) | ✅ |
| 5 | 게임 시작 E2E (Outbox → SocketServer) | ✅ |
| 6 | 분산 로그 (Serilog + Graylog) | ✅ |
| 7 | DB + Redis 캐시 + 통합 테스트 | ✅ |
| 8 | SocketServer 이동 동기화 | ✅ |
| 9 | Unity 클라이언트 (gRPC + VContainer + E2E) | ✅ |
| 10 | 인게임 연동 (소켓 진입 + 전투) | ❌ 진행 중 |

**10챕터 완성 순서: Phase A → Phase B → Phase C → Phase D → Phase E**

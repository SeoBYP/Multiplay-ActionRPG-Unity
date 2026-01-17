# Multiplay-ActionRPG-Unity

**원신 스타일 오픈월드 액션 RPG 서버 개발 포트폴리오**

[![Unity](https://img.shields.io/badge/Unity-6000.3_LTS-black.svg?style=flat&logo=unity)](https://unity.com/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)

> **게임 서버 개발 역량을 증명하는 실전 프로젝트**  
> 실무에서 검증된 하이브리드 아키텍처(HTTP + WebSocket + TCP)로 구현합니다.

---

## 📋 목차

1. [프로젝트 개요](#-프로젝트-개요)
2. [아키텍처 설계](#-아키텍처-설계)
3. [기술 스택](#-기술-스택)
4. [개발 로드맵](#-개발-로드맵)
5. [현재 진행 상황](#-현재-진행-상황)

---

## 🎮 프로젝트 개요

### 프로젝트 목표

**기술적 목표:**

- 실무 수준의 게임 서버 아키텍처 설계 및 구현
- HTTP + WebSocket + TCP 하이브리드 통신 모델 구현
- 확장 가능하고 유지보수 가능한 코드 작성
- 보안과 성능을 모두 고려한 설계

### 게임 컨셉

**장르**: 오픈월드 액션 RPG (원신 스타일)

**플레이 모드:**

- **PVE 모드**: 싱글 플레이 (오픈월드 탐험, 퀘스트, 몬스터 사냥)
- **Co-op 모드**: 2~4인 파티 플레이 (던전, 레이드 보스)

---

## 🏗️ 아키텍처 설계

### 하이브리드 통신 모델

원신 스타일 게임의 특성을 분석한 결과, **통신 방식을 기능별로 분리**하는 것이 최적입니다.

```
┌─────────────────────────────────────────────────────┐
│  통신 방식 선택 기준                                  │
├─────────────────────────────────────────────────────┤
│                                                     │
│  HTTP REST (80% - 대부분의 게임 로직)                │
│  ├─ 요청-응답 패턴                                   │
│  ├─ Stateless (수평 확장 쉬움)                       │
│  ├─ 실시간성 덜 중요 (수초 지연 OK)                   │
│  └─ 예: 로그인, 퀘스트, 아이템, 위치 동기화(저장)        │
│                                                     │
│  WebSocket (15% - 실시간 알림)                       │
│  ├─ 서버 → 클라이언트 Push                           │
│  ├─ 양방향 실시간 통신                               │
│  └─ 예: 채팅, 친구 알림, 파티 초대                    │
│                                                     │
│  TCP Socket (5% - 고성능 실시간)                     │
│  ├─ 극도의 실시간성 (60Hz+)                          │
│  ├─ 최소 지연시간                                    │
│  └─ 예: Co-op 전투                                  │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### 전체 아키텍처

```
                    ┌─────────────────┐
                    │ Load Balancer   │
                    │   (Nginx)       │
                    └────────┬────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
        ┌───────▼────────┐       ┌───────▼────────┐
        │  Game Server   │       │  Game Server   │
        │  (ASP.NET)     │       │  (ASP.NET)     │
        │  - HTTP API    │       │  - HTTP API    │
        │  - SignalR     │       │  - SignalR     │
        └───────┬────────┘       └───────┬────────┘
                │                        │
                └────────────┬───────────┘
                             │
                ┌────────────▼────────────┐
                │   Shared Resources      │
                │  ┌──────────────────┐   │
                │  │   PostgreSQL     │   │
                │  │   (플레이어 DB)   │   │
                │  └──────────────────┘   │
                │  ┌──────────────────┐   │
                │  │     Redis        │   │
                │  │ (세션, 캐시)      │   │
                │  └──────────────────┘   │
                └─────────────────────────┘
                             │
                             │ (던전 입장 시)
                             ▼
                ┌─────────────────────────┐
                │  Dungeon Server Pool    │
                │  (TCP Socket)           │
                │  - 독립 인스턴스         │
                │  - Co-op 전투 전용       │
                └─────────────────────────┘
```

### 서버 역할 분리

#### Game Server (ASP.NET Core)

**통합 서버 - Modular Monolith**

```
┌──────────────────────────────────────┐
│  ASP.NET Core Web API                │
│                                      │
│  HTTP Controllers (80%)              │
│  ├─ AuthController                   │
│  │   - POST /api/auth/login          │
│  │   - POST /api/auth/register       │
│  │                                   │
│  ├─ PlayerController                 │
│  │   - POST /api/player/sync         │
│  │   - POST /api/player/item/acquire │
│  │                                   │
│  ├─ QuestController                  │
│  │   - POST /api/quest/complete      │
│  │   - GET  /api/quest/list          │
│  │                                   │
│  └─ LobbyController                  │
│      - GET  /api/lobby/rooms         │
│      - POST /api/lobby/room          │
│                                      │
│  SignalR Hub (20%)                   │
│  └─ GameHub                          │
│      - 채팅                           │
│      - 실시간 알림                     │
│      - 파티 초대                       │
└──────────────────────────────────────┘
```

**장점:**

- 개발/디버깅 쉬움 (Swagger, Postman)
- 수평 확장 용이 (Stateless)
- ASP.NET Core 생태계 활용
- 익숙한 REST API 패턴

#### Dungeon Server (TCP Socket)

**분리된 서비스 - 독립 인스턴스**

```
┌──────────────────────────────────────┐
│  .NET Console Application            │
│                                      │
│  - TCP 소켓 (고성능)                  │
│  - 높은 틱레이트 (60Hz)               │
│  - 2~4명 격리된 인스턴스              │
│  - Protocol Buffers                  │
│  - Co-op 전투만 담당                  │
└──────────────────────────────────────┘
```

**분리 이유:**

- 극도의 실시간성 필요 (60Hz 동기화)
- 독립적인 생명주기 (던전 종료 시 프로세스 종료 가능)
- 높은 CPU 사용 (물리, AI 계산)
- 장애 격리 (한 던전 크래시가 다른 곳에 영향 없음)

---

## 🛠️ 기술 스택

### 클라이언트

- **엔진**: Unity 2021.3 LTS
- **언어**: C# 10.0
- **HTTP**: UnityWebRequest / HttpClient
- **WebSocket**: SignalR Client
- **직렬화**: JSON (HTTP), Protocol Buffers (TCP)

### Game Server (Main)

- **프레임워크**: ASP.NET Core 8.0
- **HTTP API**: RESTful Controllers
- **WebSocket**: SignalR
- **인증**: JWT (Bearer Token)
- **DB**: Entity Framework Core + PostgreSQL
- **캐시**: StackExchange.Redis

### Dungeon Server

- **프레임워크**: .NET 8.0 Console App
- **통신**: System.Net.Sockets (TCP)
- **직렬화**: Protocol Buffers
- **물리**: Headless Unity (선택)

### 공통 인프라

- **데이터베이스**: PostgreSQL 15+
- **캐시**: Redis 7+
- **로깅**: Serilog
- **컨테이너**: Docker
- **오케스트레이션**: Kubernetes (K3s)

---

## 📅 개발 로드맵

### 전체 일정

```
Phase 1      Phase 2         Phase 3         Phase 4
(1개월)      (2~3개월)       (2~3개월)       (2~3개월)
  │              │               │               │
채팅 서버     HTTP API      네트워크 동기화    전투 시스템
완료 ✅       세션/로비       (Co-op)          AI 시스템
```

---

## 📍 현재 진행 상황

### ✅ Phase 1: 채팅 서버 (완료)

**개발 기간:** 1개월  
**상태:** 완료

**구현 내용:**

- [x] TCP 소켓 서버
- [x] Protocol Buffers 통합
- [x] 메시지 브로드캐스팅
- [x] 자동화된 패킷 핸들러

**데모:**
![채팅 데모](./assets/chat.gif)

---

### 🔄 Phase 2: HTTP API 및 세션 관리 (진행중)

**개발 기간:** 2~3개월  
**목표:** ASP.NET Core 기반 REST API 및 실시간 알림 구현

---

#### Step 2-1: ASP.NET Core 프로젝트 세팅 (1주)

**목표:** 기본 웹 API 프로젝트 구성

**세부 작업:**

1. **프로젝트 생성**

```bash
# 솔루션 생성
dotnet new sln -n GameServer

# ASP.NET Core Web API 프로젝트 생성
dotnet new webapi -n GameServer.API
dotnet sln add GameServer.API

# 공통 라이브러리
dotnet new classlib -n GameServer.Core
dotnet new classlib -n GameServer.Data
```

2. **패키지 설치**

```bash
# ASP.NET Core
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.SignalR

# Database
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package StackExchange.Redis

# 로깅
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console

# 유틸리티
dotnet add package BCrypt.Net-Next
dotnet add package Swashbuckle.AspNetCore
```

3. **프로젝트 구조**

```
GameServer/
├─ GameServer.API/              # ASP.NET Core 웹 API
│   ├─ Controllers/
│   │   ├─ AuthController.cs
│   │   ├─ PlayerController.cs
│   │   └─ LobbyController.cs
│   ├─ Hubs/
│   │   └─ GameHub.cs          # SignalR
│   ├─ Middleware/
│   │   └─ JwtMiddleware.cs
│   └─ Program.cs
│
├─ GameServer.Core/             # 비즈니스 로직
│   ├─ Services/
│   │   ├─ AuthService.cs
│   │   ├─ PlayerService.cs
│   │   └─ LobbyService.cs
│   └─ Models/
│
└─ GameServer.Data/             # 데이터 액세스
    ├─ Entities/
    │   ├─ User.cs
    │   └─ Character.cs
    └─ GameDbContext.cs
```

4. **기본 설정 (appsettings.json)**

```json
{
  "ConnectionStrings": {
    "GameDb": "Host=localhost;Database=gamedb;Username=postgres;Password=password",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-characters",
    "Issuer": "GameServer",
    "Audience": "GameClient",
    "ExpirationMinutes": 15
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**완료 조건:**

- [ ] 프로젝트 생성 및 구조 설정
- [ ] 패키지 설치 완료
- [ ] Swagger UI 동작 확인
- [ ] 기본 Health Check API

---

#### Step 2-2: 인증 시스템 (HTTP API) (2주)

**목표:** JWT 기반 회원가입/로그인 API

**API 설계:**

```
POST /api/auth/register
Body: {
  "username": "player1",
  "password": "password123",
  "email": "player1@game.com"
}
Response: {
  "success": true,
  "userId": 12345
}

POST /api/auth/login
Body: {
  "username": "player1",
  "password": "password123"
}
Response: {
  "success": true,
  "accessToken": "eyJhbGc...",
  "refreshToken": "refresh_token_here"
}

POST /api/auth/refresh
Body: {
  "refreshToken": "refresh_token_here"
}
Response: {
  "accessToken": "new_access_token"
}
```

**데이터베이스 설계:**

```sql
-- users 테이블
CREATE TABLE users (
    user_id BIGSERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP
);

CREATE INDEX idx_username ON users(username);
CREATE INDEX idx_email ON users(email);
```

**보안 구현:**

- 비밀번호 해싱: bcrypt (cost factor 12)
- JWT Access Token: 15분 유효
- JWT Refresh Token: 7일 유효, Redis 저장
- HTTPS 필수 (프로덕션)

**Redis 세션 관리:**

```
Key: session:{user_id}
Value: {
  "refreshToken": "token_value",
  "loginAt": "2024-01-16T10:00:00Z",
  "ipAddress": "192.168.1.1"
}
TTL: 7 days
```

**완료 조건:**

- [ ] 회원가입 API
- [ ] 로그인 API
- [ ] JWT 토큰 발급/검증
- [ ] Refresh Token 로직
- [ ] Redis 세션 저장
- [ ] Postman 테스트
- [ ] Unity 클라이언트 연동

---

#### Step 2-3: 로비 시스템 (HTTP + SignalR) (3주)

**목표:** 방 생성/관리 및 실시간 상태 동기화

**HTTP API 설계:**

```
GET /api/lobby/rooms
Response: {
  "rooms": [
    {
      "roomId": 1,
      "roomName": "초보자방",
      "currentPlayers": 2,
      "maxPlayers": 4,
      "difficulty": "NORMAL",
      "status": "WAITING"
    }
  ]
}

POST /api/lobby/room
Headers: Authorization: Bearer {token}
Body: {
  "roomName": "고수방",
  "maxPlayers": 4,
  "difficulty": "HARD"
}
Response: {
  "success": true,
  "roomId": 123
}

POST /api/lobby/room/{roomId}/join
Response: {
  "success": true,
  "players": [
    {"userId": 1, "username": "player1"},
    {"userId": 2, "username": "player2"}
  ]
}

DELETE /api/lobby/room/{roomId}/leave
Response: {
  "success": true
}
```

**SignalR Hub (실시간 알림):**

```
GameHub Methods:

Server → Client:
- RoomCreated(roomId, roomInfo)
- RoomDeleted(roomId)
- PlayerJoined(roomId, playerInfo)
- PlayerLeft(roomId, userId)
- RoomStatusChanged(roomId, status)
- ChatMessage(username, message, type)
- PartyInvite(fromUserId, fromUsername)

Client → Server:
- SendChatMessage(message, chatType)
- InviteToParty(targetUserId)
```

**데이터베이스:**

```sql
-- rooms 테이블
CREATE TABLE rooms (
    room_id BIGSERIAL PRIMARY KEY,
    room_name VARCHAR(100) NOT NULL,
    host_user_id BIGINT NOT NULL,
    max_players INT NOT NULL,
    difficulty VARCHAR(20),
    status VARCHAR(20) DEFAULT 'WAITING',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- room_players 테이블
CREATE TABLE room_players (
    room_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (room_id, user_id)
);
```

**Redis 캐싱:**

```
# 방 목록 (빠른 조회)
Key: lobby:rooms
Type: Set
Members: [room_id_1, room_id_2, ...]

# 방 정보
Key: room:{room_id}
Type: Hash
Fields: {
  room_name, host_user_id, current_players,
  max_players, difficulty, status
}

# 방 플레이어 목록
Key: room:{room_id}:players
Type: Set
Members: [user_id_1, user_id_2, ...]
```

**완료 조건:**

- [ ] 방 목록 조회 API
- [ ] 방 생성 API
- [ ] 방 입장/퇴장 API
- [ ] SignalR Hub 구현
- [ ] 실시간 알림 동작 확인
- [ ] Redis 캐싱
- [ ] Unity 클라이언트 UI 연동

---

#### Step 2-4: PVE 게임 로직 (HTTP API) (2주)

**목표:** 오픈월드 PVE 기본 기능

**API 설계:**

```
POST /api/player/sync
Headers: Authorization: Bearer {token}
Body: {
  "position": {"x": 100, "y": 0, "z": 50},
  "rotation": {"x": 0, "y": 90, "z": 0},
  "timestamp": "2024-01-16T10:30:00Z"
}
Response: {
  "success": true
}

POST /api/player/item/acquire
Body: {
  "itemId": 1001,
  "position": {"x": 100, "y": 0, "z": 50},
  "timestamp": "2024-01-16T10:30:00Z"
}
Response: {
  "success": true,
  "itemName": "체력 포션"
}

POST /api/quest/complete
Body: {
  "questId": 5,
  "objectiveData": {
    "monstersKilled": 10
  }
}
Response: {
  "success": true,
  "rewards": {
    "gold": 100,
    "exp": 500,
    "items": [
      {"itemId": 2001, "quantity": 1}
    ]
  }
}
```

**서버 검증 로직:**

**이동 속도 검증:**

- 클라이언트가 물리적으로 불가능한 속도로 이동하면 차단
- 최대 이동 속도: 10 m/s
- 허용 오차: 20% (네트워크 지연 고려)

**아이템 획득 검증:**

- 플레이어 위치와 아이템 위치 거리 확인 (5m 이내)
- 이미 획득한 아이템 중복 획득 방지
- 타임스탬프 검증 (5초 이내 패킷만 허용)

**퀘스트 완료 검증:**

- 퀘스트 진행 상태 확인
- 목표 달성 여부 확인
- 보상 중복 지급 방지

**완료 조건:**

- [ ] 위치 동기화 API (30초마다 호출)
- [ ] 아이템 획득 API
- [ ] 퀘스트 완료 API
- [ ] 서버 검증 로직
- [ ] Unity 클라이언트 연동

---

#### Step 2-5: 던전 마이그레이션 (gRPC) (2주)

**목표:** Game Server → Dungeon Server 전환

**gRPC 서비스 정의:**

```protobuf
// dungeon.proto
syntax = "proto3";

service DungeonService {
    rpc AllocateInstance(AllocateRequest) returns (AllocateResponse);
    rpc ReleaseInstance(ReleaseRequest) returns (ReleaseResponse);
}

message AllocateRequest {
    uint64 party_id = 1;
    repeated uint64 player_ids = 2;
    uint32 dungeon_id = 3;
}

message AllocateResponse {
    bool success = 1;
    string instance_id = 2;
    string host = 3;
    uint32 port = 4;
    string migration_token = 5;
}
```

**마이그레이션 흐름:**

```
[Client]           [Game Server]         [Redis]      [Dungeon Server]
    │                     │                  │                 │
    ├─ 던전 입장 요청 ────►│                  │                 │
    │  POST /api/dungeon/enter              │                 │
    │                     │                  │                 │
    │                     ├─ 플레이어 상태 저장 ─►│                 │
    │                     │  (position, HP, MP)│                 │
    │                     │                  │                 │
    │                     ├─ gRPC AllocateInstance ────────────►│
    │                     │                  │                 │
    │                     │◄─────────────────┼─────────────────┤
    │                     │  (host, port, token)               │
    │                     │                  │                 │
    │◄─ 재접속 정보 ───────┤                  │                 │
    │  {                  │                  │                 │
    │    "host": "10.0.0.5",                │                 │
    │    "port": 10000,   │                  │                 │
    │    "token": "abc123"│                  │                 │
    │  }                  │                  │                 │
    │                     │                  │                 │
    ├─ Dungeon Server 접속 (TCP) ────────────┼─────────────────►│
    │  (token)            │                  │                 │
    │                     │                  │                 │
    │                     │                  │◄─ 상태 로드 ────┤
    │                     │                  │                 │
    │◄─ 던전 입장 완료 ─────┼──────────────────┼─────────────────┤
```

**HTTP API:**

```
POST /api/dungeon/enter
Body: {
  "dungeonId": 1,
  "partyId": 123  // nullable
}
Response: {
  "success": true,
  "host": "10.0.0.5",
  "port": 10000,
  "migrationToken": "eyJhbGc..."
}
```

**완료 조건:**

- [ ] Dungeon Server 프로토타입
- [ ] gRPC 서비스 구현
- [ ] 마이그레이션 토큰 생성/검증
- [ ] 상태 저장/복구
- [ ] 통합 테스트

---

### Phase 2 완료 기준

**기능 완성도:**

- [x] HTTP REST API (인증, 로비, PVE)
- [x] SignalR WebSocket (채팅, 알림)
- [x] JWT 인증
- [x] Redis 캐싱
- [x] PostgreSQL 데이터 저장
- [x] 던전 마이그레이션

**성능 목표:**

- 1,000명 동시 접속
- API 응답 시간 < 100ms
- SignalR 메시지 지연 < 50ms

**테스트:**

- [ ] API 통합 테스트
- [ ] 부하 테스트 (1,000명)
- [ ] Unity 클라이언트 E2E 테스트

---

### 📌 Phase 3: Co-op 던전 시스템 (예정)

**개발 기간:** 2~3개월  
**목표:** TCP 소켓 기반 실시간 전투

**구현 시스템:**

- TCP 소켓 서버 (Dungeon Server)
- 실시간 위치 동기화 (60Hz)
- 전투 로직 (공격, 스킬)
- 서버 권위 검증

---

### 📌 Phase 4: 데이터 & AI 시스템 (예정)

**개발 기간:** 2~3개월

**구현 시스템:**

- 인벤토리 시스템
- 퀘스트 시스템
- 몬스터 AI
- 보스 AI

---

## 🎯 핵심 설계 결정

### 왜 HTTP를 주 통신으로 선택했는가?

**PVE 게임의 특성:**

- 대부분의 액션이 클라이언트에서 처리 (이동, 전투)
- 서버는 주기적 검증만 필요 (30초~1분)
- 실시간성이 덜 중요 (수초 지연 허용)

**HTTP의 장점:**

- 구현 간단 (ASP.NET Core)
- 디버깅 쉬움 (Swagger, Postman)
- Stateless → 수평 확장 용이
- 로드밸런싱 간단
- 개발자 친숙도 높음

**실제 사례:**

- 원신: HTTP/HTTPS로 대부분 처리
- 모바일 RPG: 거의 모두 HTTP 기반

### 언제 WebSocket을 쓰는가?

**필요한 경우:**

- 서버 → 클라이언트 Push
- 실시간 알림 (채팅, 친구 접속)
- 양방향 실시간 통신

**SignalR 선택 이유:**

- ASP.NET Core 통합
- 자동 재연결
- 브라우저/Unity 모두 지원

### 언제 TCP 소켓을 쓰는가?

**필요한 경우만:**

- 극도의 실시간성 (60Hz+ 동기화)
- 최소 지연시간 필수
- 예: Co-op 전투, PvP

**Co-op만 TCP 소켓인 이유:**

- 4명이 같은 공간에서 전투
- 정확한 히트 판정 필요
- 타이밍 중요

---

## 🔒 보안

### JWT 인증

- Access Token: 15분 (짧게)
- Refresh Token: 7일 (Redis)
- 비밀키 환경변수 관리

### API 보안

- HTTPS 필수
- Rate Limiting
- Input Validation

### 서버 검증

- 이동 속도 검증
- 아이템 획득 거리 검증
- 타임스탬프 검증

---

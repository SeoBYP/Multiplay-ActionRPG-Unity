# Multiplay-ActionRPG-Unity

**Unity 멀티플레이 액션 RPG — 이중 서버 게임 서버 개발 포트폴리오**

[![Unity](https://img.shields.io/badge/Unity-6000.4.8f1-black.svg?style=flat&logo=unity)](https://unity.com/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![gRPC](https://img.shields.io/badge/gRPC-HTTP%2F2-00ADD8.svg?style=flat&logo=go)](https://grpc.io/)
[![MemoryPack](https://img.shields.io/badge/Socket-TCP%20%2B%20MemoryPack-orange.svg?style=flat)](https://github.com/Cysharp/MemoryPack)
[![Redis](https://img.shields.io/badge/Redis-Streams-DC382D.svg?style=flat&logo=redis)](https://redis.io/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15-336791.svg?style=flat&logo=postgresql)](https://www.postgresql.org/)

> **이중 서버 아키텍처**로 게임 서버 개발 역량을 증명하는 실전 프로젝트.
> 대부분의 게임 로직은 **gRPC**(HTTP/2)로, 극한 실시간 인게임은 **TCP + MemoryPack**으로 처리하며,
> 두 서버는 **직접 RPC 없이 Redis Streams**(Outbox 패턴)로만 통신합니다.

---

## 📋 목차

1. [프로젝트 개요](#-프로젝트-개요)
2. [아키텍처](#-아키텍처)
3. [기술 스택](#-기술-스택)
4. [프로젝트 구조](#-프로젝트-구조)
5. [구현 현황](#-구현-현황)
6. [어빌리티 연출 타임라인 툴](#-어빌리티-연출-타임라인-툴)
7. [개발 로드맵](#-개발-로드맵)
8. [핵심 설계 결정](#-핵심-설계-결정)
9. [문제 → 원인 → 해결 사례](#-문제--원인--해결-사례)
10. [테스트 & 품질](#-테스트--품질)
11. [실행 방법](#-실행-방법)
12. [문서](#-문서)

---

## 🎮 프로젝트 개요

**장르**: 멀티플레이 액션 RPG
**플레이 모드**: PVE 싱글 (오픈월드 탐험·퀘스트·사냥) + Co-op 던전 (2~4인 파티)

**기술적 목표**
- 실무 수준의 **이중 서버**(gRPC 게임 서버 + TCP 소켓 서버) 아키텍처 설계·구현
- **서버 간 강결합 제거** — 직접 RPC 대신 Redis Streams + Outbox 패턴으로 비동기 연동
- Clean Architecture 기반의 유지보수 가능한 도메인 설계
- 보안(JWT·DeviceId 바인딩·토큰 로테이션)과 성능(HTTP/2·바이너리 직렬화·캐시)을 모두 고려
- **테스트 주도** — 단위 / 통합(Testcontainers) / E2E(실서버) 3계층 + 회귀 방지 자동화

**규모 스냅샷** (2026-07 기준)

| 항목 | 규모 |
|------|------|
| gRPC | 서비스 **11개** / RPC **31개** (Auth · User · Lobby · Chat · Inventory · Equipment · Progression · Wallet · Shop …) |
| TCP 패킷 | MemoryPack Union **30종** (인증 / 입·퇴장 / 이동 / 전투 / 라이프사이클 / 던전 이벤트) |
| 게임 데이터 (SO 저작 → JSON bake) | 어빌리티 **15** · 몬스터 **13종**(엘리트·보스 변종 4 포함) · 드롭 테이블 **12** · 레벨 테이블 **60레벨** · 스폰 레이아웃 **8맵**(플레이용 던전 5, L1→L30) |
| 테스트 | 서버 xUnit **643개**(GameServer 384 · SocketServer 209 · Shared.Gameplay 50) + Unity **375개**(EditMode 192 · PlayMode 183, 그중 Docker 실서버 E2E 91) ≈ **총 1,000여 개** |

---

## 🏗 아키텍처

### 전체 구성

```mermaid
flowchart TB
    Client["Unity Client<br/>(VContainer DI · MVI)"]

    subgraph GS["GameServer (ASP.NET Core · Clean Architecture)"]
        direction TB
        api["API: gRPC Unary + Server Streaming"]
        app["Application (도메인 서비스)"]
        infra["Infrastructure (EF Core · Redis)"]
        api --> app
        infra --> app
    end

    subgraph SS["SocketServer (.NET Generic Host)"]
        direction TB
        tcp["TCP Listener · Session · Room"]
        ph["PacketHandler (MemoryPack)"]
    end

    subgraph Data["Shared Infra"]
        PG[("PostgreSQL<br/>영속 데이터")]
        RD[("Redis<br/>캐시 · 세션 · Streams")]
    end

    Client -->|"gRPC (HTTP/2)<br/>인증·로비·채팅"| GS
    Client -.->|"TCP + MemoryPack<br/>입장·이동·전투"| SS
    GS --> PG
    GS --> RD
    SS --> RD
    GS <-->|"Redis Streams<br/>(직접 RPC 없음)"| RD
    SS <-->|"Redis Streams"| RD
```

### 통신 3계층

| 계층 | 수단 | 용도 | 예 |
|------|------|------|----|
| **gRPC Unary** | HTTP/2, Protobuf | 요청-응답형 게임 로직 | 회원가입/로그인, 방 CRUD, 닉네임 |
| **gRPC Server Streaming** | HTTP/2 | 서버→클라 실시간 Push | 로비 방 갱신(SubscribeRoom), 채팅 |
| **TCP + MemoryPack** | 순수 TCP | 극한 실시간 인게임 | 입장(C_PlayerJoin), 이동(C_Move/S_Move), 전투 |

### 서버 간 연동 — Redis Streams + Outbox (직접 RPC 금지)

GameServer와 SocketServer는 **서로를 직접 호출하지 않습니다.** 모든 교차 이벤트는 Redis Stream을 경유합니다.

```mermaid
sequenceDiagram
    participant C as Unity Client
    participant G as GameServer
    participant DB as PostgreSQL
    participant R as Redis Streams
    participant S as SocketServer

    C->>G: gRPC StartRoom
    G->>DB: 방 상태 + Outbox 기록 (한 트랜잭션)
    G-->>R: (OutboxPublisher) stream:game:start 발행
    R-->>S: GameStartRequested 소비 → 방/플레이어 메모리 생성
    G-->>C: gRPC SubscribeRoom 스트림으로 GameSessionReady(ip:port)
    C->>S: TCP 연결 + C_PlayerJoin
    Note over C,S: 인게임 (이동/전투, MemoryPack)
    C->>S: C_PlayerLeave (퇴장)
    S-->>R: stream:game:room:lifecycle 발행 (PlayerLeftRoom)
    R-->>G: RoomLifecycleConsumer 소비 → 방/플레이어 association 정리
```

주요 스트림: `stream:game:start`(게임 시작) · `stream:game:session:ready`(세션 준비) · `stream:game:room:lifecycle`(플레이어 퇴장) · `stream:chat:*`(채팅).

---

## 🛠 기술 스택

### 클라이언트 (Unity)
- **엔진**: Unity `6000.4.8f1`
- **DI**: VContainer (씬별 LifetimeScope)
- **아키텍처**: MVI (OutGame / InGame), 상태머신 + 입력 버퍼 분리
- **gRPC**: Grpc.Net.Client + **YetAnotherHttpHandler**(HTTP/2 h2c)
- **소켓**: System.Net.Sockets (TCP) + **MemoryPack** 직렬화
- **비동기/반응형**: UniTask, R3
- **로딩**: Addressables (HUD/뷰 런타임 로드)
- **2-클라 테스트**: Unity Multiplayer Play Mode (MPPM)

### GameServer (ASP.NET Core)
- **.NET 10** + ASP.NET Core gRPC (Grpc.AspNetCore)
- **Clean Architecture**: API → Application ← Infrastructure / Domain
- **인증**: JWT (Access 15분 + Refresh), DeviceId 바인딩, 토큰 로테이션, 단일 기기 세션
- **DB**: EF Core + PostgreSQL / **캐시·세션·스트림**: StackExchange.Redis
- **신뢰성**: Outbox 패턴, BackgroundService(Consumer/Publisher)

### SocketServer
- **.NET 10** Generic Host (콘솔)
- **통신**: TCP + **MemoryPack**, 자동 패킷 핸들러(Union ID)
- Session/Room 관리, HeartBeat 타임아웃

### 공통 인프라
- **PostgreSQL 15** · **Redis 7** · **Docker Compose**
- **로깅**: Serilog + Graylog (TraceId 전파)
- **테스트**: xUnit + Testcontainers(통합), Unity Test Framework(EditMode/PlayMode E2E)

---

## 📂 프로젝트 구조

```
ServerAll/
├─ GameServer/
│  ├─ GameServer.API            # gRPC 서비스, 인터셉터, Installer, Consumer 등록
│  ├─ GameServer.Application    # 도메인 서비스 + 인터페이스 (Auth/User/DungeonLobby/GameSession/Chat)
│  ├─ GameServer.Infrastructure # EF Core · Redis 구현, MessageQueue, Consumer
│  ├─ GameServer.Domain         # 엔티티 (User, DungeonRoom ...)
│  └─ GameServer.Tests          # 단위 + 통합(Testcontainers) + 인메모리 E2E
├─ SocketServer/
│  ├─ SocketServer              # TCP Listener · Session · Room · PacketHandler
│  └─ SocketServer.Tests        # RoomManager 등 단위 테스트
├─ Shared/
│  ├─ Shared.Packet             # MemoryPack 패킷 정의 + Union 등록
│  ├─ Shared.Gameplay           # 결정론 전투 코어(수식·히트박스·태그·Effect) — 클라는 동일 DLL 공유
│  ├─ Shared.Infrastructure     # Redis MessageQueue + 게임 데이터 카탈로그(abilities/monsters/drop/level/spawn JSON 임베드)
│  └─ Shared.Contracts/Protos   # .proto (common/auth/user/lobby/chat ...)
└─ Tools/ClientCodegen          # Shared.Packet → Unity 클라 패킷 동기화

Client/Assets/Script/
├─ Network/{Https, Socket}      # gRPC 채널·서비스 / TCP 세션·패킷·핸들러(+전투 진단 링버퍼)
├─ System/                      # Auth·Startup 등 시스템 레이어 + GAS(ASC·Effect·Tag)
├─ Presentation/                # MVI 모델 (Title / DungeonLobby / InGame)
├─ Gameplay/                    # 캐릭터·전투·입력·스폰·몬스터 + SO 저작(Abilities/Monster/Loot/Maps) + 에디터 툴(Exporter·전투 트레이스 창)
├─ GUI/                         # 뷰·HUD·ViewController
├─ VContainer/                  # LifetimeScope · Installer · EntryPoint
└─ Tests/{EditMode, PlayMode}   # 단위 / Docker 대상 E2E
```

의존성 방향(서버): `API → Application ← Infrastructure`, `Application → Domain`. Application이 Infrastructure를 직접 참조하면 위반.

---

## ✅ 구현 현황

| 영역 | 상태 | 내용 |
|------|------|------|
| 서버 인프라 | ✅ | Clean Architecture, DI Installer, Serilog+Graylog 로깅 |
| 인증/세션 | ✅ | JWT(Access/Refresh), BCrypt, DeviceId 바인딩, 토큰 로테이션, 단일 기기 세션 |
| 던전 로비 | ✅ | gRPC 방 CRUD, 호스트 이양, `SubscribeRoom` Server Streaming |
| 채팅 | ✅ | Redis Streams 기반 Global/Room/Whisper |
| 게임 시작 E2E | ✅ | Outbox → `stream:game:start` → SocketServer 방 생성 → GameSessionReady(ip:port) |
| SocketServer | ✅ | TCP 입장(C_PlayerJoin)·이동(C_Move/S_Move)·Ping/Pong·HeartBeat |
| 던전 입·퇴장 일관성 | ✅ | 플레이어 단위 `PlayerLeftRoom` 이벤트 → association 정리(빈 방 삭제/호스트 이양), 재로그인 복원 차단 |
| DB/캐시 | ✅ | PostgreSQL + Redis Cache-Aside(+Delete), Testcontainers 통합 테스트 |
| Unity 클라(OutGame) | ✅ | gRPC 로그인/로비 UI, VContainer DI, MVI |
| Unity 클라(InGame) | ✅ | 캐릭터 스폰·이동 보간·HUD·타겟팅/락온·애니메이션(파라미터 구동) |
| 실시간 전투(서버 권위) | ✅ | GAS + **Ability SO 단일 저작**(스킬 추가 = 코드 0) · **Actor 통합 발동 파이프**(플레이어=몬스터 동일 경로) · 서버 게이트(쿨다운·콤보 cadence·마나) · 히트박스 판정 · CC/회피 i-frame · Co-op 부활 |
| 몬스터·던전 루프 | ✅ | 서버 권위 몬스터 AI(틱) · 몬스터 13종 · **레벨 스케일(상수 0 — 플레이어 곡선 직독)** · 변종=ID 직접 저작(보스/엘리트) · 드롭→인벤 지급 · 클리어/실패→보상→로비 복귀 · 던전 5개(L1→L30) |
| 전투 진단/무결성 | ✅ | 서버 `[CombatTrace]` + 클라 링버퍼 + 에디터 창 — **측정 우선**으로 D1(송신 직렬화)·D2(상태 시퀀스) 근본 수정, 틱레이트 조정은 "불필요" 판정 |

자세한 “무엇을·왜” 결정 로그는 [`docs/wiki/codemap.md`](docs/wiki/codemap.md), 학습 기록은 [`docs/portfolio/`](docs/portfolio/README.md) 참고.

---

## 🛠 어빌리티 연출 타임라인 툴

게임 데이터를 **ScriptableObject 로 저작 → JSON bake** 하는 파이프라인(어빌리티·몬스터·드롭·레벨·스폰) 위에, **어빌리티 연출을 시간축에서 저작**하는 커스텀 Unity 에디터를 직접 만들었습니다. (Unreal *Animation Montage* · Unity *Timeline* 참고)

![Ability Timeline Editor](assets/AbilityEditorWindow.gif)

한 `AbilityDefinition` 의 **SFX · VFX · 애니 · 메서드 호출(Event) · 판정창** 타이밍을 타임라인에서 편집하고, **라이브 3D 프리뷰**로 스크럽하며 프레임 단위로 연출을 맞춥니다.

- **시각 편집** — 종류별 트랙, 클립 드래그/리사이즈, 우클릭 추가, 다중 선택·복제·넛지, 이름 구간(Sections)·루프, 창 폭에 맞춘 반응형 룰러
- **라이브 3D 프리뷰** — `PreviewRenderUtility` 뷰포트에 액터 메시 렌더 → `PlayableGraph` 로 애니 클립을 스크럽 시각에 **양방향 샘플**(휴머노이드/제네릭 공용) → VFX 는 프리뷰 씬 소켓에 스폰 + `ParticleSystem.Simulate` 동조. **URP 는 `RenderPipeline.SubmitRenderRequest`** 로 렌더(빌트인 경로가 URP 셰이더를 마젠타로 그리는 문제를 픽셀 리드백으로 진단·회피)
- **자동화** — `cueTrigger`(enum) → `CharacterAgentAnimations` 파라미터 → AnimatorController 상태를 추론해 프리뷰 클립을 자동 해석(수동 지정 시 그게 우선)
- **2층 교리 준수** — **판정창(startup/active)은 서버로 bake**(서버 권위) · **연출(SFX/VFX/Anim/Event)은 클라 전용 — bake 안 됨.** exporter 의 allowlist 로 강제해 *"서버는 연출을 하나도 모른다"* 를 코드로 보장

> **기술**: UI Toolkit(+`.uss`) · `SerializedObject` 바인딩(`BindProperty`/`TrackPropertyValue` — 편집 중 필드 파괴 회피) · `PreviewRenderUtility` + URP Render Request · `PlayableGraph` 수동 스크럽 샘플 · 리플렉션 기반 메서드 호출(Event).
> 상세 설계·기능 백로그: [`docs/wiki/ability-timeline-tool.md`](docs/wiki/ability-timeline-tool.md)

---

## 📅 개발 로드맵

**완성 목표(DoD)**: 2명이 로비에서 방 생성·시작 → 던전 입장(서로 보임·이동) → 몬스터 협력 처치 → 클리어 → 보상 → 로비 복귀를 **전 과정 서버 권위**로. 범위 = **Co-op 던전 버티컬 슬라이스 + PVE 오픈월드 맛보기**.

```mermaid
flowchart LR
    M0["✅ M0 기반"]
    M1["✅ M1 인게임 진입"]
    M2["✅ M2 전투 코어"]
    M3["✅ M3 몬스터"]
    M4["✅ M4 던전 루프"]
    M5["🔄 M5 폴리시 + PVE 맛보기"]
    M6["M6 마감"]
    M0 --> M1 --> M2 --> M3 --> M4 --> M5 --> M6
```

| 마일스톤 | 내용 | 상태 |
|---|------|------|
| M0 | 인증·로비·채팅·게임시작 E2E·소켓 이동·던전 입퇴장·DB/캐시·Unity OutGame | ✅ |
| M1 | 인게임 진입 — 원격 캐릭터 스폰·이동 보간, 인게임 UI 전환 | ✅ |
| M2 | 실시간 전투 — 서버 권위 Attack/Hit/Damage, Health·Attribute | ✅ |
| M3 | 몬스터 — Spawn/State/AI/Dead 동기화 | ✅ |
| M4 | 던전 루프 — Clear/실패 → 보상(경험치/아이템) → 로비 복귀 | ✅ |
| **M5** | 폴리시(애니·스킬·아이템·밸런스) + PVE 맛보기 — Actor 통합 전투·전투 진단·몬스터 레벨링 완료, 잔여 = 전용 애니·VFX·맵 배경 | 🔄 |
| M6 | 마감 — 데모 영상·부하/E2E 검증·배포 문서 | 📝 |

> 단일 진실 소스: [`docs/wiki/plan.md`](docs/wiki/plan.md)

---

## 🎯 핵심 설계 결정

- **이중 서버 분리** — 대부분 로직은 gRPC(타입 안전·HTTP/2)로, 60Hz급 인게임만 TCP로. 던전은 독립 생명주기·장애 격리.
- **서버 간 Redis Streams + Outbox** — GameServer↔SocketServer 직접 RPC를 금지. DB 트랜잭션과 이벤트 발행을 Outbox로 원자화하고, Consumer Group(at-least-once, `Beginning`부터)으로 소비. 강결합·유실을 동시에 해결.
- **TCP + MemoryPack** — 인게임은 HTTP/2 프레임 오버헤드를 피해 지연을 최소화. MemoryPack으로 제로카피에 가까운 고속 직렬화.
- **퇴장 = 플레이어 단위 이벤트** — 방이 빌 때만이 아니라 **퇴장마다** 이벤트를 발행해, 어느 경우든 플레이어 association을 정리(재로그인 시 떠난 방으로 복원되는 버그 방지).
- **클라 MVI + VContainer** — View는 자기 Model만 알고(Intent→Effect→Result→Reducer→State), 네트워크/시스템 레이어와 분리. HUD 등은 생명주기에 맞춰 Addressable 런타임 로드.
- **서버 권위 전투 + 결정론 코어 공유** — 판정·수치는 서버가 소유하되, 산식(`Shared.Gameplay`)을 클라와 DLL 로 공유해 예측·표시가 어긋나지 않게. 발동 게이트(쿨다운·콤보 cadence·마나)가 패킷 연사 치팅을 서버에서 거부.
- **게임 데이터 = SO 저작 → JSON bake** — 어빌리티·몬스터·드롭·레벨·스폰을 Unity ScriptableObject 로 편집하면 exporter 가 서버 임베디드 JSON 으로 굽는다. **스킬·몬스터 추가에 서버 코드 수정 0.** 몬스터 스케일 공식조차 상수 없이 플레이어 성장 곡선을 직독.
- **측정 우선(관측 가능성)** — "체력 동기화가 느린 것 같다"는 체감을 전투 트레이스(서버 구조적 로그 + 클라 링버퍼 + 에디터 창)로 계측 → 실제 결함(송신 비직렬화·상태 스테일)만 근본 수정하고, 틱레이트 조정은 데이터로 "불필요" 판정.
- **보안** — JWT + DeviceId 바인딩(SHA256), 토큰 로테이션·재사용 탐지, 단일 기기 세션.

자세한 트레이드오프는 [`docs/portfolio/`](docs/portfolio/README.md) 챕터별 기록 참고.

---

## 🩺 문제 → 원인 → 해결 사례

실제로 겪고 근본 수정한 대표 사례들 — 증상만 덮지 않고 원인을 특정해 구조로 해결한 기록입니다.

| 증상 | 근본 원인 | 해결 |
|------|-----------|------|
| 던전 입장이 간헐적으로 안 됨 | 스트리밍 RPC의 long-lived DbContext가 **EF 추적 캐시의 stale 엔티티**를 반환 — 다른 스코프가 쓴 DB 변경을 스트림이 끝날 때까지 못 읽음 | 캐시 미스 DB 폴백은 전부 `AsNoTracking()` 원칙화 — "이벤트는 트리거일 뿐, 최신 상태는 항상 DB에서" |
| 컨테이너 재시작 후 게임 시작 영구 불가 | Redis `LOADING` 응답에 Stream Consumer가 예외로 **영구 사망** | `ResilientStreamConsumer`로 재시도·복원을 중앙화, 컨슈머 3종 이관 |
| 몬스터 HP가 줄었다 **되돌아감** | 상태 브로드캐스트 순서 역전 — 오래된 스냅샷이 늦게 도착해 최신 값을 덮음 | `S_MonsterState.Seq`를 **스냅샷 생성 시점**에 스탬프(송신 시점이면 역전을 정당화), 클라는 stale 드롭. 송신 FIFO 도입 후 실측 역전 0건 |
| 다중 스레드 소켓 송신 → 프레임 손상 위험 | 틱·핸들러 스레드가 한 소켓에 동시 `Send` (Windows에선 WSASend 원자성으로 재현 불가 — **재현 안 됨 ≠ 버그 없음**) | 세션당 bounded Channel(1024) 송신 큐 + 단일 소비자. 생산자는 `TryWrite`만(무블록), 포화 시 그 세션만 끊음(무한 큐 = DoS 벡터) |
| "체력 동기화가 느린 것 같다" (체감) | 계측 결과 공격→HP 반영 **~37ms, RTT 지배** — 서버 처리는 병목 아님 | 틱레이트 상향을 **데이터로 기각**. 측정이 "고치지 않을 것"을 결정 — 수정 전 계측 원칙 정착 |
| 측면(90°/270°) 공격이 안 맞음 | 공유 `HitboxMath` 월드→로컬 회전의 **yaw 부호 오류** — 기존 테스트가 0°/180°만 검증한 사각지대 | 부호 교정(서버·클라 공유 수식 한 곳) + 전 방향 판정 테스트 추가 |
| 콤보가 **던전에서만** 끊김 | 서버 콤보 cadence 검증에 네트워크 지터 허용치 부재 — 정상 입력이 거부돼 데미지 유실 | cadence에 지터 허용치(100ms). 타이밍 진실원은 SkillTimeline 공유 데이터로 단일화 |

> 상세 경위: [`docs/wiki/combat-diagnostics.md`](docs/wiki/combat-diagnostics.md)(계측·D1/D2) · [`docs/portfolio/`](docs/portfolio/README.md) 챕터별 학습 로그 · [`docs/wiki/codemap.md`](docs/wiki/codemap.md) 결정 로그.

---

## 🧪 테스트 & 품질

- **단위** — GameServer.Tests **384**(Application/Domain, Fake 레포), SocketServer.Tests **209**(Room/Session/전투 게이트/레벨링), Shared.Gameplay.Tests **50**(공유 수식 — 서버↔클라 parity), Unity EditMode **192**
- **통합** — GameServer.Tests/Infrastructure(**Testcontainers** 실제 PostgreSQL+Redis), 인메모리 풀스택(gRPC Host) + Consumer 파이프라인
- **E2E** — Unity PlayMode **183**(그중 **91**이 **Docker 실서버** 대상 — Auth/User/DungeonLobby/Chat/Socket, 던전 퇴장→복원 차단, 재접속 유예, 회복/사망/부활)
- **2-클라 테스트** — 자동(단일 프로세스 다중 소켓) + 수동(Unity MPPM 가상 플레이어)
- **회귀 방지 자동화** — `.claude/` Hook으로 stale Docker 이미지 가드 / proto 재생성 / 서버 변경 시 테스트·codemap 갱신 유도

---

## 🚀 실행 방법

### 요구사항
- .NET 10 SDK · Docker & Docker Compose · Unity 6000.4.8f1 (클라)

### 인프라 + 서버 기동
```bash
cd ServerAll/Infra
docker compose up -d        # PostgreSQL · Redis · GameServer · SocketServer · Graylog 등
```

포트: GameServer HTTP `5131` / gRPC `5132`, SocketServer TCP `7777`.

### 빌드 & 테스트
```bash
# 서버 빌드 (코드젠 스킵)
dotnet build ServerAll/ServerAll.sln --no-restore -p:SKIP_CODEGEN=true

# 서버 테스트
dotnet test  ServerAll/GameServer/GameServer.Tests/GameServer.Tests.csproj
dotnet test  ServerAll/SocketServer/SocketServer.Tests/SocketServer.Tests.csproj

```

> 클라이언트 컴파일 판정은 **Unity 에디터가 유일한 권위**입니다 — `Client/*.csproj` 는 Unity 생성물이라 `dotnet build` 대상이 아닙니다(상세: `CLAUDE.md` §검증 명령).

### proto 수정 시
`.proto`를 바꾸면 클라이언트 `Client/Assets/Script/Network/Https/Generated/`를 재생성해야 합니다 (명령은 [`CLAUDE.md`](CLAUDE.md) 참조).

---

## 📖 문서

| 분류 | 위치 |
|------|------|
| 코드맵 + 설계 결정 로그 | [`docs/wiki/codemap.md`](docs/wiki/codemap.md) |
| 작업 플랜(로드맵) | [`docs/wiki/plan.md`](docs/wiki/plan.md) |
| 아키텍처 / 패킷 / 소켓 / Redis / 게임 흐름 | [`docs/wiki/`](docs/wiki/) |
| 전투·GAS 설계(Actor 통합 / Ability SO / 전투 진단 / 몬스터 레벨링) | [`docs/wiki/actor-combat-architecture.md`](docs/wiki/actor-combat-architecture.md) 외 `ability-so-authoring` · `combat-diagnostics` · `monster-leveling` |
| 포트폴리오 학습 기록(챕터별) | [`docs/portfolio/README.md`](docs/portfolio/README.md) |
| 기여/작업 규칙 | [`CLAUDE.md`](CLAUDE.md), [`.claude/rules/`](.claude/rules/) |

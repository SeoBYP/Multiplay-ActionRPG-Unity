# 프로젝트 진행 로드맵 — 마일스톤 요약 (M0 → M4)

> 이 문서는 [plan.md](../wiki/plan.md)의 실행 이력을 **포트폴리오 관점**(무엇을 만들었고, 왜 그렇게 설계했으며, 어떤 트레이드오프를 택했는가)으로 한 장에 정리한 것이다.
> 세부 구현·대화 이력은 각 챕터(아래 링크)에서 다룬다.

---

## 🎯 목표 (DoD)

> 2명이 (MPPM) 접속 → 로비에서 방 생성·시작 → 던전 입장(서로 보임·이동) → 몬스터 협력 처치 → **클리어 → 보상 수령 → 로비 복귀**.
> **전 과정 서버 권위 + E2E 통과**.

Co-op 던전 **버티컬 슬라이스**가 코어. 폴리시(애니메이션·스킬·아이템·사운드)와 PVE 오픈월드는 코어 루프 이후(M5).

---

## 🧱 아키텍처 한 장 요약

```
              gRPC/HTTP                     TCP / MemoryPack
 Unity Client ──────────▶ GameServer.API   Unity Client ──────────▶ SocketServer
 (VContainer, MVI)         (Clean Arch)     (Socket layer)            (Room/Session/Tick)
                                │                                          │
                                ▼                                          ▼
                    PostgreSQL + Redis  ◀────── Redis Streams ──────▶  (게임 상태/이벤트)
                    (영속 · 캐시)              (서버 간 단일 통신 채널)
```

- **이중 서버**: 비실시간(인증·로비·채팅·결과/영속)은 **GameServer**(gRPC), 실시간(입장·이동·전투·몬스터)은 **SocketServer**(TCP). 둘은 **직접 RPC 금지 — Redis Streams로만** 통신.
- **의존성 방향**: `API → Application ← Infrastructure` (Application은 Infra를 모름).
- **클라**: `Game.GUI → Game.OutGame → Game.System → Game.Network` 단방향(asmdef 강제), 프레젠테이션은 **MVI**.

---

## 📦 마일스톤 흐름

```
✅ M0/M1  기반 — 인증·로비·채팅·소켓·DB/캐시·인게임 진입(스폰/이동/HUD)
🔄 M2     전투 코어 — Character 두 축 분리(GAS) + 서버 권위 Attack/Hit/Damage
✅ M3     몬스터 — 스폰·AI 틱·양방향 전투·클라 렌더(2인 검증)
✅ M4     던전 루프(DoD) — Clear/Fail → Exp 보상 → 로비 복귀
⬜ M5     폴리시/콘텐츠 — 애니(MotionMatching V2)·스킬·아이템·PVE 맛보기
⬜ M6     마감 — 데모·부하/E2E·배포 문서
```

---

## ✅ M0 / M1 — 기반 시스템

플레이가 가능해지기 전까지의 토대. 상세는 챕터 1~12.

| 영역 | 핵심 | 설계 결정 / 트레이드오프 | 챕터 |
|------|------|--------------------------|------|
| 인증 | JWT Access(15m) + Refresh, **DeviceId 바인딩**(SHA256(token+deviceId)), Token Rotation·재사용 탐지, 단일 기기 세션 | 토큰 탈취 방어를 위해 기기 바인딩. 단일 기기 정책으로 세션 일관성 단순화 | [2](./chapter-02-authentication.md) |
| 던전 로비 | gRPC 방 CRUD + **`SubscribeRoom` 서버 스트리밍**(방 상태 실시간 푸시) | 폴링 대신 스트리밍. Race condition·스트림 취소(OperationCanceled/RpcException 양쪽) 처리 | [3](./chapter-03-dungeon-lobby.md) |
| 채팅 | Redis **Streams** Global/Room/Whisper, `IBroadcastChannel`(fan-out) | Pub/Sub 대신 Streams(재시작 시 미처리 재처리). 소비 방식 추상화로 채팅↔작업분배 분리 | [4](./chapter-04-chat.md) |
| 게임 시작 E2E | **Outbox → Redis Stream → SocketServer 방 생성 → IP:Port 알림** | 두 서버를 직접 호출하지 않고 이벤트로 결합. `DungeonLobbyService`는 Outbox 기록까지만(책임 분리) | [5](./chapter-05-game-start-e2e.md) |
| 소켓 진입/이동 | 세션 합성(Socket/Auth/Room/PlayerState), `C_Move`/`S_Move` 릴레이, Ping/Pong, **Redis 기반 세션 검증**(C_Auth 제거) | 이동은 `session.Room` O(1) 직접 접근. TimeStamp는 클라 원본 릴레이(서버 비덮어쓰기) | [8](./chapter-08-socket-movement.md), [11](./chapter-11-socket-session-entry.md) |
| 분산 로그 | Serilog + Graylog, **TraceId 전파** | 두 서버·클라 흐름을 한 TraceId로 추적 | [6](./chapter-06-logging.md) |
| DB/캐시 | PostgreSQL + Redis **Cache-Aside + Delete**, Testcontainers 통합 테스트 | Update는 캐시 덮어쓰기 금지(DEL만) → stale 방지. DB 폴백은 항상 `AsNoTracking`(long-lived DbContext stale 버그 회피) | [7](./chapter-07-db-cache.md) |
| 클라 OutGame | gRPC 로그인/로비 UI, VContainer DI, **MVI**, Addressable 팝업 | `GrpcChannelProvider` 채널 공유, h2c용 `YetAnotherHttpHandler` | [9](./chapter-09-unity-client.md), [12](./chapter-12-addressable-popup-system.md) |
| 인게임 진입(M1) | 로컬/원격 캐릭터 스폰, **결정론 스폰**, 전원입장 게이트, HUD, 로비 복귀 | 좌표 전송 없이 `(layout, spawnIndex)`로 **서버·클라 동일 결과**(`SpawnResolver` 미러) | [10](./chapter-10-mvi-architecture.md) 외 |
| GAS 기반 | Attribute·GameplayEffect·ASC·버프/디버프 서버 동기화 | 전투 수치의 단일 모델(GAS)로 통일 | — |

---

## 🔄 M2 — 전투 코어 (GAS + 서버 권위)

**무엇** — 캐릭터를 **두 축**으로 분리하고, 기본 공격을 서버 권위로 관통시켰다.

```
[두 축 분리 (CA-1)]
 Locomotion 축 = FSM (Ground/Jump/Fall/Land)   ← 배타적 이동 모드
 Action 축    = 입력→발동 (공격/상호작용)        ← FSM 상태가 아님(AttackState/InteractState 제거)

[서버 권위 기본공격 (CA-3)]
 클라 좌클릭 → C_Attack{skillId} (트리거만)
   → 서버 CombatHandler: 시전자 위치/yaw로 SkillCatalog + HitboxMath.Overlaps 재판정(권위)
   → S_ApplyEffect{basic_attack_dmg} 방 브로드캐스트
   → 클라 EffectReceiver → ASC Health 감소 + HitStop 연출
```

**설계 결정 / 트레이드오프**
- **클라는 트리거만, 판정·데미지는 서버**. 이유 = [authority-model.md](../wiki/authority-model.md)의 4축(치팅 방지·일관성·반응성·결정론). 연출(스윙 애니·HitStop)만 입력 즉발로 반응성 확보.
- **결정론 코어를 단일 소스로**: `Shared.Gameplay`(netstandard2.1)를 서버는 프로젝트 참조, 클라는 **DLL을 Plugins/에 배치**(중복 순수코드 8개 삭제, 클라 코드 수정 0). golden 테스트로 서버↔클라 parity 보장.
- **YAGNI 정리**: 서버 권위 이관 후 죽은 로컬 GAS *ability* 경로(HitDetector·BasicAttackAbility 등) 삭제. *effect*(버프/데미지)는 유지.

**관련 챕터**: [10-gameplay-state](./chapter-10-unity-gameplay-state.md), [10-input-system](./chapter-10-unity-input-system.md)
**남은 것(M5)**: 공유 시계(StartTick 정밀 만료)·클라 예측/정정·스킬 확장.

---

## ✅ M3 — 몬스터

**무엇** — 서버가 소유하는 NPC(몬스터)의 스폰·AI·양방향 전투·클라 렌더.

```
[저작]   Map Editor(마커: Spawn/Patrol/Bounds) → spawn-layouts.json → 서버 파싱
[서버]   Room이 몬스터 상태 동거 + 단일 RoomTickService(10Hz)가 전 방 순회
            └ MonsterAiMath.Step (순수: Idle/Patrol/Chase/Attack + 매 틱 bounds.Clamp)
            └ 플레이어→몬스터: CombatHandler hitbox 판정 → Room.DamageMonster(GAS) → S_MonsterDead
            └ 몬스터→플레이어: Attack 페이즈+쿨다운 → S_ApplyEffect{monster_attack_dmg}
[클라]   MonsterEntity (RemoteDriver류 보간) — AI/물리 없음, 받은 스냅샷만 보간
```

**설계 결정 / 트레이드오프**
- **의도된 비대칭**: 몬스터 HP=서버 권위, 플레이어 HP=클라 결정론(기존). 몬스터는 서버 소유 NPC라 권위 일원화가 자연스럽다.
- **단일 `RoomTickService`**(방마다 매니저를 두지 않음 — 과분리 회피). AI *수식*만 순수 함수로 떼 단위테스트.
- **저작-런타임 분리**: 스폰/패트롤/경계는 에디터에서 데이터로 저작 → 클라 런타임은 위치 받아 인스턴스+보간만(클라에 AI 없음).
- 검증: SocketServer.Tests 43/43 + 2인 시각 검증 + E2E 3종.

---

## ✅ M4 — 던전 루프 완성 (= DoD)

**무엇** — 클리어/실패 판정 → Exp 보상 지급 → 결과 UI → 로비 복귀까지 한 판 루프를 닫았다.

```
[클리어]  몬스터 전멸 → Room.TryMarkCleared(서버 1회) → S_DungeonClear(1820) 방 브로드캐스트
              + DungeonClearMessage{RoomId,MapId,Participants} → stream:game:dungeon:result
[실패]    참가자 전원 다운 → C_PlayerDead 집계 → Room.TryMarkFailed → S_DungeonFailed(1821)
              ※ Room._outcome(Interlocked)로 클리어/실패 배타 — 동시성 1회 보장
[보상]    GameServer DungeonResultConsumer(Consumer Group)
              → SpawnLayoutTable.Get(MapId).ExpReward → 참가자 전원 ProgressionService.AddExp
              → RoomId 멱등(Redis SET claim-first, at-most-once)
[UI/복귀] 클라 InGameState.IsDungeonCleared/Failed → GameHud가 DungeonClear/Failed 패널 토글
              → ReturnToLobby(기존 던전→Main 복귀 재사용)
```

**설계 결정 / 트레이드오프**
- **보상 범위 = Exp 전용**(인벤토리 제외, YAGNI). 던전→Exp 매핑은 **Shared 카탈로그(MapId 키)** — DB `DungeonId`를 도입하지 않음(서버 간 직접 참조 금지·정적 기획데이터, MapId가 이미 메시지로 흐름).
- **멱등 지급**: 같은 RoomId가 두 번 들어와도 Redis SET claim-first로 at-most-once. 분산 결과 처리의 중복 방어.
- **진행/성장은 별도 `user_progressions` 테이블**(UserProfile 컬럼 금지) — 미래 캐릭터 귀속(원신식 교체) 대비.
- **결과는 MVI 흐름**: 패킷 → `ISocketPacketState` 이벤트 → `InGameModel`(Reducer) → State → `GameHud`(View). View는 자기 Model만 안다.

**검증**: Progression 단위/통합/E2E, DungeonFail 단위·E2E, SocketE2E 12/12. MPPM 2-client 이동/서로보임 동작 확인.

---

## 🧩 전 마일스톤 관통 — 설계 원칙

이 프로젝트가 일관되게 지킨 규칙들(개별 기능보다 이게 포트폴리오의 핵심):

1. **서버 권위 (Server Authority)** — 전투·수치·결과는 서버가 판정, 클라는 트리거+연출. 판단 기준 4축 = [authority-model.md](../wiki/authority-model.md).
2. **서버 간 결합은 Redis Streams로만** — 직접 RPC 금지. Consumer Group은 `Beginning("0")`(재시작 시 미처리 재처리), `NewMessages("$")` 금지. 복원력은 **`ResilientStreamConsumer`로 중앙화**(컨테이너 재시작 시 Redis `LOADING`에 컨슈머가 영구사망하던 버그를 한 곳에서 해결).
3. **캐시 일관성** — Cache-Aside + **Update는 DEL만**(덮어쓰기 금지) + DB 폴백은 **`AsNoTracking`**(long-lived 스트리밍 스코프의 stale 엔티티 버그 회피).
4. **결정론 공유 코어** — 스폰·전투 수식을 `Shared.*`로 두고 서버·클라가 같은 입력→같은 결과(좌표/판정 전송 최소화).
5. **클라 MVI + 레이어 단방향** — `Intent→Effect→Result→Reducer→State`, View는 자기 Model만 참조. asmdef로 의존 방향 강제(`GUI→OutGame→System→Network`).
6. **테스트 전략** — 순수 수식=단위, 인프라=Testcontainers 통합, 전 흐름=**Docker 대상 PlayMode E2E**(목 금지). 멀티플레이는 한 프로세스 다중 클라 E2E + MPPM 2-창 수동.
7. **YAGNI / 과분리 금지** — 매니저 남발·죽은 추상화 제거. 인터페이스는 (구현체 2+ / 테스트 교체 / asmdef 경계) 중 하나일 때만.

---

## 📌 현재 상태 & 다음

- **완료**: M0·M1·M3·M4(✅), M2 코어 대부분(🔄 — 정밀화·스킬 확장만 M5로). **DoD 루프는 코드·E2E로 관통**.
- **다음(M5)**: 애니메이션(MotionMatching V2)·스킬1~2·아이템 최소·소모품·PVE 오픈월드 맛보기·전투 보조(회피/CC/부활).
- **마감(M6)**: 데모 영상·부하/E2E 검증·배포/문서.

> 실시간 진척·이슈는 [GitHub Project #2](https://github.com/users/SeoBYP/projects/2)(plan.md 커밋 시 post-commit 훅 자동 동기화), 설계·이력 진실원은 [plan.md](../wiki/plan.md), 코드 위치·결정 로그는 [codemap.md](../wiki/codemap.md).

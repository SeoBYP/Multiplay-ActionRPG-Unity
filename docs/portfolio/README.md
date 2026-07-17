# 게임 서버 개발 학습 로그

Claude와의 대화를 통해 배운 내용, 틀렸던 부분, 수정 이력을 챕터별로 기록한 학습 노트입니다.

---

## 목차

> 📍 **먼저 읽기**: [프로젝트 진행 로드맵 (M0→M5 마일스톤 요약)](./00-roadmap.md) — 무엇을 만들었고 왜 그렇게 설계했는지 한 장 요약. 아래 챕터는 영역별 상세.

| 챕터 | 주제 | 상태 |
|------|------|------|
| [챕터 1](./chapter-01-architecture.md) | 아키텍처 설계 (이중 서버 / 프로토콜 / Clean Architecture) | ✅ 기록 완료 |
| [챕터 2](./chapter-02-authentication.md) | 인증 시스템 (JWT / Refresh Token / DeviceId Binding) | ✅ 기록 완료 |
| [챕터 3](./chapter-03-dungeon-lobby.md) | 실시간 던전 로비 (gRPC Streaming / Redis Pub-Sub / Race Condition) | ✅ 기록 완료 |
| [챕터 4](./chapter-04-chat.md) | 채팅 시스템 (Redis Streams / BroadcastChannel / ReadLoop 설계) | ✅ 기록 완료 |
| [챕터 5](./chapter-05-game-start-e2e.md) | 게임 시작 E2E 흐름 (GameServer → SocketServer → Client) | ✅ 기록 완료 |
| [챕터 6](./chapter-06-logging.md) | 분산 로그 시스템 (Serilog + Graylog + TraceId 전파) | ✅ 기록 완료 |
| [챕터 7](./chapter-07-db-cache.md) | DB + Redis 캐시 레이어 (Cache Aside + 통합 테스트) | ✅ 기록 완료 |
| [챕터 8](./chapter-08-socket-movement.md) | SocketServer 이동 동기화 (Session Composition / IHost / HeartBeat) | ✅ 기록 완료 |
| [챕터 9](./chapter-09-unity-client.md) | Unity 클라이언트 (gRPC + VContainer + Docker E2E) | ✅ 기록 완료 |
| [챕터 11](./chapter-11-socket-session-entry.md) | 소켓 세션 진입 흐름 (C_Auth 제거 · Redis 기반 검증 · 버그 수정) | ✅ 기록 완료 |
| [챕터 12](./chapter-12-addressable-popup-system.md) | Addressable 리소스 관리 & 공통 팝업 시스템 (AddressableLoader · Glow · MVI 연동) | ✅ 기록 완료 |
| [챕터 13](./chapter-13-monster-server-authority.md) | 몬스터 (서버 권위 NPC · 단일 RoomTickService · MonsterAiMath · 클라 보간) | ✅ 기록 완료 |
| [챕터 14](./chapter-14-dungeon-clear-loop.md) | 던전 클리어 루프 + Exp 보상 (Interlocked outcome · 멱등 지급 · MVI 결과) | ✅ 기록 완료 |
| [챕터 15](./chapter-15-loot-drop-inventory.md) | 루트/드랍 + 인벤토리 (roll/grant 책임 분리 · 줍기 서버권위 · 컨테이너/Content 슬롯) | ✅ 기록 완료 |
| [챕터 16](./chapter-16-main-loot-path.md) | Main 싱글 루트 — 클라 시뮬·렌더 + **서버 검증 B-lite**(GrantItem 파밍 핵 → ClaimKill 슬롯/쿨다운 검증·권위 roll) · 데이터 진실원 교리(SO→bake) · 공유 roll DLL | ✅ 기록 완료 |
| [챕터 17](./chapter-17-equipment-system.md) | 장비 시스템 — 정의/소유/착용 3분리 · EquipmentType 공통 enum(proto 1:1) · GetStatsAsync 단일 합산 합류 · 착용=표시 필터 + OnChanged 크로스 갱신 · ItemActionPanel 공용화 | ✅ 기록 완료 |
| [챕터 18](./chapter-18-wallet-shop.md) | 재화(Wallet) + 상점(Shop) — 골드=통화 승격(영속 경계 1곳 라우팅) · 지갑=인벤 단일값 미러 · gRPC 조회 전용(증감 RPC 없음) · 상점=지갑·인벤 조합(자기 영속 없음) | ✅ 기록 완료 |
| [챕터 19](./chapter-19-quest-system.md) | 퀘스트 시스템 — 진행=서버 권위(킬 클레임 funnel에 훅, 클라 보고 없음) · 완료=파생 상태 · 보상=조합+Claimed 선마킹(at-most-once) · GetQuests 카탈로그×상태 병합(클라 미러 없음) · DB-only · 블래스트 반경 1곳(CollectItem 훅 보류) | ✅ 기록 완료 |
| [챕터 20](./chapter-20-content-pipeline-addressables.md) | 던전 메타 + Addressables 데이터 파이프라인 — `MapId` 식별자 통일(서버권위 영속) · 검증=Application(DIP) · 던전 선택 UI(proto+MVI) · expReward export 왕복 결함 · Resources 폐기→자산성격별 도구(async / WaitForCompletion / 이동만) · root-relative key · 인증 레이스 게이트 · 실패 사유 클라 추론+피드백 일관화 | ✅ 기록 완료 |
| [챕터 21](./chapter-21-connection-liveness-hp-authority.md) | 연결 생존성 & HP 서버 권위 — 하트비트(설계 존재 ≠ 클라 사용) · ASC HP 베이스라인 desync(동기화됨 ≠ 적용됨, 마지막 1마일) · 연결 불변식 E2E 커버리지 가드(Stop 훅) | ✅ 기록 완료 |
| [챕터 22](./chapter-22-hud-windows-mvi.md) | HUD 창 MVI 확장(스탯·퀘스트추적·판매) — 공용 GameHud + Main 전용 의존 = 선택 주입(TryResolve) · GUI↔System 레이어 변환(StatLine/bool 헬퍼) · 신호 구독은 받을 수 있는 레이어에 · 단일 토글 funnel | ✅ 기록 완료 |
| [챕터 23](./chapter-23-mana-resource-authority-ability.md) | 전투 자원(마나) 서버 권위 + GameplayAbility 식별 — 자원 권위의 목적 구분(HP=사망감지 vs 마나=발동게이트) · 예측 수렴(리젠은 동기화 안 함, 단일소스 상수) · 원자 게이트(TryBeginDodge) · 베이스라인 일치(입장 1회 정정) · YAGNI(Ability=엔진 신설 아닌 경량 식별+로그) · 만들기 전에 존재 확인(HUD 마나바 이미 완비) | ✅ 기록 완료 |
| [챕터 24](./chapter-24-coop-revive.md) | Co-op 부활 — 서버 권위 골격 재사용(입력→검증→브로드캐스트) · 원자적 멱등 검증(중복 C_Revive 거부) · 한 기능이 과거 결정 뒤집기(원격 다운 Destroy→보존) · **DI vs 컴포넌트**("아예 안 되던" 진짜 원인: 입력은 DI 아닌 GetComponent) · 전체 PlayMode가 인접 스폰 테스트 DI 회귀 포착 | ✅ 기록 완료 |
| [챕터 25](./chapter-25-lock-on-targeting.md) | 타겟팅/락온 — **"패킷 0"의 근거**(락온=권위 없는 조준 보조, facing은 이미 동기화됨) · 이동과 facing 두 축 분리(락온 스트레이프가 공짜로 나옴) · Unity `== null`의 거짓말(fake null → 죽은 적에 카메라 영구 잠김, `ReferenceEquals`로 수정·테스트 포착) | ✅ 기록 완료 |
| [챕터 26](./chapter-26-measured-combat-cleanup.md) | AC 트랙: **측정이 이끈 전투 정리** — CombatTrace 계측(서버 구조적 로그+클라 링버퍼+에디터 창)이 틱레이트 조정을 "불필요" 판정하고 진짜 결함 2건 노출 · D2=Seq **스냅샷 시점** 스탬프 · D1=bounded 송신 큐("재현 안 됨≠버그 없음") · 몬스터 레벨링 **상수 0 스케일**(플레이어 곡선 직독) · 변종=**ID 직접 저작**(배율 간접층 기각) · 데이터 전부 SO→bake | ✅ 기록 완료 |

---

## 기록 방식

각 챕터는 아래 형식으로 작성:

- **처음 내가 생각한 것** — 대화 전 내 이해 수준
- **피드백으로 수정된 것** — 틀렸거나 부족했던 부분
- **추가로 배운 것** — 몰랐던 개념, 키워드
- **아직 미완성인 것 (TODO)** — 구현해야 할 항목
- **핵심 키워드 정리** — 나중에 복습용 한 줄 정리

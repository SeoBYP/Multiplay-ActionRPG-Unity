# 게임 서버 개발 학습 로그

Claude와의 대화를 통해 배운 내용, 틀렸던 부분, 수정 이력을 챕터별로 기록한 학습 노트입니다.

---

## 목차

> 📍 **먼저 읽기**: [프로젝트 진행 로드맵 (M0→M4 마일스톤 요약)](./00-roadmap.md) — 무엇을 만들었고 왜 그렇게 설계했는지 한 장 요약. 아래 챕터는 영역별 상세.

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

---

## 기록 방식

각 챕터는 아래 형식으로 작성:

- **처음 내가 생각한 것** — 대화 전 내 이해 수준
- **피드백으로 수정된 것** — 틀렸거나 부족했던 부분
- **추가로 배운 것** — 몰랐던 개념, 키워드
- **아직 미완성인 것 (TODO)** — 구현해야 할 항목
- **핵심 키워드 정리** — 나중에 복습용 한 줄 정리

# 게임 서버 개발 학습 로그

Claude와의 대화를 통해 배운 내용, 틀렸던 부분, 수정 이력을 챕터별로 기록한 학습 노트입니다.

---

## 목차

| 챕터 | 주제 | 상태 |
|------|------|------|
| [챕터 1](./chapter-01-architecture.md) | 아키텍처 설계 (이중 서버 / 프로토콜 / Clean Architecture) | ✅ 기록 완료 |
| [챕터 2](./chapter-02-authentication.md) | 인증 시스템 (JWT / Refresh Token / DeviceId Binding) | ✅ 기록 완료 |
| [챕터 3](./chapter-03-dungeon-lobby.md) | 실시간 던전 로비 (gRPC Streaming / Redis Pub-Sub / Race Condition) | ✅ 기록 완료 |
| [챕터 4](./chapter-04-chat.md) | 채팅 시스템 (Redis Streams / BroadcastChannel / ReadLoop 설계) | ✅ 기록 완료 |
| [챕터 5](./chapter-05-game-start-e2e.md) | 게임 시작 E2E 흐름 (GameServer → SocketServer → Client) | ✅ 기록 완료 |
| [챕터 6](./chapter-06-logging.md) | 분산 로그 시스템 (Serilog + Graylog + TraceId 전파) | ✅ 기록 완료 |
| [챕터 7](./chapter-07-db-cache.md) | DB + Redis 캐시 레이어 (Cache Aside + 통합 테스트) | ✅ 기록 완료 |

---

## 기록 방식

각 챕터는 아래 형식으로 작성:

- **처음 내가 생각한 것** — 대화 전 내 이해 수준
- **피드백으로 수정된 것** — 틀렸거나 부족했던 부분
- **추가로 배운 것** — 몰랐던 개념, 키워드
- **아직 미완성인 것 (TODO)** — 구현해야 할 항목
- **핵심 키워드 정리** — 나중에 복습용 한 줄 정리

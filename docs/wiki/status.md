# 구현 현황 (포인터)

> ⚠️ 이 문서는 더 이상 현황의 진실원이 아니다. 과거 stale 내용을 제거하고 포인터로 축소했다 (부채 9.7 해소, 2026-06-07).

현황·진행·다음 작업은 아래 단일 진실원에서 확인한다:

| 알고 싶은 것 | 문서 |
|---|---|
| **지금 무슨 Phase / 무엇이 남았나** | [plan.md](plan.md) ← 진실원 |
| 일정·진척 보드 | [GitHub Project #2](https://github.com/users/SeoBYP/projects/2) |
| 도메인별 파일 위치 + 설계 결정 로그 | [codemap.md](codemap.md) |
| 아키텍처 (레이어·의존 방향) | [architecture.md](architecture.md) |
| 클라 vs 서버 권위 모델 | [authority-model.md](authority-model.md) |
| 패킷 규칙 | [packets.md](packets.md) |
| SocketServer | [socketserver.md](socketserver.md) |
| 서버 연동 흐름 | [gameflow.md](gameflow.md) |
| Redis | [redis.md](redis.md) |
| 멀티플레이 테스트(MPPM/E2E) | [mppm-testing.md](mppm-testing.md) |

> 통신 구조 요약: Unity Client ──gRPC──▶ GameServer(인증/로비/채팅) · ──TCP──▶ SocketServer(인게임 실시간).
> 서버 간은 직접 RPC 없이 **Redis Streams**로만 통신한다.

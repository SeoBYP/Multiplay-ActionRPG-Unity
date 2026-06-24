# 테스트 규칙

## 테스트 메서드 이름

게임플레이·도메인 동작 테스트의 메서드 이름은 한국어로 작성한다.

```csharp
// 올바른 예
[Test] void L키_누르면_ToggleLobby_라우팅된다()
[Test] void 일반공격_어빌리티는_대상_체력을_감소시킨다()
[Test] void 공격_입력이_없으면_AttackState로_전환되지_않는다()

// 잘못된 예
[Test] void PressLKey_RoutesToToggleLobby()
```

## E2E 테스트 (PlayMode)

E2E 테스트는 Docker 서버를 대상으로 실행한다. 목(mock)으로 서버를 대체하지 않는다.

| 테스트 클래스 | 검증 범위 |
|--------------|-----------|
| `AuthE2ETests` | 회원가입/로그인/Refresh/Logout 전체 흐름 |
| `UserE2ETests` | 닉네임 설정/중복/금지어 |
| `DungeonLobbyE2ETests` | 방 생성/입장/시작, SubscribeRoom 스트림 |
| `ChatE2ETests` | Global/Room/Whisper 수신 |
| `SocketE2ETests` | gRPC 로비 → TCP 인증/입장 → 이동 브로드캐스트 |

베이스: `Tests/PlayMode/E2E/E2ETestBase.cs` (채널/서비스 생성, RegisterAndLogin 공통 헬퍼)

### ⚠️ E2E 실행 전 Docker 서버 이미지 신선도 확인 (필수)

E2E는 실행 중인 Docker 서버를 때린다. **소스보다 이미지가 오래되면 옛 서버를 검증해 거짓 실패(주로 전부 타임아웃)** 가 난다. Stop 훅의 stale-image guard가 불일치를 경고하니, 경고가 뜨면 리빌드부터:

```powershell
docker compose -f ServerAll/Infra/docker-compose.yml build gameserver socketserver
docker compose -f ServerAll/Infra/docker-compose.yml up -d gameserver socketserver
```

MPPM 멀티 클라/플레이 테스트 전반은 [docs/wiki/mppm-testing.md](../../docs/wiki/mppm-testing.md) 참조.

## 네임스페이스 주의

테스트 네임스페이스에 `System` 세그먼트 포함 금지.  
`System`이 포함되면 전역 `System` 네임스페이스를 가릴 수 있다.

```csharp
// 금지
namespace Game.Tests.System.Combat { }

// 허용
namespace Game.Tests.Combat { }
```

## 연결 처리(소켓) E2E 커버리지 정책 (필수)

**연결/세션 생명주기 동작은 전부 테스트가 있어야 한다.** 해피패스(입장·이동·전투·클리어)만 E2E하고 *liveness/실패모드*를 빠뜨린 탓에, 클라 하트비트 누락(무이동 60s → 서버 유휴 타임아웃으로 끊김)이 조용히 새어 플레이 중에야 발견됐다. 같은 일이 재발하지 않도록 아래를 지킨다.

**연결 처리 소스를 바꾸면 반드시 대응 테스트를 함께 추가/갱신한다:**
- 소스 = `Client/Assets/Script/Network/Socket/**`, `ServerAll/SocketServer/SocketServer/**`, 패킷 정의(`Shared.Packet`, 클라 `Network/Socket/Packets`).
- 테스트 = PlayMode E2E `Tests/PlayMode/E2E/Network/Socket/`(Docker 대상) 또는 빠른 단위 `Tests/PlayMode/Network/Socket/`(Fake 커넥터) 또는 `SocketServer.Tests`.

**반드시 커버해야 하는 연결 불변식(체크리스트):**
- 세션 검증/거부 — 배정 없는 UserId·방 불일치·상태 없음 입장 거부
- keep-alive — 무이동 유휴에도 하트비트로 연결 유지(서버 타임아웃 회피)
- 서버발 끊김 감지 — 서버가 끊으면 클라 `State=Disconnected` + `OnDisconnected` 발화
- 재접속/유예 — 강제끊김 후 재접속·크래시 유예 보존·명시퇴장/전원끊김/유예만료 거부
- 브로드캐스트 — 입장/이동/퇴장 상호 수신

**시간 기반(느린) 테스트 작성법:**
- 서버 타임아웃 상수(방 60s·로비 30s)는 실시간이라 그 이상 대기하는 E2E엔 `[Timeout(180000)]` 부여 + `UniTask.Delay(..., ignoreTimeScale: true)`.
- 세션을 오래 살려야 하면 `ConnectJoinedSessionAsync(..., CancellationToken.None)` — 짧은 `Timeout()` 토큰을 세션 수명에 넘기면 그 토큰이 세션을 조기 종료시킨다(연결 끊김 오관측).
- 빠른 단위(하트비트 송신 등)는 Fake `ISocketConnector` + 짧은 `HeartbeatInterval` 주입으로 60s 대기 없이 검증.

> Stop 훅 `.claude/hooks/check-network-e2e-coverage.ps1` 이 연결 소스 변경 시 소켓 테스트 동반 변경이 없으면 경고한다(누락 방지). 경고가 뜨면 테스트를 추가하거나, 정말 불필요하면(순수 리팩터) 그 이유를 말하고 끝낸다.

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

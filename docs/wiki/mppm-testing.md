# MPPM 멀티 클라이언트 플레이 테스트

> Multiplayer Play Mode(MPPM)로 한 머신에서 2+ 클라이언트를 띄워 코옵을 검증하는 방법과,
> 2026-06-03 검증 세션에서 발견한 **블로커 2건**을 기록한다.
> 관련 plan 태스크: M1 마지막 두 줄 (`S_DungeonReady` → **MPPM 2-client** 검증).

---

## 1. 동작 원리 (이미 구현됨 — 추가 코드 불필요)

핵심: [`EditorAutoLoginInitializer`](../../Client/Assets/Script/System/Auth/EditorAutoLoginInitializer.cs) (`#if UNITY_EDITOR`).

에디터에서 Play를 누르면 Title 씬 없이 **게스트 계정으로 자동 로그인**한다. 이때 MPPM 가상 플레이어를
`CurrentPlayer.Tags`로 인지해 **인스턴스마다 다른 계정**으로 분리한다:

```csharp
var tags = CurrentPlayer.Tags;
if (tags is { Count: > 0 })
    return $"guest_{hash}_{tag}@editor.test";  // 가상 플레이어: 태그로 계정 분리
return $"guest_{hash}@editor.test";            // 태그 없을 때
```

- 메인 에디터  → 태그 `Player 1` → `guest_{hash}_player_1@editor.test`
- 가상 플레이어 → 태그 `Player 2` → `guest_{hash}_player_2@editor.test`

계정이 없으면 [`AuthService.LoginOrRegisterAsync`](../../Client/Assets/Script/System/Auth/AuthService.cs)가 **자동 회원가입**까지 처리.
등록 위치: [`AuthInstaller`](../../Client/Assets/Script/VContainer/Installers/AuthInstaller.cs) (`RegisterEntryPoint`, EDITOR 전용 → 빌드 제외).

### 왜 단일 기기 세션 정책에 안 걸리나
가상 플레이어는 같은 머신이라 `SystemInfo.deviceUniqueIdentifier`(=DeviceId)가 **동일**하다.
하지만 "마지막 로그인 기기만 유효" 정책은 **계정(userId) 단위**라, 서로 다른 계정 A·B가
같은 기기에서 각 1회 로그인하는 건 충돌하지 않는다. → 태그로 계정을 가르는 위 설계가 이 함정을 정확히 우회한다.

> ⚠️ **태그를 안 주면** 가상 플레이어도 `guest_{hash}@editor.test`(태그 없음)로 떨어져
> 메인과 **같은 계정**이 된다 → 단일 기기 세션 정책에 걸려 한쪽이 튕긴다. **태그 분리 필수.**

---

## 2. 실행 순서

1. **Docker 서버 기동** (자동 로그인이 실서버를 때림 — GameServer gRPC 5132/HTTP 5131, SocketServer TCP 7777).
   안 켜면 로그인 실패 후 Play 모드 자동 종료.
2. `Window > Multiplayer > Multiplayer Play Mode` → Player 2 활성화 → **태그 지정**(예: `Player2`).
   최초 활성화 시 클론 프로세스가 에셋 임포트로 수 분 소요(1회만).
3. **메인 에디터에서 Play 1번** → 모든 활성 가상 플레이어가 같이 Play 진입. (인스턴스마다 따로 누를 필요 없음)
4. 각 인스턴스가 다른 게스트로 로그인 → 한쪽이 방 생성 → 다른 쪽이 입장 → 시작 → 소켓 접속 → 같이 플레이.

### MCP 자동화 한계 (중요)
UnityMCP 브리지는 **메인 에디터에만** 붙는다(인스턴스 목록에 메인 1개만 잡힘).
가상 플레이어는 별도 프로세스라 **에이전트가 클릭/로그확인을 직접 못 한다.**
→ MPPM 2-창 수동 플로우에서 Player 2는 **사람이 직접 조작**해야 한다.
→ 자동·반복 검증은 MPPM이 아니라 **E2E 테스트**(`SocketE2ETests` 등, 한 프로세스에서 다중 클라)로 한다.

---

## 3. 2026-06-03 검증 결과

### ✅ 통과: 메인 에디터 자동 로그인 (서버 도달 정상)
```
[EditorAutoLogin] 게스트 로그인 시도: guest_7a91914b_player_1@editor.test
Success
[AuthService] ApplyLogin — PublicId=U2eQB7FvhQ CurrentRoomId=0 HasPending=False
[EditorAutoLogin] 게스트 로그인 성공
```
- MPPM이 메인 에디터까지 `Player 1` 태그를 부여 → 메인/가상이 깔끔히 갈림.

### ✅ 블로커 ① [해소됨]: SocketE2ETests 6/6 전부 타임아웃 — **stale 서버 이미지**

> **2026-06-03 해소**: 두 서버 이미지 리빌드(`docker compose build gameserver socketserver` → `up -d`) 후
> SocketE2ETests **6/6 통과**(10.8초). stale 이미지가 원인이었음이 확정됨.
`run_tests`(PlayMode, MCP) 결과 6개 전부 `OperationCanceledException: The operation was canceled`(= `Timeout()` 발화):
```
✗ SocketSession_두_클라이언트_인증후_입장_성공     ← 핵심: 두 명 방 입장
✗ RawSocket_호스트가_Move_전송하면_게스트가_S_Move_수신
✗ RawSocket_호스트가_퇴장하면_게스트가_S_PlayerLeft_수신
✗ 강제_연결_끊김_후_재접속_성공
✗ Disconnected_상태에서_재시도로_입장_성공
✗ 게스트_부분퇴장후_재로그인시_방복원_안되고_호스트는_유지
```
**원인 (거짓 실패):** 실행 중 Docker 이미지가 소스보다 오래됨.
- `infra-gameserver` 이미지 2026-06-01 17:06Z / `infra-socketserver` 2026-05-31 15:50Z
- 소스 변경 2026-06-03 04:38Z — 특히 오늘 커밋 **`877cdfee fix(server): 캐시 어사이드 Get에 AsNoTracking 적용`** 미반영.

기본 인증+입장까지 전부 타임아웃 = "입장 흐름이 완료되지 않음" → codemap에 기록된 그 버그와 정확히 일치:
*"SendLoop이 `Starting`을 계속 읽어 `GameSessionEvent` 대신 `UpdateEvent`만 전송 → 클라가 던전 입장 못 함"*.
이 fix가 없는 옛 서버라 클라가 `GameSessionEvent`를 못 받아 입장 완료를 영원히 대기.

**해결:** 두 서버 이미지 리빌드 후 재실행.
```powershell
cd ServerAll/Infra
docker compose build gameserver socketserver
docker compose up -d gameserver socketserver
```
> Stop 훅(stale-image guard)이 PlayMode E2E 직전에 이 불일치를 자동 경고함. **경고 뜨면 리빌드 먼저.**

### ✅ 블로커 ② [해소됨]: Main 씬 DI 에러 — `CharacterSpawner` 의존성 미등록
자동 로그인 중 Main 씬에서 발생:
```
VContainerException: Failed to resolve Game.Gameplay.Character.CharacterSpawner
: No such registration of type: Game.System.Player.LocalPlayerContext
```
**근본원인:** `CharacterSpawner` 생성자가 `LocalPlayerContext`와 `SpawnLayoutProvider`를 주입받는데,
이 둘은 [`DungeonLifetimeScope`](../../Client/Assets/Script/VContainer/LifetimeScopes/Scenes/DungeonLifetimeScope.cs)에만
등록돼 있고 [`MainLifetimeScope`](../../Client/Assets/Script/VContainer/LifetimeScopes/Scenes/MainLifetimeScope.cs)에는
빠져 있었다(소켓/Auth 의존성은 부모 Singleton이라 resolve됨). 결정론 스폰 도입 시 Main 미러링이 누락된 회귀.
에러는 생성자 첫 미해결 의존(`LocalPlayerContext`)만 보고했으나 실제로는 2건 누락.

> **2026-06-03 해소**: `MainLifetimeScope`에 `LocalPlayerContext` + `SpawnLayoutProvider` 등록 2줄 추가
> (Dungeon 스코프 미러링). Main은 네트워크 미연결이라 `SpawnLayoutProvider`는 생성자 충족용이고
> 런타임 `Get()`은 호출되지 않음. Play 검증: VContainerException 소멸 + `[CharacterSpawner] 로컬 캐릭터 스폰 완료`,
> 에러 0건.

---

## 4. "MPPM 2-client 검증" 통과 전 선행 조건

plan.md M1 마지막 태스크(**MPPM 2-client "서로 보임·이동" 검증**)를 신뢰성 있게 통과시키려면:

1. ~~**서버 이미지 리빌드** (블로커 ①)~~ → ✅ 해소, SocketE2ETests 6/6 그린.
2. ~~**`LocalPlayerContext` 등록/스코프 정리** (블로커 ②)~~ → ✅ 해소, Main 씬 스폰 정상.
3. 위 둘 그린 → **이제 MPPM 2-창 수동(Player 2 사람 조작)으로 "서로 보임·이동" 시각 검증 가능.**
   (단, 이 시각 검증은 던전 씬 진입 후 원격 캐릭터 스폰까지 돼야 하며, plan M1의
   `로컬 스폰`·`원격 스폰`·`S_DungeonReady` 태스크가 선행이다.)

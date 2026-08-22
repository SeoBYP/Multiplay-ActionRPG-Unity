# 11. 소켓 진입 — 인증을 한 번 더 하지 않기로 한 결정

> **한 줄** — Docker를 재시작하자 **아무도 던전에 들어갈 수 없게** 됐다. 원인은 소켓 인증이 인메모리 딕셔너리에 의존한 것이었고, 해법은 그 딕셔너리를 Redis로 바꾸는 게 아니라 **소켓 전용 인증 자체를 없애는 것**이었다. 패킷 2개(`C_Auth`/`S_Auth`)를 삭제했다.
>
> **범위** 소켓 입장 검증 재설계 · 이벤트/데이터 순서 · 상태머신 실패 경로 · 멱등 가드 · 재시작 복구
> **검증** `SocketE2ETests`(세션 배정 없는 UserId 거부 등) · `GameSessionConnectorE2ETests`

---

## 1. 증상 — 재시작 후 무한 재접속 루프

```
ConnectionReset (10054) from 127.0.0.1
ConnectionReset (10054) from 127.0.0.1
ConnectionReset (10054) from 127.0.0.1      ← 끝없이 반복
```

원래 흐름은 이랬다.

```
GameStartRequestedMessage 수신 → Room 생성 → _userRoomIndex[userId] = roomId   ← 메모리
C_Auth { UserId } 수신          → _userRoomIndex 조회 → S_Auth { Success }
```

Docker를 재시작하면 `_userRoomIndex`가 비고, 모든 `C_Auth`가 실패한다. 클라는 끊고 재시도하고, 또 실패한다.

**설계 의도와 실제가 어긋나 있었다** — SocketServer는 stateless를 지향했는데 **정작 인증이 stateful**이었다. 인게임 상태(위치·HP)는 메모리에 있어도 되지만, **입장 자격은 프로세스보다 오래 살아야 한다.**

## 2. 질문을 바꿨다 — "왜 두 번 인증하지?"

처음 든 생각은 "인메모리를 Redis로 바꾸자"였다. 그전에 한 단계 위를 봤다.

```
클라 ──JWT──▶ GameServer   로그인·방 생성·입장·시작  ← 매 RPC마다 검증됨
                  │
                  └─ GameSessionReadyEvent(ip, port) ─▶ 클라
클라 ──TCP──▶ SocketServer  C_Auth { UserId }         ← 여기서 또 인증?
```

**GameServer가 이미 전부 검증했다.** 방을 만들고 시작할 수 있었다는 것 자체가 인증을 통과했다는 뜻이다.

> ⚠️ 다만 "SocketServer 주소를 아는 클라는 검증된 클라다"는 논리만으로는 **부족하다** — 주소는 비밀이 아니고 포트는 스캔된다. 실제 보증은 다음 절의 **Redis 배정 레코드**가 한다. 그 레코드는 GameServer가 세션을 만들 때만 생기고, 없으면 입장이 거부된다. 즉 **"인증을 없앤" 게 아니라 "인증의 근거를 GameServer가 남긴 사실로 옮긴" 것**이다.

## 3. 채택 — 선기입된 배정 레코드로 검증

```
[GameServer] GameSessionReadyConsumer
   ① GameSession 생성(DB)
   ② HSET gamesession:player:{userId}  roomId / gameSessionId / nickname / spawnIndex   (TTL 2h)
   ③ PublishAsync → 구독자에게 GameSessionReadyEvent(ip, port)

[SocketServer] C_PlayerJoin { RoomId, UserId }
   → HGETALL gamesession:player:{userId}
   → 키 없음        ⇒ 거부 "Player not assigned to any session"
   → roomId 불일치  ⇒ 거부 "Room assignment mismatch"
   → 통과 ⇒ session.UserId / Nickname 세팅 → 방 입장 → S_PlayerJoined
```

패킷 두 개가 사라졌다.

```
[제거] C_Auth(1300) / S_Auth(1301)      ※ Union ID 1300번대가 통째로 비었다
[변경] C_PlayerJoin 에 UserId 추가
[클라 상태머신 단순화]
   이전: Idle → Connecting → Connected → Authenticating → Authenticated → Joining → Joined
   이후: Idle → Connecting → Connected → Joining → Joined
```

| | 인메모리 `_userRoomIndex` | Redis 배정 레코드 |
|---|---|---|
| 프로세스 재시작 | 소실 → 전원 입장 불가 | 남아 있음(TTL 2h) |
| 수평 확장 | 인스턴스끼리 공유 불가 | 모든 인스턴스가 같은 것을 본다 |
| 인증 책임 | SocketServer가 독자 보유(중복) | GameServer 검증 결과를 그대로 사용 |

> **교훈** — 기능이 고장 났을 때 **고칠 대상이 그 기능이 아닐 수 있다.** 인증을 고치는 대신 인증을 지웠고, 결과적으로 패킷·상태·핸들러가 함께 줄었다. 중복을 제거하면 고칠 곳도 줄어든다.

## 4. 순서가 계약이다 — 쓰고 나서 알린다

3절 다이어그램에서 **②와 ③의 순서가 전부**다.

```
② HSET → ③ Publish      클라가 이벤트를 받는 시점에 데이터는 이미 있다  ✅
③ Publish → ② HSET      클라가 먼저 도착 → 키 없음 → 입장 거부         💥
```

분산 시스템에서 이벤트는 **"이 데이터를 보라"는 포인터**다. 포인터를 대상보다 먼저 발행하면 경쟁이 생긴다. 이건 [03](./chapter-03-dungeon-lobby.md)에서 정한 "이벤트는 ID + 다시 읽어라"의 이면이다 — **다시 읽으라고 말하기 전에 읽을 것을 놓아둬야 한다.**

## 5. 실패 경로가 없으면 무한히 기다린다

`S_PlayerJoined { Success = false }`를 받았는데 클라가 **영원히 대기**했다.

```csharp
await UniTask.WaitUntil(() => State == Joined || State == Failed);   // 이 조건이 절대 참이 안 됨
...
// 원인: 실패를 Failed 가 아니라 Connected 로 처리하고 있었다
State = joined.Success ? Joined : Connected;   // ❌  Connected 는 대기 조건에 없다
State = joined.Success ? Joined : Failed;      // ✅
```

증상은 "무한 대기"였지만 실제 손해는 그 다음이었다 — 30초 뒤 하트비트가 연결을 끊고, 다시 붙고, 다시 대기하는 **사이클**이 됐다. 1절의 재접속 폭풍에는 이 버그도 섞여 있었다.

재연결 조건도 같이 고쳤다.

```csharp
// Failed 상태에서도 재시도를 허용해야 두 번째 이벤트로 복구된다
if (State != Idle && State != Disconnected && State != Failed) throw ...;
```

> **교훈** — 상태 열거형에 `Failed`가 있는 것과 **실패했을 때 그리로 가는 것**은 다른 문제다. 성공 경로만 보고 짜면 실패는 "아무 상태나"로 떨어지고, 대기하는 쪽은 그 사실을 알 방법이 없다.

## 6. 같은 이벤트는 두 번 온다고 가정한다

```
[GameSessionConnector] GameSessionReady 수신 — roomId=30
[GameSessionConnector] GameSessionReady 수신 — roomId=30   ← 중복
[GameSessionConnector] TCP 연결 시도 ×2
```

구독 시작 시의 즉시 전송(kick)과 `StartGame` 재트리거 경로가 **각각** 이벤트를 만들 수 있었다. 재구독·재시도·네트워크 복구가 있는 한 중복은 정상이다.

```csharp
// 이미 진행 중이면 무시. 단 Failed 는 통과시킨다(두 번째 이벤트가 곧 복구 기회다)
if (state != Idle && state != Disconnected && state != Failed) return;
```

**멱등 가드는 "중복을 막는 것"이 아니라 "중복이 와도 결과가 같게 만드는 것"** 이다. 그래서 무조건 무시가 아니라 **어떤 상태에서 무시할지**를 정해야 한다 — `Failed`까지 무시했다면 5절에서 만든 복구 경로가 막혔을 것이다.

## 7. 내가 로딩 중인 핸들을 남이 해제한다

```
[LobbyViewController] RoomDetail 로드 실패: Attempting to use an invalid operation handle
```

```
NavigateToRoom  구독 → OpenRoomDetailAsync()  (Addressable 로딩 시작, await 중)
NavigateToGame  구독 → CloseRoomDetail()      (같은 핸들을 Release)  💥
```

두 구독이 **같은 리소스를 서로 모른 채** 만지고 있었다. 로컬 Docker라 gRPC 응답이 빨라서 "로딩이 끝나기 전에 게임이 시작되는" 타이밍이 쉽게 재현됐다.

수정은 잠금이 아니라 **해제 주체를 하나로 줄이는 것**이었다 — `NavigateToGame`에서 `CloseRoomDetail()` 호출을 뺐다. 씬이 전환되면 VContainer 스코프가 dispose되고 `LobbyViewController.Dispose()`가 정리한다.

> **교훈** — 비동기 로딩의 수명은 **로딩을 시작한 컨텍스트**가 소유해야 한다. 제3자가 중간에 해제할 수 있는 구조라면 락을 걸 게 아니라 **해제 권한을 회수**하는 편이 맞다. ([04](./chapter-04-chat.md)의 "정리 코드도 같이 분리해야 한다"와 같은 문제의 다른 얼굴이다.)

## 8. 재시작 복구 — 이벤트를 보내기 직전에 보장한다

Docker 재시작으로 Redis가 비면 3절의 배정 레코드도 사라진다. 이미 `Playing`인 방에 재접속하면 다시 입장 거부다.

두 겹으로 막았다.

```
① SubscribeRoom 재구독 시 자동 재트리거 대상을 Starting → Playing 까지 확장
     (호스트가 다시 붙으면 StartGame 을 멱등 재실행 → 메시지가 다시 흐른다)

② GameSessionReadyEvent 를 보내기 "직전"에 레코드 존재를 보장
     EnsurePlayerDataInRedisAsync(roomId, gameSessionId)
       → 방 참가자를 DB에서 조회
       → 키가 없는 참가자만 HSET (+TTL 2h)
```

②가 핵심이다. **"복구를 어디서 할 것인가"의 답을 '이벤트 전송 직전'으로 잡으면 경쟁 조건이 사라진다.** 클라가 이벤트를 받았다는 것은 곧 레코드가 있다는 뜻이 된다(4절과 같은 원리). 호스트뿐 아니라 게스트에게도 자동으로 적용된다.

## 9. 그 이후 — 이 검증 지점 위에 쌓인 것들

`C_PlayerJoin` 핸들러는 이 챕터에서 만든 골격을 유지한 채 계속 자랐다.

```
Redis 배정 검증 (이 챕터)
  → 방 조회 · 입장
  → PlayerState 존재 확인   ← 없으면 거부 = "게임 미시작" 또는 "재접속 유예 만료"
  → MarkJoined(userId)      ← 이제부터 몬스터 AI 타깃 + 끊김 보존 상태 해제(즉시 복귀)
  → S_PlayerJoined(MapId·SpawnIndex) + S_PlayerMana(서버 권위 기준선 정렬)
```

- **재접속 유예** — 세션이 죽어도 `PlayerState`는 방에 남는다([08](./chapter-08-socket-movement.md) 2절). 그 보존 상태를 되살리는 지점이 여기다.
- **죽은 세션 인수** — 유예 위에서 또 다른 문제가 드러난다. 서버가 아직 "죽은 줄 모르는" 세션이 자리를 잡고 있으면 재입장이 거절된다. 감지에 60초 넘게 걸려 **재입장이 30번 거절**된 사건이 [29](./chapter-29-multiplayer-sync-invisible-failures.md)에 있다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 입장 자격은 공유 저장소가 소유 | SocketServer 재시작·다중 인스턴스에도 입장 유지 |
| 데이터 먼저, 이벤트 나중 | 서버 간 메시지의 공통 순서 규칙 |
| 실패도 명시적 상태로 | 연결 생존성·끊김 감지 E2E 커버리지([21](./chapter-21-connection-liveness-hp-authority.md)) |
| 이벤트 핸들러는 멱등하게 | 던전 결과 보상·루팅 지급의 중복 방어([14](./chapter-14-dungeon-clear-loop.md)·[15](./chapter-15-loot-drop-inventory.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-11-socket-session-entry.md](../learning-log/chapter-11-socket-session-entry.md)

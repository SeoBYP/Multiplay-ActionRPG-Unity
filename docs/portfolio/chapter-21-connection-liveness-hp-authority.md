# 21. 연결 생존성과 HP 베이스라인 — "동기화됐다"와 "적용됐다"는 다르다

> **한 줄** — 플레이 중 만난 두 버그 모두 **증상이 나타난 곳과 원인이 있는 곳이 달랐다.** 전투가 멈춘 원인은 전투가 아니라 연결이었고, 패배 판정이 늦은 원인은 밸런스가 아니라 **클라와 서버가 다른 HP에서 출발**한 것이었다. 그리고 둘 다 **해피패스 E2E만 있어서** 플레이 중에야 발견됐다.
>
> **범위** keep-alive · 끊김 감지 · 서버 권위 수치의 베이스라인 · 실패 모드 테스트 커버리지
> **결과물** 연결 불변식 E2E 커버리지 정책 + Stop 훅 가드

---

## 1. 전투가 멈췄는데 원인은 전투가 아니었다

```
InvalidOperationException: SocketSession is not joined
```

던전에서 가만히 서 있다가 전투를 시작하면 터졌다. 전투 코드 문제로 보였지만, **그 시점에 세션은 이미 끊겨 있었다.** 전투 패킷은 그저 **끊긴 연결을 처음으로 건드린 코드**였을 뿐이다.

### 원인 — 클라가 "움직일 때만" 말을 걸었다

```
[서버] HeartbeatService  15초마다 스윕
         SessionTimeout 30s / RoomPlayerTimeout 60s
         LastRecvAt(모든 수신 패킷이 갱신)이 넘으면 킥

[클라] 이동할 때만 C_Move 송신
         가만히 서 있음 → 60초 무송신 → 서버가 유휴로 판단해 끊음
```

**서버는 "살아 있다"를 트래픽으로 판단하는데, 클라는 할 말이 있을 때만 말했다.**

### 흥미로운 지점 — 하트비트는 이미 설계돼 있었다

서버에는 `PingPongHandler`(C_Ping → S_Pong)가 있었고, 테스트용 `DummyClient`에는 `PingLoop`까지 있었다. **정작 실제 Unity 클라에만 송신이 구현돼 있지 않았다.**

> **"설계했다"와 "모든 클라가 쓴다"는 다르다.** 프로토콜에 있고, 서버가 처리하고, 테스트 클라가 쓰고 있으면 "구현됐다"고 착각하기 쉽다. 실제로 그 기능을 필요로 하는 클라가 쓰고 있는지는 **별도의 사실**이다.

```csharp
// SocketSession.cs:38 — 서버 타임아웃(60s)의 1/4 주기
public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
// :84  Connected~Joined 동안 주기적으로 C_Ping → LastRecvAt 갱신
```

## 2. 끊김을 클라가 알아야 한다

살아 있게 만드는 것과 **죽었을 때 아는 것**은 다른 문제다. 서버가 끊어도 클라는 다음 송신을 시도할 때까지 몰랐다.

```csharp
// ISocketSession.cs:16
event Action OnDisconnected;
// SocketSession.cs:22 — 의도적 종료는 끊김으로 오관측하지 않는다
private bool _intentionalDisconnect;
```

`State = Disconnected` 전이 + `OnDisconnected` 발화를 넣고, HUD가 구독해 끊김 팝업(→로비 복귀)을 띄운다.

**의도적 종료를 분리한 것이 핵심이다.** 이걸 구분하지 않으면 정상 퇴장할 때마다 "연결이 끊겼습니다" 팝업이 뜬다 — 같은 판단을 채팅 방 전환([04](./chapter-04-chat.md) 5절)과 소켓 E2E 종료([09](./chapter-09-unity-client.md) 7절)에서도 했다.

## 3. 두 번째 버그 — 내가 틀린 진단부터

**증상**: 몬스터에게 맞아 죽으면 **로컬에선 이미 쓰러졌는데 몬스터가 계속 때리고, 패배 패널이 한참 뒤에** 떴다.

**내 첫 진단**: "슬라임 공격력 밸런스 문제"(1뎀 × 100HP라 오래 걸린다). **틀렸다.**

사용자가 정확히 좁혀줬다 — *"다운이 되고 나서도 몬스터가 때린다. **[로컬 다운]과 [S_PlayerDead 수신] 사이에 왜 Delay가 있냐**"*.

**이 질문이 문제를 재정의했다.** 밸런스라면 둘 다 똑같이 느려야 한다. 둘 **사이**에 간격이 있다는 건 **두 판정이 서로 다른 값을 보고 있다**는 뜻이다.

## 4. 근본 원인 — 두 HP가 다른 곳에서 출발했다

```
서버 던전 HP = 레벨 MaxHealth (예: 140)    ← InitPlayerState(playerInfo.MaxHealth)
클라 ASC HP  = 프리팹 기본값 100            ← InitializeAttributes 기본값

데미지 누적
   → 클라는 100에서 먼저 0 도달 → 로컬 다운 예측 발동
   → 서버는 아직 40 남음 → 그 동안 몬스터의 타격이 계속 유효
   → 40만큼 늦게 S_PlayerDead 도착
```

**Delay의 크기가 곧 베이스라인 차이(140−100)였다.**

여기서 사용자의 두 번째 지적이 핵심이었다 — *"스텟 동기화는 기본 아니야?"*

**스텟은 이미 동기화되고 있었다.** 로그인·레벨업 후 `GetProgression`이 `PlayerProgressionHolder.Stats`(MaxHealth/Atk/Def)를 채운다. 빠진 것은 동기화가 아니라 **그 값을 ASC에 꽂는 마지막 한 걸음**이었다.

> **desync는 "안 받았다"보다 "받고도 안 적용했다"에서 더 자주 난다.** 데이터가 흘러온 것과 **판정 지점이 그 값을 읽는 것**은 다르다. "동기화 완료"를 체크리스트로 넘기면 이 구간이 보이지 않는다.

## 5. 패킷 0개로 해결했다

```
PlayerProgressionHolder.Stats.MaxHealth      ← 이미 서버에서 받아둔 값
      │  CharacterSpawner: TryResolve → Bind
      ▼
PlayerStatApplier.Bind(holder) → ASC.SetMax(Health, holder.MaxHealth)
      ▼
로컬 다운 예측이 읽는 ASC HP = 서버와 같은 베이스라인 → Delay 소멸
```

새 패킷도, 새 RPC도 필요 없었다. **값은 이미 클라 손에 있었다.**

권위 구조는 그대로다 — 서버가 HP를 소유하고 사망을 선언한다. 바뀐 것은 **클라 예측의 출발점**뿐이다. 예측(prediction)은 서버와 같은 초기 상태에서 시작해야 수렴한다. 출발점이 다르면 아무리 정확히 계산해도 어긋난다.

> "동기화가 필요하다"는 진단에서 곧바로 패킷을 추가하지 않은 게 이 수정의 값어치다. **먼저 "그 값이 이미 어딘가에 있지 않은지" 확인**하면 프로토콜이 늘지 않는다. (같은 판단이 [23](./chapter-23-mana-resource-authority-ability.md) 5절에서 한 번 더 나온다 — 만들려던 HUD가 이미 있었다.)

## 6. 왜 둘 다 플레이 중에야 발견됐나

두 버그의 공통점은 **테스트가 잡을 수 없는 자리에 있었다**는 것이다.

```
있던 E2E   입장 → 이동 → 전투 → 클리어          (해피패스 프로토콜 흐름)
없던 E2E   가만히 있기 · 서버가 끊기 · 잘못된 입장 · 오래 버티기
                                                  (생존성 / 실패 모드)
```

**해피패스 테스트는 항상 바쁘다.** 계속 패킷을 보내므로 유휴 타임아웃에 걸릴 일이 없고, 짧게 끝나므로 60초를 넘길 일도 없다. **버그를 재현하려면 아무것도 하지 않아야** 했다.

HP 베이스라인도 마찬가지다 — 테스트는 죽을 때까지 맞지 않았다.

## 7. 그래서 정책과 가드를 세웠다

**① 커버리지를 채웠다** — 유휴 상태에서 하트비트로 연결 유지 / 서버발 끊김 시 `State=Disconnected` + `OnDisconnected` 발화 / 배정 없는 UserId 입장 거부.

**② 시간 기반 테스트 작성법을 규칙화했다.** 서버 타임아웃이 실시간이라 그냥 짜면 반드시 깨진다.

```
[Timeout(180000)] + UniTask.Delay(..., ignoreTimeScale: true)
세션을 오래 살려야 하면 세션 수명에 짧은 취소 토큰을 넘기지 말 것
   → 그 토큰이 세션을 조기 종료시켜 "끊김"으로 오관측된다
```

**③ 60초를 기다리지 않는 빠른 단위 테스트도 만들었다.**

```
SocketSessionHeartbeatTests
  FakeConnector (PingCount 카운트, 수신 루프는 취소까지 열어둠)
  + 짧은 HeartbeatInterval 주입
  → Docker 불필요, 60초 대기 불필요
```

**같은 불변식을 두 층에서 검증**한다 — 빠른 단위는 "핑을 보내는가", 느린 E2E는 "실제 서버가 안 끊는가". 전자는 매번 돌리고 후자는 회귀 확인용이다.

**④ Stop 훅으로 갭 재발을 막았다.** `check-network-e2e-coverage.ps1`이 연결 처리 소스(`Network/Socket/**`, `SocketServer/**`, 패킷 정의)가 바뀌었는데 대응 소켓 테스트가 함께 바뀌지 않으면 경고한다. 훅 주석에 이유가 박제돼 있다 — *"liveness/connection bugs silently slipped because E2E covered only happy-path protocol flows."*

> CI가 없는 환경([09](./chapter-09-unity-client.md) 9절)에서 **"잊어버림"이 가장 큰 실패 원인**이라, 실행 자동화보다 누락 감지를 먼저 세웠다.

## 8. 상수가 남긴 대가

```
RoomPlayerTimeout 60s + CheckInterval 15s  →  죽은 세션을 감지하는 데 최대 75초
```

이 값들은 **끊긴 연결을 얼마나 참아줄지**의 선택이다. 짧으면 순간 렉에도 킥되고, 길면 죽은 세션이 오래 남는다.

후자의 대가는 나중에 실제로 청구됐다 — **서버가 아직 죽은 줄 모르는 세션이 자리를 점유해 재입장이 30번 거절**된 사건이다. 해법은 타임아웃을 줄이는 게 아니라 **인수(takeover)** 였다. → [29](./chapter-29-multiplayer-sync-invisible-failures.md) 4절

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 예측의 출발점을 서버와 맞춘다 | 마나 베이스라인도 입장 시 1회 정정([23](./chapter-23-mana-resource-authority-ability.md)) |
| 의도적 종료 ≠ 끊김 | 재접속·유예 처리 전반([29](./chapter-29-multiplayer-sync-invisible-failures.md)) |
| 연결 불변식은 반드시 E2E | `.claude/rules/testing.md`의 체크리스트로 박제 |
| 같은 불변식을 두 층에서 | 빠른 단위 + 느린 E2E 조합 |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-21-connection-liveness-hp-authority.md](../learning-log/chapter-21-connection-liveness-hp-authority.md)

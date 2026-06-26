# 챕터 21 학습 로그 — 연결 생존성 & HP 서버 권위 동기화

> 플레이 중 "전투가 갑자기 멈추고 끊긴다", "패배 패널이 한참 뒤에 뜬다" 두 버그를 쫓다가 만난 두 교훈.
> ① **연결은 트래픽이 없어도 살아 있어야 한다**(하트비트). ② **서버 권위 수치는 "동기화했다"가 아니라 "어디까지 적용됐나"가 핵심**(HP 베이스라인 desync).
> 그리고 둘 다 *해피패스 E2E만 있고 실패/생존 모드 커버리지가 없어서* 플레이 중에야 발견됐다 — 그래서 **"모든 연결 불변식은 E2E가 있어야 한다"** 가드를 세웠다.

---

## 1. 연결 생존성 — "하트비트를 왜 구현했는데 안 쓰였나"

### 증상

던전에서 가만히 서 있다가 전투를 시작하면 `InvalidOperationException: SocketSession is not joined` 가 나면서 전투가 멈췄다. 처음엔 전투 코드 문제로 보였지만, 진짜 원인은 **그 전에 세션이 이미 끊겨 있었다**는 것이다.

### 근본 원인 — 클라가 "움직일 때만" 패킷을 보냈다

서버 `HeartBeatService`는 방 플레이어를 **무이동 60초**면 유휴로 보고 킥한다(`LastRecvAt` 기준, 모든 수신 패킷이 갱신). 그런데 Unity 클라는 **이동(C_Move) 때만** 패킷을 보냈다 — 가만히 서 있으면 60초 후 서버가 끊는다.

흥미로운 건 **하트비트 설계는 이미 있었다**는 점이다. 서버엔 `PingPongHandler`(C_Ping→S_Pong)가 있고, 테스트용 `DummyClient`엔 `PingLoop`가 있었다. **정작 실제 Unity 클라에만 하트비트 송신이 구현돼 있지 않았다.** "설계했다 ≠ 모든 클라가 쓴다"의 전형.

```
[서버] HeartBeatService: 15s마다 스윕, RoomPlayerTimeout=60s
          LastRecvAt(수신마다 갱신)이 60s 넘으면 → 킥

[클라-기존] 이동할 때만 C_Move 송신
   가만히 서있음 → 60s 무송신 → 서버가 유휴로 끊음 → 다음 전투 패킷이 "not joined"

[클라-수정] SocketSession 하트비트 루프
   Connected/Joined인 동안 15s마다 C_Ping → LastRecvAt 갱신 → 유지
```

### 수정

`SocketSession`에 하트비트 루프를 넣었다 — 연결/입장 상태인 동안 주기적으로 `C_Ping`을 보내 `LastRecvAt`를 살린다. 추가로 **서버발 끊김을 클라가 감지**하도록 `ISocketSession.OnDisconnected` 이벤트 + `State=Disconnected` 전이를 넣고, HUD가 이를 구독해 끊김 팝업(→로비 복귀)을 띄운다. 의도적 종료(`_intentionalDisconnect`)는 끊김으로 오관측하지 않게 분리.

---

## 2. HP 서버 권위 동기화 — "스텟 동기화는 기본 아니야?"

### 증상

던전에서 몬스터에게 맞다 죽으면 **로컬에선 이미 쓰러졌는데(다운) 몬스터가 계속 때리고, 패배 패널(S_PlayerDead)이 한참 뒤에** 떴다.

### 내가 처음 틀린 진단

처음엔 "슬라임 공격력 밸런스(1뎀 × 100HP)" 문제로 봤다. **틀렸다.** 사용자가 정확히 짚어줬다 — "다운이 되고 나서도 몬스터가 때린다 / **[로컬 다운]과 [S_PlayerDead 수신] 사이에 왜 Delay가 있냐**". 증상은 밸런스가 아니라 **두 HP가 다른 값에서 출발**한 것이었다.

### 근본 원인 — ASC HP 베이스라인이 prefab 기본값(100)에 머물렀다

```
서버 던전 HP = 레벨 MaxHealth (예: 140)   ← RoomManager.CreateRoom → InitPlayerState(playerInfo.MaxHealth)
클라 ASC HP = 프리팹 기본 100             ← InitializeAttributes 기본값, 레벨 스탯 미반영

데미지 누적 → 클라는 100에서 먼저 0(로컬 다운 예측) → 서버는 아직 140 중 100 남음
   → 그 사이(140-100=40만큼) 몬스터는 계속 유효 타격 → S_PlayerDead 지연
```

여기서 사용자의 "스텟 동기화는 기본 아니야?"가 핵심이었다. **스텟은 이미 동기화되고 있었다** — 로그인/킬 후 `GetProgression`이 `PlayerProgressionHolder.Stats`(MaxHealth/Atk/Def)에 들어온다. 빠진 건 동기화가 아니라 **마지막 1마일: 그 MaxHealth를 ASC에 *적용*하는 것**이었다. 값은 손에 있는데 ASC에 안 꽂혀 있었다.

### 수정 — 패킷 추가 없이 클라 내부에서 적용

```
PlayerProgressionHolder.Stats.MaxHealth (이미 서버에서 pull됨)
      │  CharacterSpawner: TryResolve(holder) → Bind
      ▼
PlayerStatApplier.Bind(holder) → ASC.SetMax(Health, holder.MaxHealth)
      ▼
로컬 다운 예측(다운-게이트)이 읽는 ASC HP = 서버와 같은 베이스라인 → Delay 소멸
```

서버 던전 HP는 이미 레벨 MaxHealth로 맞아 있었으니 **새 패킷이 필요 없었다** — 클라가 이미 가진 값을 ASC에 적용하기만 하면 됐다. 권위는 서버에 그대로 두고, 클라 예측의 출발점만 서버와 일치시킨 것.

> 교훈: "동기화됨"을 체크리스트로 끝내지 말 것. 데이터가 흘러온 것과 **그 데이터가 실제 판정 지점(ASC)에 적용된 것**은 다르다. desync는 "안 받았다"보다 "받고도 안 꽂았다"에서 더 자주 난다.

---

## 3. "다시는 조용히 새지 않게" — 연결 불변식 E2E 커버리지 가드

두 버그의 공통점: **해피패스(입장·이동·전투·클리어)만 E2E**가 있고, *생존성/실패 모드*엔 테스트가 없어서 플레이 중에야 터졌다. 그래서 정책 + 자동 가드를 세웠다.

- **누락됐던 커버리지를 채움**(PlayMode E2E): idle 타임아웃에도 하트비트로 연결 유지 / 서버발 끊김 → `State=Disconnected`+`OnDisconnected` / Auth 전 Join 거부.
- **시간 기반 테스트 작성법**을 규칙화: 서버 타임아웃(방 60s)은 실시간 → `[Timeout(180000)]` + `UniTask.Delay(ignoreTimeScale:true)`. 세션을 오래 살릴 땐 짧은 `Timeout()` 토큰을 세션 수명에 넘기지 말 것(그 토큰이 세션을 조기 종료 → 끊김 오관측). 빠른 단위는 Fake `ISocketConnector` + 짧은 HeartbeatInterval 주입으로 60s 대기 회피.
- **Stop 훅 가드**(`check-network-e2e-coverage.ps1`): 연결 처리 소스(`Network/Socket/**`, `SocketServer/**`, 패킷 정의)를 바꾸면 대응 소켓 테스트 동반 변경이 없을 때 경고 → 커버리지 갭 재발 방지. 규칙은 `.claude/rules/testing.md`에 불변식 체크리스트로 박제.

---

## 처음 생각한 것 → 피드백으로 수정된 것

- **"전투가 멈춘 건 전투 버그"** → 아니다. 그 전에 **세션이 유휴 타임아웃으로 끊겨** 있었다. 증상 지점(전투)과 원인 지점(연결)이 달랐다.
- **"패배가 늦은 건 슬라임 밸런스"** → 아니다. **클라 ASC HP(100)와 서버 HP(140)의 베이스라인 desync**. 사용자가 "다운과 S_PlayerDead 사이 Delay"로 정확히 좁혀줬다.
- **"스텟을 동기화하면 되겠네(패킷 추가)"** → 이미 동기화돼 있었다. 빠진 건 **ASC 적용(마지막 1마일)**뿐 → 패킷 0개로 해결.

## 핵심 키워드

- **하트비트(keep-alive)**: 트래픽이 없어도 연결은 살아야 한다. `LastRecvAt` + 주기 Ping. "설계 존재 ≠ 모든 클라가 사용".
- **베이스라인 일치**: 클라 예측의 출발 수치를 서버 권위 값과 맞춘다. 권위는 서버, 출발점만 동기.
- **마지막 1마일**: pull한 데이터 ≠ 판정 지점에 적용된 데이터. desync는 후자에서 난다.
- **생존성/실패 모드 E2E**: 해피패스만으론 부족. liveness·auth-order·server-disconnect를 커버. Stop 훅으로 갭 재발 방지.
- **시간 기반 테스트**: `ignoreTimeScale` + 넉넉한 `[Timeout]`, 세션 수명에 짧은 취소 토큰 금지.

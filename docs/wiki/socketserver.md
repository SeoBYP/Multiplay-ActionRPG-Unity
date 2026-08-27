# SocketServer 패턴

## 방 구조 — Room 은 구성 루트다

`Room` 은 상태를 직접 들지 않는다. **협력자 넷을 조립**하고, 자기 일(생명주기 오케스트레이션)만 한다.

```
Room  (RoomId · MapId · Bounds)
 ├─ Sessions   RoomSessions      연결 집합 · 정원 · 브로드캐스트
 ├─ Actors     ActorStore        누가 있나(플레이어+몬스터) · 효과 적용
 ├─ Loot       GroundItemStore   바닥 아이템 · 줍기 경쟁 중재
 ├─ Progress   DungeonProgress   클리어 · 실패 · 다운 · 부활
 └─ (private)  RoomSimulation    한 틱 → 액터 결정을 패킷으로 번역

Room 이 직접 하는 일 = Join · Leave · MarkJoined · SweepExpiredDisconnected
  (Sessions 와 Actors 를 **함께** 만져야 하는 조율이라 나눌 수 없다)
```

**핸들러는 협력자를 직접 부른다.** Room 을 파사드로 쓰지 않는다 —
어느 하위 시스템을 건드리는지 호출부에 드러나야 한다.

```csharp
room.Sessions.Broadcast(packet);
room.Actors.SetPosition(userId, x, y, z, rotY);
room.Actors.DamageMonster(instanceId, mods);
room.Progress.MarkDowned(userId);
room.Loot.TryPickup(picker.PosX, picker.PosZ, groundId);
```

## Actor — 캐릭터·전투의 단일 표현

플레이어든 몬스터든 싸우는 것은 전부 `Actor` 다. 종족은 **타입 계층이 아니라 데이터**(`ActorKind`).

```
Actor (abstract)              신원(ActorId) · 공간(Pos/RotY) · 수명
 ├─ Gas  AbilitySystemComponent   HP·마나·스탯·태그·활성 Effect·쿨다운  ← 전투 상태 일체
 │        └ Shared.Gameplay 소유. **클라 `GasComponent`(MonoBehaviour)도 같은 타입을 들고 위임한다**
 ├─ PlayerActor               회피 무적창 · 콤보 cadence
 └─ MonsterActor              MonsterId · Level · Tier · Phase · Patrol · dirty-flag · Seq
```

- **ActorId 부호 규약**: 양수 = 플레이어(UserId) / 음수 = −InstanceId(몬스터). 진실원 = `Shared.Gameplay/Actors/ActorIds.cs`.
- **Actor 는 UserId·닉네임·접속 여부를 모른다.** 그건 방 참가자(`RoomMember`)의 속성이고, 몬스터에겐 없는 개념이다.
- **역참조 금지**: 방향은 항상 `Session → Room → RoomMember → Actor` 단방향.
  Session 은 재접속마다 교체되므로 액터가 붙들면 유령 참조가 된다.

### Actor.Tick — 액터가 자기 결정을 소유한다

```csharp
ActorTickResult Tick(dt, nowMs, targets, bounds)   // { TargetIndex, Cast, ExpiredEffectIds }
```

- `MonsterActor` 가 **이동·페이즈 결정 + 어빌리티 선택 + 쿨다운 커밋**까지 한다.
- `RoomSimulation` 은 그 결과를 **패킷·피해로 번역**만 한다.
  데미지 산정은 대상 방어력을 읽어야 해서 방에 남긴다 — 액터가 다른 액터를 뒤지면 경계가 무너진다.
- 액터는 패킷을 모른다.
- **지속 Effect 만료도 액터가 소유한다** — 기본 `Actor.Tick` 이 `Gas.TickEffects(nowMs)` 를 돌려 만료된
  인스턴스 id 를 결과에 실어 보내고, 방은 그것을 `S_RemoveEffect` 로 번역하기만 한다.

## Session Composition

```
Session
  ├── (네트워크) Socket, Connected, LastRecvAt
  ├── (인증)    UserId, Nickname     ← C_PlayerJoin 의 Redis 검증 통과 시 세팅
  └── Room?                          ← 입장 성공 시 직접 참조
```

이동 패킷에서 `session.Room` 으로 O(1) 직접 접근. `RoomManager.GetRoom()` 탐색은 입장/퇴장에서만.

## 참가자·액터 생성 타이밍

```
GameStartRequestedMessage 수신 → RoomManager.CreateRoom
                                   ├ Room.AddPlayer(...)     ← 참가자 + 액터 생성(소켓 입장 전)
                                   └ Room.SpawnMonsters(...) ← 몬스터 액터 생성
C_PlayerJoin 수신              → Room.MarkJoined(userId)     ← 조회·활성화만(재생성 금지)
```

누가 들어올지 이미 알고 있으므로 미리 세팅한다. 늦게 만들면 입장 레이스가 생긴다.

**`HasJoined` 가 AI 타깃 자격의 일부다** — 아직 소켓 입장 전인 플레이어를 몬스터가 죽이면
`S_PlayerDead` 가 빈 방에 발행돼 유실된다.

## 세션과 액터는 수명이 다르다 (재접속의 근거)

```
t=0    TCP 끊김(FIN 없음)   Sessions 에서 제거 · RoomMember.DisconnectedAtMs 마킹
                            액터는 그대로 살아 있다(위치·HP 보존)
t<60s  재접속               MarkJoined → 마킹 해제 → 원래 자리로 복귀
t>60s  유예 만료            SweepExpiredDisconnected → 참가자·액터 함께 제거
```

이 비대칭이 재접속을 가능하게 하는 구조 그 자체다. 그래서 저장소도 나눈다.

## 다운·사망은 태그다

별도 집합(`_downed`)을 두지 않는다. 다운 = 액터의 `GameplayTags.Dead`.

- `Gas.AddTag/RemoveTag` 의 bool 반환이 그대로 **dedup**(사망 통지 1회)·**멱등**(중복 부활 차단) 가드다.
- **`C_PlayerDead` 만으로는 다운되지 않는다** — `DungeonProgress.MarkDowned` 가 서버 HP 0 을 확인한다.
  만피인 채로 자기신고해 AI 타깃에서 빠지는 구멍을 막는다(다운도 서버 권위).

## 상태이상(CC)은 서버가 걸고 서버가 푼다

```
몬스터 발동 → RoomSimulation
   ├ 서버 액터에 적용   Gas.ApplyEffect(def, id, nowMs)   ← 활성 목록 + GrantedTags
   └ 클라에 통지        S_ApplyEffect { InstanceId = 적용된 id }

다음 틱들 → Actor.Tick → Gas.TickEffects(nowMs)
   └ 만료분 → S_RemoveEffect { InstanceId }
```

- **브로드캐스트만 하던 시절의 구멍**: 서버 액터에 흔적이 없어 `IsActivationBlocked` 가 스턴 중에도 false 였다.
  스턴을 무시하는 클라의 `C_Attack` 을 서버가 거를 근거가 아예 없었다(지금은 `CombatHandler` 가 거른다).
- **태그는 저장하지 않고 파생한다** — `HasTag` 가 직접 부여 태그 ∪ 활성 Effect 의 `GrantedTags` 를 합산한다.
  회수 장부를 두면 같은 스턴을 두 개 맞았을 때 하나가 끝나며 잘못 떼는 사고가 나고, 그 순간 영구 스턴이 된다.
- **브로드캐스트하는 id 는 `ApplyEffect` 의 반환값**이다. 스택 정책이 기존 인스턴스를 재사용하면
  방금 뽑은 id 는 버려지므로, 그걸 그대로 보내면 만료 통지와 짝이 어긋나 클라에 CC 가 영영 남는다.

## 이동 동기화 정책

서버가 클라이언트 `TimeStamp` 를 **그대로 릴레이**. 덮어쓰지 않는다.
이유: 다른 클라이언트 보간이 원본 발생 시점 기준으로 계산해야 정확하다.

```csharp
room.Sessions.Broadcast(new S_Move { TimeStamp = packet.TimeStamp }, excludeSessionId: session.SessionId);
```

## 락 규칙

| 락 | 보호 대상 | 잡는 곳 |
|---|---|---|
| `ActorStore.SyncRoot` | 액터 + 참가자(둘은 항상 함께 바뀐다) | 저장소 메서드 내부. 복합 연산(틱)은 호출자가 잡고 `ActorsLocked`/`MembersLocked` 로 순회 |
| `AbilitySystemComponent.SyncRoot` | 한 액터의 속성·태그·활성 Effect·쿨다운 | 컴포넌트 내부. **틱 스레드와 핸들러 스레드가 동시에 만진다** |
| `RoomSessions` 내부 | 연결 집합 | 자체 |
| `GroundItemStore` 내부 | 바닥 아이템 | 자체 |

- **`AbilitySystemComponent` 는 스스로 스레드 안전하다.** 예전엔 락이 방/저장소에 있어 *저장소를 지나는 경로만* 안전했고,
  핸들러가 `Gas` 를 직접 만지는 경로(마나 차감·쿨다운·회피)가 구멍이었다.
- 락 중첩을 만들지 않는다. 특히 액터 락 안에서 아이템 락을 잡지 않는다 —
  줍기는 시전자 위치를 **먼저 스냅샷**해 저장소에 넘긴다.

## 서비스 구조 (BackgroundService)

| 클래스 | 책임 |
|--------|------|
| `TcpListenerService` | TCP 소켓 생명주기 |
| `GameStartRequestedConsumer` | MQ 소비 → Room 생성 → 참가자·몬스터 액터 생성 → GameSessionReady 발행 |
| `RoomTickService` | 10Hz 로 모든 방 `Room.Tick` + 브로드캐스트 + 재접속 유예 스윕 |
| `HeartbeatService` | 유휴 타임아웃 감시 |
| `PlayerConsumedConsumer` | 크로스 서버 소비(회복) 통지 — 방 단위 멱등 |

## 알려진 한계

- `RoomManager.GetAssignedRoom` — 인메모리 인덱스라 프로세스 재시작에 소실된다(인증 근거로 쓰지 않는다).
- 공간 분할 없음 — 한 방 최대 액터 15(몬스터 11 + 플레이어 4)라 아직 불필요.
  착수선 = 한 방 100+ 또는 틱이 100ms 예산의 20% 초과(**틱 프로파일 미실측**).

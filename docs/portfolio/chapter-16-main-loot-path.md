# 16. 싱글 플레이의 보상 — 내가 틀린 설계를 스스로 깬 기록

> **한 줄** — "싱글 플레이니까 클라가 판정해도 된다"고 설계했다가 **무한 파밍 핵**을 막지 못한다는 걸 발견하고 뒤집었다. 교정의 핵심은 검증을 강화한 게 아니라 **클라가 보고하는 내용을 바꾼 것**이다 — "이 아이템을 달라"에서 "이 슬롯을 죽였다"로.
>
> **범위** 권위 경계 재설계 · 원자적 쿨다운 · 데이터 진실원 · 클라 시뮬 컴포넌트
> **정본** [main-spawn-claim.md](../wiki/main-spawn-claim.md) · [authority-model §4b](../wiki/authority-model.md)

---

## 1. 1차 설계와 그 논리

던전(Co-op)은 경쟁과 치팅 방지 때문에 SocketServer가 전부 판정한다([15](./chapter-15-loot-drop-inventory.md)). Main은 싱글이라 **동기화할 상대가 없다.** 그래서 이렇게 갔다.

```
클라가 로컬에서 몬스터를 잡고 → 줍는 순간 GrantItem(itemId, qty) gRPC 1번

서버 가드 3겹:
  ① 인증      AuthInterceptor(JWT)
  ② 수량 상한  MaxGrantPerCall = 99
  ③ 정의 검증  ItemCatalog.Get(itemId) == null 이면 거부
```

세 겹이면 충분해 보였다. 아이템을 지어낼 수도 없고(③), 한 번에 많이 받을 수도 없다(②).

## 2. 무엇이 구멍이었나

**이 가드들은 한 호출의 위조 폭만 제한하고, 호출 빈도는 전혀 막지 못한다.**

```
LocalMonster 를 무한 스폰 (스폰 결정권이 클라에 있다)
    → 잡는다 (판정도 클라)
    → GrantItem(qty ≤ 99) 연타
    → 가드 3겹을 전부 통과하면서 무한 파밍 → 사실상 만렙
```

각 호출은 **완벽하게 합법**이다. 인증됐고, 수량 제한 안이고, 존재하는 아이템이다. 그런데 전체는 부정이다.

### "싱글이니까 안전하다"가 거짓이 되는 지점

**진행이 서버 계정에 영속되는 순간**이다. 솔로 플레이어도 자기 계정을 치팅할 수 있고, 그 계정은 언젠가 Co-op·랭킹·거래 같은 사회적 맥락과 닿는다. "혼자 노는데 무슨 상관이냐"는 **게임이 정말로 로컬에서 끝날 때만** 성립한다.

### 근본 정리

> **클라가 저작한 이벤트는, 서버가 그 콘텐츠를 소유하지 않는 한 검증할 수 없다.**

"내가 몬스터를 잡았다"는 주장은 서버가 몬스터의 존재를 모르면 참·거짓을 판정할 방법이 없다. 이 상태에서 할 수 있는 건 rate-limit뿐인데, 그건 파밍 **속도만 늦추는 반창고**이지 차단이 아니다.

이건 플레이어 HP를 서버 권위로 올릴 때 이미 배운 것과 같은 교훈이었다 — **"클라가 할 수 있다"와 "클라가 소유해야 한다"는 다르다**([authority-model §0](../wiki/authority-model.md)). 루트에서는 한 박자 늦게 깨달았다.

## 3. 교정 — 질문을 바꾼다

고민을 "어떻게 더 막을까"에서 **"클라가 무엇을 보고하게 할까"** 로 바꿨다.

서버는 이미 던전용으로 **맵 스폰 데이터**를 갖고 있었다(`spawn-layouts`). Main 맵을 여기에 추가하면, 서버가 **어느 자리에 어떤 몬스터가 있는지**를 알게 된다. 그러면 클라의 킬을 **슬롯 단위로 검증**할 수 있다.

```
[클라] LocalMonster(slot) 킬 → 바닥 오브 → E 줍기
          → ClaimKill(mapId, slotId)          ← "보상을 달라"가 아니라 "이 슬롯을 죽였다"
                     │
[서버]  ① 슬롯이 그 맵에 존재하는가        (SpawnLayoutTable — 위조 슬롯/맵 거부)
        ② per-user 쿨다운이 지났는가        (Redis SET NX PX — 재청구 차단)
        ③ 서버 권위 roll                    (DropTableCatalog.Roll — 클라 roll 불신)
          → GrantItemAsync → granted[] 반환
```

**클라는 더 이상 보상의 내용을 말하지 않는다.** 사실(어느 슬롯이 죽었다)만 보고하고, 무엇을 받을지는 서버가 정한다.

`GrantItem(itemId, qty)` gRPC는 **제거**했다 — 치팅 진입점 자체를 없앴다. 도메인 메서드 `GrantItemAsync`는 살아서 `ClaimKill`과 던전 `LootGrantConsumer`가 계속 쓴다.

> proto 파일에 그 이유가 박제돼 있다 — `inventory.proto:15` *"구 GrantItem(itemId,qty)을 대체(클라가 보상 임의지정 = 무한파밍 핵 차단)"*.

## 4. 파밍률을 맵 설계로 상한 짓기

```csharp
// RedisClaimCooldownStore.cs:18 — 한 줄이 "점유 + TTL" 을 원자 처리
=> _redis.StringSetAsync(key, "1", ttl, when: When.NotExists);
```

`SET key NX PX cooldownMs`는 **키가 없으면 점유하고 TTL을 걸고, 있으면 실패**한다. 검사와 점유 사이에 틈이 없으므로 동시 요청 경쟁이 없다.

효과는 정책적으로도 깔끔하다.

```
파밍률 상한 = 맵의 슬롯 수 ÷ 슬롯당 쿨다운
```

무한 스폰을 해도 **쿨다운 안의 재청구는 보상이 0**이다. 치팅 방어가 별도 규칙이 아니라 **맵 설계 수치에서 자동으로 따라 나온다.**

클라도 같은 쿨다운 값을 써서 그 슬롯에 몬스터를 다시 스폰한다 — 재등장 시점과 재보상 시점이 일치하므로 플레이어 체감이 자연스럽다.

**하지 않은 것(YAGNI)** — B-full(Main을 서버 권위 풀 시뮬 = 솔로 소켓 세션)은 Co-op 오픈월드가 필요해질 때. 지금 서버는 **맵 데이터와 쿨다운만** 갖고 실시간 AI는 돌리지 않는다.

## 5. 두 경로는 다르지만 불변식은 같다

| | 던전 (Co-op) | Main (싱글) |
|---|---|---|
| 몬스터 시뮬 | SocketServer | **클라** (`LocalMonster`) |
| 드랍 roll | SocketServer | **GameServer** (ClaimKill 시) |
| 줍기 중재 | SocketServer (경쟁) | 불필요 (혼자) |
| 지급 | GameServer ← Redis Stream | GameServer ← gRPC ClaimKill |

**공통 불변식 — 영속되는 보상의 내용과 정원을 클라가 정하지 못한다.** 권위를 가진 주체만 다르고(던전=SocketServer, Main=GameServer), 규칙은 하나다.

## 6. 데이터는 SO에서 저작하고 서버는 bake된 것을 읽는다

드랍 테이블은 처음에 SocketServer 하드코딩이었다. 그런데 기획 데이터는 **디자이너가 ScriptableObject로 편집**하고 싶고, 서버는 `.asset`을 읽을 수 없다(`Shared.*`는 Unity 밖 DLL).

```
[기획] SO 저작  ──Export bake──▶  *.json (서버 임베디드)  ──▶  서버가 읽어 검증
```

세 종류가 같은 교리를 따른다.

| 데이터 | 저작 SO | bake 산출물 | 서버 로드 |
|---|---|---|---|
| 드랍 | `DropTableDefinition` | `drop-tables.json` | `DropTableCatalog` |
| 스폰/슬롯 | `MapDefinition` + 맵 에디터 마커 | `spawn-layouts.json` | `SpawnLayoutTable` |
| 소모품 회복 | `ConsumableCatalog` | `consumable-effects.json` | `ConsumableEffectCatalog` |

roll **로직**은 결정성을 위해 공유 DLL(`Shared.Gameplay.DropTableRoll`, 순수 함수)에 둔다 — 던전과 Main이 같은 함수를 쓴다. 다만 B-lite 이후 그 함수를 **호출하는 주체가 서버**로 바뀌었다.

### 교리가 완성된 지점

`slotId`/`respawnCooldownMs`를 처음엔 **JSON과 런타임 파서에만** 넣었다. 지적을 받고 `MapDefinition` SO와 맵 에디터 마커까지 왕복(round-trip)하도록 보완했다.

> **이유** — 저작 SO에 없는 값은 **기획자가 볼 수 없고, 다시 Export하면 날아간다.** "데이터가 서버에 도달한다"까지가 아니라 **"데이터가 저작 지점에 존재한다"**까지가 교리의 완성이다. (이 드리프트는 나중에 다른 데이터에서 실제로 터진다 → [27](./chapter-27-silent-failure.md) 6절)

## 7. 컴포넌트는 나누고, 로직은 공유한다

던전의 `MonsterEntity`·`GroundItemEntity`는 **"서버 명령을 받아 표시·중계"** 전용이다(보간, `C_PickupItem`). Main은 클라가 **시뮬·렌더**한다. 근본이 달라서 억지로 합치면 분기만 늘어난다.

```
LocalMonster        HP · 간단 AI(추격+근접 공격) · TakeDamage→OnDied + slotId/mapId
LocalCombat         공격 이벤트 구독 → OverlapSphere 광역 수집 → HitboxMath 정밀 판정
LocalGroundItem     IInteractable, E → ClaimKillAsync(mapId, slotId) → granted 반영
MainMonsterSpawner  Main 일 때만 슬롯 기반 스폰, 사망 후 쿨다운 뒤 재스폰
LocalRespawnController  Main 전용 타이머 부활 (던전은 등록하지 않음 = 다운 잠금 유지)
```

> **재사용한 것은 로직이지 컴포넌트가 아니다** — 적중 판정 `HitboxMath`, 드랍 `DropTableRoll`은 던전 서버와 **같은 `Shared.Gameplay` DLL**이다. 표현 계층은 각자, 결정론 코어는 공유.

대상 수집도 별도 레지스트리를 만들지 않고 `Physics.OverlapSphere` + `GetComponentInParent`로 끝냈다. 몬스터에는 이미 콜라이더가 있다 — **엔진이 이미 유지하는 인덱스를 다시 만들 이유가 없다.**

## 8. 부딪힌 것들

**슬롯이 처치 후 영구히 비었다** — 서버 쿨다운(재청구 차단)은 넣었는데 클라가 시작 시 1회만 스폰해서, 다 잡으면 필드가 비었다. 서버와 **같은 쿨다운 값**으로 재스폰을 넣어 클라·서버 시점을 맞췄다.

**`Game.System`이 `System`을 가렸다** — `Game.Gameplay.Character` 안에서 `System.Random`의 `System`이 `Game.System`으로 해석돼 컴파일이 깨졌다. `global::System.Random`으로 해결. 테스트 규칙이 경고하던 함정이 **런타임 코드에서도** 나타났다.

**타입명 충돌** — 파싱 결과 타입을 `MonsterSpawn`으로 지었는데 저작 SO의 `MapDefinition.MonsterSpawn`과 부딪혔다(`CS0101`). 런타임 타입을 `MonsterSlot`으로 개명 — **저작 타입과 런타임 타입은 다른 레이어**라 이름도 달라야 한다.

**로컬은 그린인데 Docker만 실패** (`NETSDK1004`) — `Shared.Infrastructure → Shared.Gameplay` 참조를 추가했더니 Dockerfile의 **선택 restore 목록**에 그 csproj가 없어서 컨테이너 빌드만 깨졌다.
> **교훈** — 공유 프로젝트의 참조 그래프를 바꾸면 Dockerfile도 같이 봐야 한다. **로컬 그린 ≠ Docker 그린.**

## 9. 그 이후

이 챕터의 슬롯 검증 구조 위에 **경험치 청구가 추가**됐다.

```
ClaimMonsterExp(mapId, slotId)     ← 킬 즉시 (줍기 여부와 무관)
   ① 같은 슬롯 검증 재사용
   ② 아이템과 **독립된 쿨다운 키** — 줍지 않아도 exp 는 받고, 각자의 상한을 가진다
   ③ exp 값도 서버 권위 (MonsterCatalog.Get(...).ExpReward, 클라 신뢰 0)
   ④ 쿨다운 중이면 exp 0 — 에러가 아니라 정상 응답
```

쿨다운 저장소도 `IClaimCooldownStore`로 추상화돼 테스트에서 `FakeClaimCooldownStore`로 교체된다 — [인터페이스 도입 기준](../wiki/unity-layer-separation.md)("테스트에서 실제로 교체한다")을 충족하는 사례다.

---

### 이 챕터가 이후에 미친 영향

| 여기서 정한 것 | 나중에 지탱한 것 |
|---|---|
| 클라는 사실만 보고, 보상은 서버가 결정 | 퀘스트 진행도 같은 형태(클라 보고 없음, 킬 funnel 훅)([19](./chapter-19-quest-system.md)) |
| 원자 쿨다운으로 파밍률 상한 | 발동 게이트(쿨다운·마나)의 원자 처리([23](./chapter-23-mana-resource-authority-ability.md)) |
| SO 저작 → bake → 서버 | 어빌리티·몬스터·레벨·스폰 전부 이 파이프라인으로([20](./chapter-20-content-pipeline-addressables.md)·[26](./chapter-26-measured-combat-cleanup.md)) |
| 저작 지점에 데이터가 있어야 완성 | 저작↔bake 드리프트 감시([27](./chapter-27-silent-failure.md)) |

> 이 챕터의 원본 학습 로그 = [learning-log/chapter-16-main-loot-path.md](../learning-log/chapter-16-main-loot-path.md)

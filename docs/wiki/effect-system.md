# Effect / 버프·디버프 시스템 기획서

> 대상: GAS의 GameplayEffect(버프·디버프)와 그 HUD 표시·서버 동기화.
> **plan.md M2(CA-1~3)에서 이미 결정된 큰 골격 위에** 디테일을 채우는 문서다.
> 작성: 2026-06-03

---

## 0. plan.md에서 이미 결정된 전제 (재논의 금지)

| 항목 | 결정 | 출처 |
|------|------|------|
| 코드 공유 | **`Shared.Gameplay`(netstandard2.1) DLL** 을 서버·Unity 둘 다 참조 | CA-2 |
| 권위 모델 | **서버 권위**(active window 판정 + GE 데미지) + **클라 예측** | CA-3 |
| 데이터 진실원 | **공유 JSON** (SkillTimeline·GE 정의). ScriptableObject를 진실원으로 쓰지 않음 | "안 함" |
| 결정론 범위 | fixed-point/롤백(lockstep) **안 함**. "동일 로직 + 권위 시작시각" 수준 | "안 함" |
| 아이콘 표시 | **카테고리 아이콘 + polarity(버프/디버프) 색** | 이번 결정 |

→ 결정론의 의미: **양쪽이 같은 DLL의 같은 순수 함수**로 modifier 수학·duration을 계산하므로 결과가 구조적으로 일치한다. 매 프레임 상태를 주고받는 lockstep이 아니다.

---

## 1. 레이어 구조

```
            ┌──────────────────────────────────────────────┐
 진실원      │  공유 JSON (GE/SkillTimeline 정의)             │
            └──────────────────────────────────────────────┘
                         │ 로드(EffectId 키)
            ┌──────────────────────────────────────────────┐
 Shared.Gameplay (netstandard2.1, 엔진 비의존, 순수)          │
            │  - GameplayEffectDefinition (id, 카테고리,      │
            │    DurationPolicy, durationMs, modifiers, 스택) │
            │  - 순수 함수: Aggregate(base, modifiers),        │
            │    Remaining(startTick, durMs, now)            │
            └───────────────┬───────────────┬───────────────┘
            참조             │               │ 참조
        ┌───────────────────┘               └────────────────────┐
   [GameServer/SocketServer]                              [Unity Client]
   서버 권위: Apply/Remove 판정                       ASC: ActiveEffect 보유·tick
   GE 데미지, active window                          클라 예측 + 서버 정정
        └────────── Apply/RemoveEffect 패킷 ──────────┘
                                                          │ (Sprite 모름)
                                              ┌───────────▼───────────┐
                                              │ Client Presentation    │
                                              │  EffectIconCatalog(SO)  │ ← 표시 전용
                                              │  category→Sprite, 색규칙 │
                                              │  → BuffView DTO         │
                                              │  → BattleEffectSlot     │
                                              └────────────────────────┘
```

**핵심 분리**: 게임플레이 데이터(Shared, Sprite 없음)와 표시 데이터(클라 전용, Sprite 있음)를 절대 섞지 않는다. **서버는 Sprite/색을 모른다. 동기화로 오가는 건 `EffectId` 뿐.**

---

## 2. Effect 데이터 모델 — 정의 vs 인스턴스

### 정의 (정적·불변, 공유 JSON → `Shared.Gameplay`)

```csharp
enum EDurationPolicy { Instant, Duration, Infinite }
enum EStackPolicy    { None, Refresh, Stack }   // 없음 / 지속갱신 / 누적

// Sprite 없음. 순수 데이터. EffectId로 카탈로그 조회.
class GameplayEffectDefinition
{
    EffectId           Id;          // 안정적 키 (동기화·표시 매칭 공통)
    EEffectCategory    Category;    // 아이콘 매칭 키 (AttackPower, MoveSpeed, …)
    EDurationPolicy    Policy;
    int                DurationMs;  // Duration일 때만
    EStackPolicy       Stack;
    int                MaxStacks;
    List<GameplayAttributeModifier> Modifiers;
}
```

### 인스턴스 (런타임·동기화 대상)

```csharp
class ActiveGameplayEffect
{
    int       InstanceId;   // 고유 — RemoveEffect 타겟
    EffectId  DefId;
    long      StartTick;    // 권위 시작시각 (공유 시계 기준)
    int       Stacks;
    // Remaining은 저장하지 않는다 → 항상 (StartTick + DurationMs) - now 로 계산
}
```

---

## 3. Attribute 재구조 — 가역 버프의 핵심

**현재 `GameplayAttribute.ApplyModifier`는 `CurrentValue`를 되돌릴 수 없게 직접 변경한다 → 버프 만료 복원 불가.** 반드시 고친다.

값을 두 종류로 나눈다:

| 종류 | 예 | Current 계산 | 즉발(Instant) | 지속(Duration) |
|------|----|----|----|----|
| **Resource** | HP, MP | 직접 보유 (피해/회복으로 변동) | Current 영구 변경 | (Max를 버프하면 cap 변경) |
| **Stat** | 공격력, 방어, 이속 | **파생** = Base + 버프 | (거의 없음) | modifier 등록 → 재계산 |

```
Stat.Current = Clamp( (Base + Σ Additive) × (Π Pct / 10000) , 0, Max )
```

- **즉발 효과** → Base(또는 Resource current) 영구 변경. (데미지/힐)
- **지속 효과** → ActiveEffect의 modifier로 **등록만**. Current 재계산. **만료 시 목록에서 제거 후 재계산 → 자동 복원.**
- **재계산 트리거**: ActiveEffect **추가/제거/만료 시에만** (매 프레임 X). 순수 함수.
- **정수(또는 고정소수) 기반.** float는 플랫폼·연산순서로 클라·서버가 미세하게 갈린다. 곱연산은 `× pct / 10000` 정수.

> Resource(HP/MP)와 Stat을 한 `GameplayAttribute`에 욱여넣지 말 것. 별 종류로 다룬다. (지난 단계의 HP/MP 릴레이는 Resource 표시였음 — 그대로 유효)

---

## 4. Stacking 정책

| 정책 | 동작 | 아이콘 |
|------|------|--------|
| None | 동일 Effect 재적용 무시(또는 갱신 안 함) | 1개 |
| Refresh | 재적용 시 StartTick 갱신(지속시간 리셋) | 1개, 시간만 리셋 |
| Stack | MaxStacks까지 누적, modifier 합산 | 스택 수 뱃지 |

재적용 판정 키 = `(DefId, SourceId)` 또는 `DefId`만. (정의별로 선택)

---

## 5. 서버 동기화 — "둘 다 지속시간을 안다"

### 원리
남은시간을 계속 주고받지 **않는다**(채팅 폭증·drift). 대신:

```
서버 → 클라 :  S_ApplyEffect (effectId, instanceId, startTick, stacks, sourceId, targetId)
               S_RemoveEffect (instanceId)            ← 만료/해제 권위
양쪽       :  durationMs = 카탈로그[effectId]          (같은 DLL → 동일)
               remaining = (startTick + durationMs) - now   (공유 시계)
               modifier 수학 = 같은 DLL 순수 함수       (동일)
```

- **공유 시계 `now`**: 서버 tick에 클라를 동기(기존 ping/timestamp 재사용 — 이동 패킷이 timestamp 릴레이하는 것과 같은 결).
- **만료**: 클라가 로컬 예측으로 즉시 UI 제거하되, **서버 `S_RemoveEffect`가 최종**. 예측·정정 패턴(CA-3의 "클라 예측"과 동일).
- **동일 로직 보장 메커니즘 = `Shared.Gameplay` DLL을 양쪽이 참조**. 복제가 아니라 같은 바이너리 → drift 0.

### 패킷 (가이드 — 추가 시 networking.md 3단계 준수)

```csharp
[MemoryPackable] partial class S_ApplyEffect  : Packet { int InstanceId; int EffectId; long StartTick; int Stacks; long SourceId; long TargetId; }
[MemoryPackable] partial class S_RemoveEffect : Packet { int InstanceId; }
```
- Union ID: 전투(1600~1699) 내 **Effect 전용 소블록(예: 1640~1659)** 후보 — *확정 필요*.
- 클라→서버 능동 Effect 요청은 보통 없음(스킬 사용=Ability 활성화 경로). Effect 부여는 서버 판정 결과로만 내려온다.

---

## 6. 표시 / 아이콘 (확정: 카테고리 + polarity 색)

**클라 Presentation 전용. 서버·Shared는 관여하지 않는다.**

```csharp
// 클라 표시 전용 ScriptableObject (진실원 아님, 매핑 테이블)
class EffectIconCatalog : ScriptableObject
{
    // Category → Sprite (예: AttackPower → 검 아이콘 1장)
    Map<EEffectCategory, Sprite> icons;
    Color buffColor;     // 예: 초록
    Color debuffColor;   // 예: 빨강
}
```

- **아이콘** = `icons[def.Category]` — 공격력 버프/디버프가 **같은 Sprite 공유**.
- **색** = polarity로 자동: net modifier 부호가 + 면 `buffColor`, − 면 `debuffColor`.
  - polarity 판정은 정의의 modifier 합 부호(또는 정의에 `IsBuff` 명시). 카테고리 방향이 애매하면 정의에 명시 필드를 둔다.

### MVI 흐름 (지난 HP/MP와 동일 패턴)

```
ASC.ActiveEffects 변경
  → OnActiveEffectsChanged 이벤트
    → InGameModel: ActiveEffect[] → BuffView[] 변환 (카탈로그로 Sprite·색 해석)
      → InGameState.Buffs (DTO 배열, Sprite/Color/endTick/stacks)
        → GameHud: BattleEffectSlot 풀에 바인딩
```

```csharp
// Presentation DTO (Shared 타입 노출 안 함)
struct BuffView { Sprite Icon; Color Tint; long EndTick; int Stacks; bool IsInfinite; }
```

- **남은시간 카운트다운은 Slot이 로컬에서** 매 프레임 `EndTick - now`로 갱신.
  State는 **집합(추가/제거)이 바뀔 때만** 발행 → 매 프레임 State 재발행 안 함(성능·결정론 양립).
- `BattleEffectSlot.Bind(BuffView)`: icon.sprite=Icon, icon.color=Tint, 카운트다운 텍스트는 자체 갱신. (현재 빈 껍데기에 이 메서드 채움)
- `buffSlotContainer`(LayoutGroup)에 슬롯 풀링. 개수 변동에 맞춰 활성/비활성.

---

## 7. 단계적 작업 계획

M2 전체를 한 번에 하지 않는다. **표시 레이어는 서버 권위 이전에 클라 단독으로 sync-ready하게** 먼저 만들 수 있다.

### 1단계 — 클라 Effect 모델 + HUD 버프 표시 (서버 없이 완결, sync-ready)
1. Attribute 재구조: Resource(HP/MP) vs Stat 분리, Base/Current, `Recalculate` (순수, 정수)
2. `GameplayEffectDefinition`/`ActiveGameplayEffect`/`EffectId`/`EEffectCategory`/`EDurationPolicy`/`EStackPolicy` — **순수 데이터, Sprite 없음** (나중에 `Shared.Gameplay`로 이전 가능한 위치/형태로)
3. ASC: ActiveEffects 보유 + `Tick`(만료 제거) + 추가/제거/만료 시 재계산 → `OnAttributeChanged`(기존)·`OnActiveEffectsChanged`(신규)
4. `EffectIconCatalog`(Presentation SO) + `BuffView` DTO + `InGameState.Buffs`
5. `InGameModel`: ActiveEffects → `BuffView[]` 중계
6. `GameHud`: `BattleEffectSlot` 풀 렌더 + Slot 로컬 카운트다운
7. EditMode(가역성·재계산·만료) + PlayMode(버프슬롯 동적 렌더) 테스트

### 2단계 — M2 합류 (서버 권위)
- 1단계의 순수 모델을 `Shared.Gameplay` DLL로 이전, 서버도 참조
- `S_ApplyEffect`/`S_RemoveEffect` 패킷 + 공유 시계
- 서버 권위 판정 + 클라 예측/정정 (CA-3)

> 1단계 산출물을 2단계가 버리지 않는다 — EffectId·정수수학·event 구동·표시분리를 처음부터 지키면 그대로 얹힌다.

---

## 8. 미결 디테일 (1단계 착수 전 확정 필요)

1. **`EEffectCategory` 초기 목록** — AttackPower / Defense / MoveSpeed / (HP·MP는 Resource) … 어디까지?
2. **polarity 판정** — modifier 부호 자동 vs 정의에 `IsBuff` 명시 (카테고리 방향 애매한 경우 대비)
3. **`EffectId` 표현** — enum(컴파일타임) vs string/int 카탈로그 키(JSON 친화). plan은 JSON 진실원 → string/int 권장
4. **Effect 패킷 Union ID 소블록** — 1640~1659 후보 확정
5. **공유 시계 소스(2단계)** — 서버 tick vs timestamp; 1단계는 클라 `Time` 로컬로 충분

---

## 관련 문서
- [plan.md](plan.md) M2/CA-1~3 — 본 기획서의 상위 로드맵
- [.claude/rules/unity-gameplay-state.md] — AttackState/Hit 책임 경계
- [.claude/rules/unity-client.md] — MVI 레이어·표시 타입 노출 규칙
- [.claude/rules/networking.md] — 패킷 추가 3단계·Union ID 범위

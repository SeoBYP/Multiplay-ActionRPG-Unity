# 애니메이션 · 전투연출 폴리시 백로그

> 상태: **살아있는 백로그** (착수 시 해당 항목에 커밋/codemap 링크 추가, 완료 시 ✅)
> 맥락: MotionMatching 외부화 후 클라는 **단순 PROTOFACTOR Animator**로 복원됨. 2026-07 세션에서 플레이어·원격 플레이어 애니 기반 + 무기 판정 + HitboxMath 방향 버그 + 이동잠금 + 부활 애니(§2.52~2.56) → **NPC 애니 · 락온 8방향 strafe · 파티 HP HUD(+원격 HP 기준선 동기화)**(§2.57·§2.58·§2.58b, 2026-07-12)까지 세움. 이 문서는 **그 위에 남은 폴리시 항목**을 실행 가능하게 목록화한다.
> 진행: #1 NPC ✅ · #3 부활 기상 ✅ · #4 Interact(토스트) ✅ · #5 원격 회피(S_Dodge) ✅ · #6 락온 strafe/마커 ✅ · ★ 파티 HP HUD ✅ (2026-07-12). **남은 것 = #2 몬스터 모델·애니**(메시 결정 필요)·**#7 Attack 콤보 A→B→C**.
> 관련: [codemap.md](codemap.md) §2.52~2.58b · [plan.md](plan.md) · [.claude/rules/unity-gameplay-state.md](../../.claude/rules/unity-gameplay-state.md)

## 재사용할 확립된 패턴 (착수 전 참고)

| 패턴 | 위치 | 용도 |
|------|------|------|
| **애니 계약** = enum 트리거 + 직렬화 파라미터명 | `CharacterAgentAnimations.cs` | 새 트리거/파라미터 추가 지점 |
| **네트워크 재생 구동** = 보간 변위→Speed·이벤트→트리거 | `RemoteDriver.cs` | 몬스터·NPC·원격 애니 |
| **무기 판정** = 콜라이더 + 애니이벤트 active-window | `WeaponHitbox.cs`·`WeaponAnimationEventRelay.cs` | 콤보 타별 판정 |
| **연출 브로드캐스트** = 서버 게이트 통과분만 방 송신 → 클라 핸들러 → RemoteDriver 트리거 | `S_Attack`·`CombatHandler`·`AttackPacketHandler` | 원격 회피(S_Dodge) |
| **패킷 3단계** = 클래스 + Union 등록 + 핸들러 | `.claude/rules/networking.md` | 신규 패킷 |
| **월드공간 UI** = Canvas + 빌보드 | `MonsterHealthBar.cs` | 락온 마커 등 |

## 우선순위 요약

| # | 항목 | 규모 | 권위/계약 영향 | 권장 순서 |
|---|------|------|----------------|-----------|
| 1 | ✅ NPC 캐릭터 모델·애니 (휴머노이드) — 2026-07-12 완료(codemap §2.57) | 중 | 없음(클라) | — |
| 2 | 몬스터 캐릭터 모델·애니 | 중 | 없음(클라) | 1 (메시 결정 필요) |
| 3 | ✅ 부활 기상(GetUp) 클립 — 2026-07-12 완료(codemap §2.59). `GetBackUpFront` + Dead→GetUp→Loco 재배선(로컬·원격 공유 컨트롤러) | 소 | 없음 | — |
| 4 | ✅ Interact 클립 — 2026-07-12 결정(§2.59): 전용 줍기 클립 없음 → **애니 제거 + 획득 토스트(GameHud)로 대체**(사용자 결정) | 소 | 없음 | — |
| 5 | ✅ 원격 회피 애니 (S_Dodge) — 2026-07-12 완료(codemap §2.60). Union 1603 S_Dodge 브로드캐스트 → RemoteDriver 구르기 애니. Main=솔로라 던전 전용 | 중 | **공개계약**(Union 1603) | — |
| 6 | ✅ 락온 strafe(8방향) 블렌드 + UI 마커 — 2026-07-12 완료(codemap §2.57) | 중 | 없음(클라) | — |
| 7 | Attack 콤보 A→B→C | 대 | 서버 게이트 재검토 | 4 |
| 8 | Interact 이동잠금 지속 = IInteractable 노출 | 소 | 없음 | 필요 시 |
| 9 | FBX `.meta` 클론 안전성 | 결정 | 레포 관행 | 상시 유의 |
| ★ | ✅ **던전 파티 HP HUD** — 2026-07-12 완료(codemap §2.58). 원격 ASC 레지스트리 + EffectReceiver TargetId 라우팅(신규 패킷 X, GAS 재사용) + 좌상단 파티 패널 | 중 | 없음(클라) | — |

---

## 1. NPC 캐릭터 모델·애니 (휴머노이드) — 중

- **현재**: NPC = 캡슐. `NPCController.controller`·`NPCCharacterAgent` 존재하나 모델·Animator 미배선. 대화 NPC = `NPCDialogueInteractable`·`NPCInteractable`.
- **왜**: 플레이어/원격만 실캐릭터라 비주얼 불균형. 휴머노이드라 `SK_Protof-Actor` 그대로 재사용 가능(가장 빠름).
- **접근**: 플레이어 배선과 동일 — NPC 프리팹 자식에 `SK_Protof-Actor`+Animator(`NPCController`). NPC가 정지형이면 Idle만; 이동형이면 RemoteDriver 패턴으로 Speed 구동. `NPCController`에 Idle(+대화 제스처) 상태 배선.
- **손댈 파일**: NPC 프리팹, `NPCController.controller`, (이동형이면) NPC 구동기.
- **검증**: 플레이모드 NPC 배치 스크린샷 + 대화 E2E 무회귀.

## 2. 몬스터 캐릭터 모델·애니 — 중 (메시 결정 필요)

- **현재**: `Monster.prefab`(던전, `MonsterEntity`)·`LocalMonster.prefab`(Main, `LocalMonster`) = 캡슐 `Model`. 체력바만 부착됨(§2.55).
- **왜**: 적이 캡슐. 위치는 이미 서버/AI 보간으로 구동됨.
- **⚠ 결정 필요**: 현재 몬스터 = "slime". `SK_Protof-Actor`(인간형)는 부적합 → **PROTOFACTOR Creature/Zombie 애님셋 메시** 사용하거나, 슬라임 전용 메시 도입. 메시가 정해져야 착수.
- **접근**: `MonsterEntity`(던전)·`LocalMonster`(Main)에 RemoteDriver식 애니 구동 추가 — 보간 변위→Speed, `S_MonsterDead`/사망→Dead 트리거. 몬스터용 컨트롤러 신설(Idle/Move/Death). CharacterAgentAnimations 재사용 가능 여부 검토(몬스터는 다른 계약일 수 있음).
- **손댈 파일**: `Monster.prefab`·`LocalMonster.prefab`, `MonsterEntity.cs`·`LocalMonster.cs`, 신규 `MonsterController.controller`, 몬스터 메시.
- **검증**: 몬스터 스폰/이동/사망 애니 스크린샷 + 몬스터 E2E(SocketE2E) 무회귀.

## 3. 부활 기상(GetUp) 클립 — 소

- **현재**: 부활 시 `Dead→Idle Walk Run Blend` 0.25s 블렌드 = **툭 일어남**(§2.56).
- **접근**: `Dead→GetUp→로코모션` 상태 삽입. PROTOFACTOR `Humanoid@GetBackUpFront`(또는 1hMelee 계열) 클립. 컨트롤러에 `GetUp` 상태 + `Revive` 트리거가 `Dead→GetUp`, `GetUp`은 HasExitTime 으로 로코모션 복귀.
- **손댈 파일**: `PlayerController.controller`(+RemotePlayer 동일 컨트롤러 공유라 자동 반영).
- **검증**: 런타임 Animator 구동(§2.56 테스트 확장) Dead→GetUp→로코모션.

## 4. Interact 전용 클립 — 소

- **현재**: Interact 상태 = `DrawWeapon1hMelee` 플레이스홀더(§2.52).
- **접근**: 줍기/상호작용에 맞는 짧은 제스처로 교체. PROTOFACTOR에 전용 pickup 없으면 근접 대용 클립. 컨트롤러 Interact 상태 모션만 교체.
- **손댈 파일**: `PlayerController.controller`.

## 5. 원격 회피 애니 (S_Dodge) — 중 · **공개계약 변경**

- **현재**: 로컬 회피(`DodgeDriver`)만 애니. 원격은 `S_Dodge` 패킷이 없어 못 봄(§2.54에서 범위 밖으로 표시).
- **접근**: `S_Attack` 브로드캐스트 패턴 복제. **패킷 3단계**(`.claude/rules/networking.md`):
  1. `S_Dodge{ UserId }` 클래스 + Union 등록(전투 대역 1603 등) — Shared.Packet + 클라 미러.
  2. 서버 `DodgeHandler`가 게이트(쿨다운·마나) 통과 후 방 브로드캐스트.
  3. 클라 `DodgePacketHandler`→`OnPlayerDodged`(SocketApiClient 이벤트)→`RemoteDriver.SetTrigger(Dodge)`.
- **주의**: **공개계약(Union ID·패킷)** 변경 → 착수 전 명시 승인. gRPC 아님(TCP)이라 Generated 재생성 불요.
- **손댈 파일**: `Shared.Packet/.../DodgePacket`, `Packet.cs`(Union), 서버 `DodgeHandler`, 클라 `DodgePacket.cs`+핸들러+`SocketApiClient`+`RemoteDriver`.
- **검증**: SocketE2E(원격 회피 수신) + EditMode 디스패치 테스트(`SocketApiClientTest` 패턴).

## 6. 락온 strafe 블렌드 + UI 마커 — 중 (2.6.3 잔여)

- **현재**: 락온 시 몸이 타겟 향하고 이동은 스트레이프인데(§2.51), **애니는 전진 블렌드만** → 옆/뒤로 가도 앞으로 뛰는 애니.
- **접근(strafe)**: 2D 블렌드트리(`MoveX`/`MoveY`) + 방향 이동 클립(1hMelee `RunForward/Backwards/Left/Right` 존재). `CharacterAgentAnimations`에 `MoveX`/`MoveY` float 추가 → `GroundState`(락온 중)가 카메라/타겟 상대 이동방향을 공급. 비락온 시 기존 1D Speed 유지.
- **접근(UI)**: 락온 타겟 위 마커 = `MonsterHealthBar` 월드공간 빌보드 패턴 재사용, `LockOnDriver`가 대상 push.
- **손댈 파일**: `PlayerController.controller`(2D 블렌드), `CharacterAgentAnimations.cs`, `GroundState.cs`, `LockOnDriver.cs`, UI.

## 7. Attack 콤보 A→B→C — 대 · 서버 게이트 재검토

- **현재**: 단발(basic/heavy 각 1타, §2.2). 콤보 카운터/입력버퍼/active-window 없음(§2.52 주석).
- **접근**:
  - **입력/상태**: `PlayerCharacterAgent.FireSkill/HandleAttackInput`에 콤보 index + 다음타 입력버퍼 + active-window(다음 콤보 허용 구간). 창 놓치면 리셋.
  - **애니**: `CharacterAgentAnimations`에 콤보 index int 또는 Attack1/2/3 트리거 + 컨트롤러 상태 체인(`AttackA→B→C`, 각 클립 active-window 애니이벤트로 `WeaponHitbox` 여닫기). 1hMelee `AttackA/B/C`·`2HitCombo`·`3HitCombo` 클립 존재.
  - **서버 권위**: 던전은 `C_Attack{skillId}` 유지 — 콤보 각 타를 별도 skillId로 보낼지, 콤보는 클라 연출이고 데미지는 타별 C_Attack인지 결정. 서버 쿨다운/게이트가 연타를 막지 않도록 재검토.
- **손댈 파일**: `PlayerCharacterAgent.cs`, `CharacterAgentAnimations.cs`, `PlayerController.controller`, `WeaponHitbox`(타별), `skills.json`(콤보 타별 스킬 시 bake).
- **규모**: 대(코드+애니+서버 검토). 다른 폴리시보다 뒤.

## 8. Interact 이동잠금 지속 = IInteractable 노출 — 소 (선택)

- **현재**: `InteractRootSeconds` 고정 0.6s(§2.53). 대상별(줍기 짧게 / 대화창 닫힐 때까지) 표현 불가.
- **접근**: `IInteractable`에 `RootSeconds` 노출 → `HandleInteractInput`이 대상 값으로 `ApplyRoot`. 대화는 대화 시스템이 별도 입력/이동 차단(관심사 분리)도 대안.

## 9. FBX `.meta` 클론 안전성 — 상시 유의 (결정 필요)

- **현재**: 컨트롤러의 클립 GUID·LoopTime·Animation Event가 **미추적 FBX `.meta`**에 있음(레포 관행: 아트 메타 미추적). 로컬만 정상, **클론 시 클립 참조·loop·이벤트 유실**(구 컨트롤러 `a1af…` 깨짐과 동류).
- **결정**: 참조된 PROTOFACTOR FBX `.meta`(SK_Protof-Actor + 사용 클립 + 프롭 + AttackA 이벤트 메타 ~15개)를 `git add -f`로 추적할지, 로컬 관행 유지할지. 애니 폴리시가 늘수록 참조 메타도 늘어 결정을 미룰수록 커짐.

---

## 권장 착수 순서

```
1) NPC 애니(휴머노이드, 빠름) ─▶ 2) 몬스터 애니(메시 결정 후)
        │  "모든 캐릭터 캡슐 탈출" 달성
        ▼
3) 부활 GetUp · Interact 클립 (소, 마무리감)
        ▼
4) 원격 회피 S_Dodge (공개계약 — 승인 후)
        ▼
5) 락온 strafe/UI ─▶ 6) Attack 콤보 (가장 큼, 마지막)
```
그 다음은 M5 신규 콘텐츠(4.6 PVE 오픈월드)로 전환.

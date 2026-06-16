# 작업 플랜 (현재 진행 상황)

> **새 채팅 시작 시 이 파일을 먼저 읽어라.**
> Phase가 완료될 때마다 즉시 갱신한다.
> 마지막 갱신: 2026-06-11 (**GAS 구조 정리 + 플레이어 HP 서버 권위 승격(2.5.1 사망)** — 커밋 `ebbb8579`. ① **GAS 정리(ⓐ~ⓓ)**: Shared.Gameplay 폴더 개념별 재구성 / `GameplayTag`·Container / effect 수치 단일소스화(서버 위임) / **서버 발동 게이트(쿨다운)로 C_Attack 연사 치팅 차단**. ② **2.5.1 사망**: `State.Dead` 태그 입력게이트(던전 다운-잠금) + `S_PlayerDead`(Union 1823) 원격 다운 가시성. ③ **플레이어 HP 서버 권위(authority-model §0 권위결정규칙 + §4 승격)**: 기존 "클라 결정론"은 사용자 미승인 가정·**불사 핵 부채**였음 → 던전 HP=서버권위로. 증분1=서버 데미지누적→HP0 직접감지→S_PlayerDead(C_PlayerDead 격하). 증분2=회복 크로스서버(ConsumeItem→GameServer 검증·차감→Redis→SocketServer ApplyPlayerEffect(+heal)+S_ApplyEffect). ④ **버그픽스**: 다운 플레이어 몬스터 타깃 제외. ⑤ **맵 선택 슬림 배선**(StartRoomRequest.map_id, 4.3 부분상환) + test fixture(test_brute/test_arena). **검증: GameServer 252/252 + SocketServer 85/85 + Shared 30/30 + 클라 EditMode/PlayMode + SocketE2E 2종(회복/사망) 그린**. 상세 = codemap §2.6b. **잔여(2.5.1): 다운 포즈/애니·Main 타이머 리스폰·회복 수치 단일소스. 던전 내 부활=2.5.2.**)
>
> 직전: 2026-06-09 (**3.3.7 루트 풀 E2E + 재접속 유예 창(6.4)** — ① 던전 루트 풀 E2E(사냥→드랍→줍기→인벤토리) 작성, `DropTable` 슬라임 potion 보장드랍(결정성)·`StartedRoomContext.HostAccessToken`. ② **9.4 부채수정이 만든 재접속 회귀**(크래시 시 `_playerStates` 즉시삭제 → 재입장 거부)를 **재접속 유예 창(grace)**으로 해소 = WBS 6.4 진척: 크래시는 60s 상태 보존+AI 유령필터, 명시퇴장만 즉시제거, 만료 스윕. **SocketServer.Tests 72/72 + PlayMode 16/16 그린**(UnityMCP). 상세 = codemap §2.17. **Main 루트 경로(3.3.8~10)·클라 재접속 팝업 잔여.**)
>
> 직전: 2026-06-07 (**M4 던전 루프 완성 = DoD 달성** 🎉 — A 트랙(클리어 루프)·B 트랙(Exp 보상+실패 경로+UI) 코드 완료에 더해, **MPPM 2-창 수동 플레이로 클리어→보상→로비 복귀 1판 전체 시각 검증 통과**(사람 확인). M4 마지막 잔여(완전한 Co-op 1판 루프) 닫힘 → **M4 전체 ✅**. **다음 = M5 폴리시/콘텐츠** 또는 서버 도메인 병렬 트랙(3.1 인벤토리 등). DoD: 2명 접속→방 생성·시작→던전 입장(서로 보임·이동)→몬스터 협력 처치→클리어→Exp 보상→로비 복귀 전 과정 서버 권위 + E2E 통과 — **충족**.)
>
> 직전: 2026-06-06 (**M4 A 트랙 완료** — 던전 클리어 루프 골격 그린. 몬스터 전멸→`Room.TryMarkCleared`(서버 권위 1회)→`S_DungeonClear`(1820) 브로드캐스트+`DungeonClearMessage`(stream:game:dungeon:result)→`DungeonResultConsumer`(수신·로그, 보상 TODO)→클라 `InGameState.IsDungeonCleared`→`GameHud` 패널+기존 `ReturnToLobby`. **SocketServer.Tests 47/47 + PlayMode SocketE2ETests 12/12**(Docker 리빌드 후). 상세 = codemap §2.9. **A 잔여(사용자 영역)**: 전투 플레이 검증·결과 패널 아트. **다음 = B 트랙(보상)**: DungeonId(EF 마이그레이션)→Inventory→Progression→`DungeonResultConsumer` TODO 자리에 보상 산정·지급(Outbox 원자화)→결과/보상 UI.)
>
> 직전: 2026-06-05 (**M4 착수 — DoD 던전 루프**: A 트랙(클리어 루프 골격)→B 트랙(보상). **전투**: 플레이어→몬스터 = **서버 권위 유지**(기존 M3 ⑤ `C_Attack`→서버 hitbox→`DamageMonster`). 클라는 **트리거(`C_Attack`)만** 송신. 클리어 = **몬스터 전멸 1회**. 보상 = 경험치+아이템 둘 다.)
>
> 직전: 2026-06-05 (**M3 몬스터 완료** 🎉 — 스폰·이동(`MonsterAiMath` Patrol/Chase/Attack + 매 틱 bounds.Clamp)·**양방향 전투**(플레이어→몬스터 피격/사망 GAS·`S_MonsterDead` / 몬스터→플레이어 공격 `S_ApplyEffect{monster_attack_dmg}`)·클라 렌더(`MonsterEntity` 보간, **2인 플레이 시각검증**)·E2E 3종 작성. 서버 권위 + 단일 `RoomTickService` + GAS. SocketServer.Tests **43/43** + 클라/E2E 빌드 0오류. **추가 보완**: ① 게임시작 불가 버그(컨테이너 재시작 시 Redis `LOADING`에 `GameStartRequestedConsumer` 영구사망) → `ResilientStreamConsumer` 복원력 중앙화·3개 컨슈머 이관(plan §9.10 / codemap §2.8). ② 몬스터 프리팹(Capsule+빨강 URP) 제작. 상세 = `§M3`.)
>
> 직전 갱신: 2026-06-04 (**M3 몬스터 플랜 확정** — 스폰·패트롤(자식 마커 씬 드래그)·맵경계를 Map Editor에서 저작 → spawn-layouts.json → 서버 파싱. 증분 ①패킷→②a데이터·②b에디터·②c파싱→③서버상태→④틱AI→⑤피격사망→⑥클라렌더→⑦E2E. 같은 날 선행: **WBS 통합**(`wbs.md` 삭제·마일스톤↔WBS ID 일원화).)

---

## 🎯 최종 목표 — 포트폴리오 게임 완성

**완성 정의(DoD)**: 2명이 (MPPM) 접속 → 로비에서 방 생성·시작 → 던전 입장(서로 보임·이동) → 몬스터 협력 처치 → 클리어 → 보상 수령 → 로비 복귀. **전 과정 서버 권위 + E2E 통과 + 데모 영상.**

**범위**: Co-op 던전 **버티컬 슬라이스** + **PVE 오픈월드 맛보기**(싱글 탐험/퀘스트 최소). 폴리시(애니메이션·스킬·아이템·사운드)는 **코어 루프 이후**.

**범위 제외(2026-06-03 결정)**: **던전 난이도/시즌 · 가챠 · 우편함**은 포트폴리오 범위에서 제외 — WBS·로드맵에 넣지 않는다.

| 마일스톤 | 목표 | WBS 노드 |
|---|---|---|
| ✅ M0 | 인증·로비·채팅·소켓·던전 입퇴장·DB/캐시·Unity OutGame (기반) | 1.* |
| ✅ M1 | 인게임 진입 — 로컬/원격 캐릭터 스폰·이동, 인게임 UI 전환 | 1.6 |
| 🔄 M2 | 전투 코어 — Character 두 축 리팩터(GAS) + 서버 권위 Attack/Hit/Damage | 2.1·2.2·2.5.1·2.6.2 |
| ✅ M3 | 몬스터 — Spawn·이동(AI)·State·Dead + 양방향 전투(P↔M) + 클라 렌더(2인 검증) | 4.1.1~4.1.5 |
| ✅ M4 | 던전 루프 완성 — Clear → 보상 → 로비 복귀 **(= DoD 달성)** | 2.3·4.2·6.2·7.1 |
| M5 | 폴리시/콘텐츠 — 애니(MotionMatching V2)·스킬·아이템·사운드 + PVE 맛보기 | 2.4·2.6·2.7·3.2~3.8·4.4~4.7·5.*·6.3~6.4·7.2~7.8·8.* |
| M6 | 마감 — 데모·부하/E2E 검증·배포/포트폴리오 문서 | 9.8·9.9 |

> Character 아키텍처([character-architecture.md](character-architecture.md))는 M1(합성/Driver)·M2(GAS)에 **통합**됨. 상세 태스크는 아래 "작업 순서".

---

## 🌳 전체 범위 — WBS 트리

> RPG 게임 완성에 필요한 **전체 작업의 계층 분해(Work Breakdown Structure)**. 마일스톤(아래 "작업 순서")이 *실행 순서* 뷰라면, 이 트리는 *구조* 뷰다. 둘은 WBS ID로 묶인다.
>
> **범례** — 상태: `✅`완료 `🔄`진행 `⬜`미착수 / **Tier**: **T1** 코어·DoD 필수 · **T2** RPG 확장 · **T3** 향후/선택 / **Owner**: 🟢 서버 도메인 세션 · 🔵 GAS/전투 세션 · 🟣 애니(MotionMatching V2) 세션 · ⚪ 미배정. (말단에만 `상태 | Tier | Owner` 태그)
>
> **YAGNI**: 카탈로그는 *가시화* 목적. 실제 착수는 Tier 순(T1→T2→T3)으로 마일스톤에 편입될 때만.
>
> **🔗 GitHub Project 자동 동기화**: 노드 줄의 상태 마커(✅/🔄/⬜)를 바꿔 plan.md를 **커밋**하면 post-commit 훅이 [Project #2](https://github.com/users/SeoBYP/projects/2)에 자동 반영한다 — Status 필드 + 이슈 open/close, **새 `x.y` 노드는 이슈 자동 생성**(`9.2`처럼 중복은 스크립트 EXCLUDE). 한 줄=한 노드·`**x.y**`+상태마커 형식을 지켜야 파싱된다. 수동/미리보기: `python .claude/scripts/sync-github-project.py [--dry-run]`.

### 1. 기반 시스템 — ✅ 완료 (M0/M1) → 상세는 `§완료된 Phase`
- **1.1** 계정/인증 · **1.2** 유저/프로필 · **1.3** 로비/매칭 · **1.4** 채팅 · **1.5** 게임 세션 연결 — ✅
- **1.6** 인게임 진입(입장·이동·결정론 스폰·전원입장 게이트·HUD·로비 복귀) — ✅
- **1.7** 인프라(PostgreSQL·Redis·Docker·Serilog+Graylog·Testcontainers·E2E) — ✅
- **1.8** GAS 기반(Attribute·GameplayEffect·ASC·버프/디버프 서버 동기화 EF-1·EF-2) — ✅

### 2. 캐릭터 시스템
- **2.1** 전투 코어 — 서버 권위 Attack/Hit/Damage (GAS) — 🔄 | T1 | 🔵
- **2.2** 스킬/어빌리티 — SkillTimeline, `basic_swing` 외 확장 — 🔄 | T1 | 🔵
- **2.3** 진행/성장(Progression) — 레벨·경험치·스탯 성장 영속 (`Progression` 신규) — ✅ | T1 | 🟢 (Exp 영속 + **레벨업 산식·레벨별 스탯 테이블 룩업·클라 노출(GetProgression) 완료 2026-06-14**. 스탯=레벨 룩업 파생(DB는 Level/Exp만). 상세 codemap §2.22. 스탯창 prefab(7.3)·`LevelTable.asset` 실저작은 사용자 Unity)
- **2.4** 스탯 산식 — 레벨/장비/버프 합산 서버 권위 재계산 — 🔄 | T2 | 🟢 (**증분 1·2·3 완료 2026-06-14**: ① 스탯 전파 = GameServer 가 `ProgressionService.GetStatsAsync`(단일 합산 권위, LevelTable 룩업)로 계산해 `GameStartRequestedMessage.PlayerInfo`에 적재 → SocketServer `InitPlayerState`가 `PlayerState`에 세팅. **SocketServer DB 접근 0 — "합산 결과"만 메시지로 받음**(근거 authority-model §4c). ② 플레이어→몬스터 데미지 = `StatCombatMath.MeleeDamage(base+AttackPower−Defense)`(Shared 결정론, `CombatHandler.ScaleDamageByStats`) — AP=0이면 base 동일=하위호환. ③ **몬스터→플레이어 Defense 반영(2026-06-14)**: `Room.TickMonsters`가 `StatCombatMath.MeleeDamage(MonsterDef.AttackDamage, 0, PlayerState.Defense)`로 계산→서버 HP 차감 + `S_ApplyEffect.Amount`(신규 필드)로 권위 수치 전달. 클라 `EffectReceiver→ASC.ApplyEffectAuthoritative(healthOverride)`가 카탈로그 고정값 대신 그 값 적용(Amount=0=버프/디버프 하위호환). 클라 예측-정정은 미도입(서버 수치 렌더, EF-2d 후속). 검증(단위+통합+**E2E**): GetStatsAsync 2 + 스탯전파 4 + StatCombatMath 4 + ScaleDamage 3 + StartGame 메시지 스탯적재 + 보상→레벨업 E2E + **MonsterAttack Defense 2(SocketServer 103) + 클라 HealthOverride 2(EffectSystemTests 9)** — 전체 그린 **GameServer 272·SocketServer 103·Shared 34·클라 EditMode**. **던전 플레이 검증 통과(2026-06-15: Lv1 Def5가 slime AD5에 `amount=-1`·HP 1씩)**. ④ **Main 대응판(클라전용, 2026-06-15)**: `LocalMonster`도 `StatCombatMath.MeleeDamage(attackDamage,0,holder.Defense)`로 Defense 반영(서버/패킷0, 던전=서버권위 §2.26 ↔ Main=클라권위 §2.26b). 상세 codemap §2.23·§2.26·§2.26b. **잔여 = 장비/버프 합류는 3.2**)
- **2.5 사망/부활** — 🔄 | T1 | 🔵
  - **2.5.1** 사망 처리 — HP 0 다운/리스폰 (전투 루프 필수) — ✅ | T1 | 🔵 (**플레이 검증 완료 2026-06-13**: Main 타이머 리스폰(`LocalRespawnController`, Main 전용 등록=던전 다운잠금 유지)+`LocalMonster` 근접공격(사망 트리거)+다운→3s 부활 로그 플레이 확인. 다운포즈 Animator 클립은 클라 발전 시(로그 대체). **ⓔ-1 로컬 사망 게이트 코드 완료 2026-06-11**: GAS 정리(ⓐ~ⓓ) 위에 `State.Dead` 태그로. HP≤0(클라 결정론) → `PlayerCharacterAgent`가 `ASC.AddTag(State.Dead)` → `Update` 게이트가 Action(공격/상호작용) 무시 + `base.Update()` 미호출로 Locomotion 정지 = **던전 다운-잠금**. 상수 `GameplayTags.Dead`(Shared). 서버 빌드 0오류. **ⓔ-2 원격 다운 가시성 완료 2026-06-11**: `S_PlayerDead`(Union 1823) — 서버 `DungeonLifecycleHandler`가 `C_PlayerDead`→방 브로드캐스트, 클라 `PlayerDeadPacketHandler`→`CharacterSpawner.HandlePlayerDead`(원격=로그+Destroy / 로컬=로그만, 카메라·HUD 보호). SocketServer **80/80**(직렬화 2) + Docker 리빌드. EditMode 4+PlayMode 2(ⓔ-1) 그린. **플레이어 HP 서버 권위 승격 완료 2026-06-11**(authority-model §0 권위결정규칙 + §4): 던전 플레이어 HP=서버권위(기존 클라결정론은 미승인 가정·불사핵 부채). 증분1=서버 데미지누적+HP0 직접감지→S_PlayerDead(C_PlayerDead 격하), 증분2=회복 크로스서버(ConsumeItem→GameServer검증·차감→Redis→SocketServer ApplyPlayerEffect(+heal)+S_ApplyEffect, 던전 ConsumableEffectHandler 미등록). GameServer 252/252 + SocketServer 85/85 + 양서버 Docker. 상세=codemap §2.6b·§2.6e. **완료(2026-06-13)**: Main 타이머 리스폰·회복수치 단일소스(SO 저작→bake, §2.6c)·다운/부활. 다운포즈 Animator 클립=클라 발전 시 보류(로그 대체). 던전 내 부활=2.5.2(별도 T2))
  - **2.5.2** Co-op 부활 — 다운된 아군 살리기 — ⬜ | T2 | ⚪
- **2.6 전투 보조** — ⬜ | T2 | ⚪
  - **2.6.1** 회피/구르기(Dodge) — 무적 프레임·모션 (입력 `DodgePressed` 존재) — ⬜ | T2 | ⚪
  - **2.6.2** 상태이상/CC — 스턴·슬로우·넉백 (GAS 태그/이펙트 확장) — ⬜ | T2 | 🔵
  - **2.6.3** 타겟팅/락온 — ⬜ | T2 | ⚪
- **2.7** 스킬 트리/습득·강화 — ⬜ | T3 | ⚪
- **2.8** 직업/클래스 — ⬜ | T3 | ⚪

### 3. 아이템 / 경제 시스템
- **3.1 인벤토리(Inventory)** — `Inventory` 신규 도메인(서버 권위 영속) — ✅ | T1 | 🟢 (2026-06-07 완료. 상세 = codemap §2.14. **설계**: ⓐ 정의=코드 `ItemCatalog`(DB 아님) → DB엔 소유만 ⓑ `InventoryItem`=스택형 `(UserId,ItemId)→Quantity`(장비 인스턴스는 3.2) ⓒ 키=user_id(미래 character_id, [[character-swap-direction]]) ⓓ 캐시=Hash 1키, Update→DEL ⓔ proto=`GetInventory` pull(획득 push·보상배선은 3.3, UI는 7.2) ⓕ `InventoryInstaller` 신규. **검증**: 서버 단위 18 + Testcontainers 통합 11 = **29/29**(2026-06-07 **D(소비) 추가로 CRUD 완성** — `ConsumeItemAsync`+`RemoveQuantityAsync`), 서버 빌드 0오류, 클라 Generated 재생성. ※잔여(범위 밖): 소비 클라 RPC·포션 효과(3.8)·획득 드랍 배선(3.3)·인벤토리 UI 마감(7.2))
  - [x] **3.1.1** Domain — `InventoryItem`(소유·수량규칙) + `ItemCatalog`/`ItemDef`/`ItemGrade`(정의 시드 3종)
  - [x] **3.1.2** Application — `IInventoryService`/`Service`·`IInventoryRepository`·`ItemGrantResult`(보상 진입점)
  - [x] **3.1.3** Infrastructure — `InventoryRepository` (PostgreSQL + Redis Cache-Aside+Delete, `AsNoTracking`)
  - [x] **3.1.4** EF 마이그레이션(`inventory_items` 복합키) + DbSet + Config + `InventoryInstaller`
  - [x] **3.1.5** `inventory.proto`(GetInventory) + `InventoryGrpcService` + 클라 Generated 재생성 (획득 push는 3.3로 보류)
  - [x] **3.1.6** 단위(엔티티·카탈로그·서비스 13) + Testcontainers 통합 7(캐시 무효화 계약)
- **3.2** 장비(Equipment) — 착용 슬롯·장비 스탯 모디파이어 → 2.4 합산 — ✅ | T2 | 🟢 (**서버 풀스택 완료 2026-06-15**, 상세 = codemap §2.27. 스택형 재사용(개별 인스턴스 X, 강화=3.6)·Weapon/Armor 2슬롯·`EquipmentCatalog` 분리. **핵심 합류점=`ProgressionService.GetStatsAsync` 한 곳에 장비 Σ → SocketServer/전투 무수정 반영**(authority-model §4c). 증분 3.2.1~3.2.6 전부 완료(도메인→앱→인프라→EF→합산→proto). 시드 `sword_basic`(Weapon+5AP)·`armor_leather`(Armor+3Def). **검증: 장비 단위/통합 28(Catalog 4·Service 8·Repo통합 8·Progression합산 1·gRPC 7) + GameServer.Tests 306/306(ProgressionService 생성자 변경이 깬 DI 호스트 4곳 보정: DungeonResultConsumer통합·DungeonResultReward E2E·GameStart E2E·RoomLifecycle — IEquipmentService 미등록 회귀), 전체 솔루션 빌드 0오류, 클라 Generated 재생성·Game.Network 빌드 0오류**. **후속(범위 밖)**: 클라 wrapper/System service·장비 UI 배선(7.2, 소비자 생길 때)·외형 동기화·강화(3.6))
  - [x] **3.2.1** Domain — `EquipmentSlot`/`EquipmentStatModifier`/`EquipmentDef`/`EquipmentCatalog`(시드 2종) + ItemCatalog 장비 등록
  - [x] **3.2.2** Application — `IEquipmentService`/`Service`(Equip/Unequip/GetEquipped + `GetEquippedStatsAsync` 합산, 소유·슬롯 검증) + `IEquipmentRepository` + `UserEquipment` 엔티티 + `EquipResult`. 소유 검증=`IInventoryService` 위임. 단위 `EquipmentServiceTests` 8(장착/미보유/비장비/교체/해제/멱등/합산/빈) 그린
  - [x] **3.2.3** Infrastructure — `EquipmentRepository`(PostgreSQL + Redis Cache-Aside+Delete, `AsNoTracking`, Hash field=(int)slot→itemId) + `UserEquipment.ChangeItem` upsert. Testcontainers 통합 8(set/교체/캐시DEL/HIT/MISS+TTL/clear/빈clear/빈조회) 그린
  - [x] **3.2.4** EF 마이그레이션 `AddUserEquipments`(`user_equipments` (UserId,Slot) 복합키, raw SQL 멱등) + DbSet + `UserEquipmentConfiguration` + RedisKey + DI(`InventoryInstaller`에 합류 — 별도 Installer 안 만듦, YAGNI). 전체 솔루션 빌드 0오류
  - [x] **3.2.5** `GetStatsAsync` 합산 합류 — `ProgressionService`가 `IEquipmentService.GetEquippedStatsAsync` Σ를 base(레벨룩업) 위에 가산. **SocketServer/게임시작 전파는 무수정**으로 장비 스탯이 전투에 반영(단일 합산 권위 authority-model §4c). 생성자 주입 추가로 기존 3 테스트 사이트 보정(`FakeEquipmentService`). 단위 `장비_스탯은_레벨_base_위에_가산된다` + 회귀 90/90 그린
  - [x] **3.2.6** `equipment.proto`(Equip/Unequip/GetEquipment, 도메인별 gRPC 패턴 = 별도 서비스) + `EquipmentGrpcService`(인증 게이트 + proto↔도메인 슬롯 enum 매핑 + 결과 매핑) + `MiddlewareInstaller` 등록 + **ClientCodegen 재생성**(클라 stub `Equipment.cs`·`EquipmentGrpc.cs` + 래퍼 `IEquipmentGrpcService`/`EquipmentGrpcService` 자동). 단위 `EquipmentGrpcServiceTests` 7 그린 + 클라 `Game.Network` 컴파일 0오류. **Docker gameserver 리빌드 + PlayMode E2E `EquipmentE2ETests` 6/6 그린**(미보유거부/미인증거부/빈조회/멱등해제/미지정거부, Docker 대상). 부수: `E2ETestBase.SetUp` 토큰 리셋 추가(순서 의존 "미인증" 버그 수정, Https E2E 46/46 회귀확인). **System service/Model·장비 UI 배선은 7.2**(소비자 생기면)
- **3.3** 루트/드랍(Loot) — **월드 드랍 + 줍기**, **2경로** — ✅ | T2 | 🟢 (**설계=** [loot-drop.md](loot-drop.md). **던전 경로 서버 풀스택(증분 1~5) + 클라 렌더/줍기(증분 6) 완료·플레이 검증 통과 2026-06-08**, 상세 = codemap §2.16·§2.15·[chapter-15](../portfolio/chapter-15-loot-drop-inventory.md). roll·바닥·줍기중재=SocketServer / 지급=GameServer(`GrantItemAsync`, PickupId 멱등) / Redis Stream 단방향. 패킷 1830~1833. 클라=`GroundItemEntity`(IInteractable, E 줍기)·`GroundItemSpawner`·`GroundItem.prefab`(Layer7). **플레이 검증**: 슬라임 처치→드랍→E 줍기→인벤토리 아이콘 표시까지 통과. **Main 경로 증분8(`GrantItem` gRPC+서버 가드) 완료 2026-06-09** — proto `GrantItem(item_id,qty)`→`GrantItemResponse{result,new_quantity}`, 서버 가드=인증(AuthInterceptor 자동)+호출당 수량상한(`MaxGrantPerCall=99`, gRPC 진입점에만 — 던전 `LootGrantConsumer`는 무관)+catalog 검증(`GrantItemAsync` 재사용). 클라 Generated 재생성 + `IInventoryGrpcService`/`InventoryGrpcService` 래퍼. **서버 단위 `InventoryGrpcServiceTests` 5/5 + 클라 `InventoryE2ETests` PlayMode 6/6 그린**(2026-06-09, GameServer 리빌드 후 — 정상지급·수량상한거부·0이하거부·미존재거부·미인증거부). 서버빌드·Game.Network 0오류. 상세=codemap §2.18. **증분9a(DropTable 데이터화) 완료 2026-06-09** — 하드코딩 `Server.Loot.DropTable` → `Shared.Gameplay.DropTableRoll`(순수)+`Shared.Infrastructure.DropTableCatalog`(임베디드 `drop-tables.json`, spawn-layouts 컨벤션). 던전·Main 동일 roll 공유. 동작 보존(슬라임 동일). Shared.Gameplay 22/22 + SocketServer 72/72. **9a-2(`DropTableDefinition` SO + `DropTableExporter` bake) 완료 2026-06-09** — 클라 SO 저작→JSON bake(MapDefinition 컨벤션), 클라는 SO 직접 읽음. `.asset` 부트스트랩·goblin 추가→Export→임베디드 반영 검증(`DropTableCatalogTests` goblin, SocketServer **73/73**) + socketserver Docker 리빌드. Unity 컴파일 0오류. 상세=codemap §2.19. **9b·9c(Main 로컬 전투) 완료 2026-06-09** — `LocalMonster`(HP·간단AI·TakeDamage→OnDied)·`MainMonsterSpawner`(비-Joined만)·`LocalCombat`(OnAttackPerformed→`HitboxMath` 로컬 히트, Shared DLL 공유) + `CharacterSpawner.AttachLocalCombat`/`MainLifetimeScope` 등록. Unity 컴파일 0오류(상세 codemap §2.20). **9d(드랍→줍기→지급) 완료 2026-06-09** — `MainMonsterSpawner.HandleDied`→`DropTableRoll`(공유)→`LocalGroundItem`(E 줍기→`GrantItemAsync` 증분8)→디스폰. 클라 `Shared.Gameplay.dll` 재배치 완료. Unity 컴파일 0오류. **→ 3.3 던전·Main 양 경로 코드 완결.** **Main 루트 E2E `MainLootE2ETests` 1/1 그린**(2026-06-09, 클라 SO→`DropTableRoll`→`GrantItem`→인벤토리, Docker) + GameServer Dockerfile에 `Shared.Gameplay.csproj` COPY 추가(9a 전이의존 빌드 실패 수정). **Main 플레이 검증 통과(사용자, 2026-06-10)**: 공격→LocalMonster 사망→바닥 드랍 스폰→E 줍기→인벤토리 아이콘 전체 1판 시각 확인(`LocalMonster.prefab`/`LocalGroundItem.prefab` 제작·할당 완료). **→ 3.3 던전·Main 양 경로 코드+E2E+플레이 완결 ✅.** **후속(범위 밖)**: 정식 획득 토스트 위젯(7.x, 현재 로그))
  - [x] **3.3.1** DropTable(SocketServer 정적) + GroundItem + roll/줍기 단위테스트
  - [x] **3.3.2** 패킷 4종 + Union(1830~1833) + `ItemPickedUpMessage`(Shared)
  - [x] **3.3.3** Room GroundItem 보유·SpawnGroundItem·TryPickup(경쟁중재) + CombatHandler 사망분기 드랍 + 입장 로스터
  - [x] **3.3.4** `C_PickupItem` 핸들러 + `ILootPickupPublisher`/MessageQueue(`stream:game:loot:pickup`) + DI
  - [x] **3.3.5** GameServer `LootGrantConsumer`(ResilientStreamConsumer) → GrantItemAsync, PickupId 멱등 + 통합/E2E
  - [x] **3.3.6** 클라: 패킷 미러(ClientCodegen) + `ISocketPacketState` 바닥아이템 상태/이벤트 + `LootPacketHandler` 3종 + `GroundItemEntity`(IInteractable 줍기) + `GroundItemSpawner` + DI(`DungeonLifetimeScope`/`CharacterPrefabSettings`) + `GroundItem.prefab`(Layer7·트리거·구체). 클라 빌드 검증(Game.Network/Gameplay/VContainer 0오류). **플레이 검증 통과(2026-06-08)**: 드랍→E 줍기→인벤토리 지급·아이콘 표시. (E 줍기 버그=오브가 바닥높이라 감지구체 미스 → +0.7 띄움으로 해결) 정식 획득 토스트 위젯은 후속(현재 로그)
  - 🔄 **3.3.7** 풀 E2E(사냥→드랍→줍기→인벤토리) + Main 경로(3.3.8~10) — **던전 풀 E2E 작성 완료**(2026-06-08): `SocketE2ETests.RawSocket_슬라임_처치_드랍_줍기하면_GameServer_인벤토리에_지급된다`(처치→`S_SpawnGroundItem`→`C_PickupItem`→`S_ItemPickedUp`/`S_GroundItemRemoved`→Stream→GameServer 지급→`GetInventory` 폴링). **결정성 확보**: `DropTable` 슬라임 `potion_hp_small` 보장 드랍(Chance 0.5→1.0, gold 0.2 유지) — 슬라임 1마리뿐인 dungeon_01에서 40% 거짓실패 제거. SocketServer.Tests **66/66**(DropTable 5, 보장 드랍 1 신규) + 양 서버 Docker 리빌드·재배포 완료. **PlayMode 실행 통과(2026-06-09, UnityMCP)**: SocketE2ETests 13/13 + GameSessionConnectorE2ETests 3/3 = **16/16 그린**(루트 풀E2E 포함). **Main 경로: 증분8(`GrantItem` gRPC+가드)·증분10(가드 E2E 작성) 완료 2026-06-09(codemap §2.18). 증분9(Main 로컬 sim 배선) 잔여.**
- **3.4** 재화(Wallet) — 골드 보유·증감(서버 권위) — ⬜ | T2 | 🟢
- **3.5** 상점(Shop) — 구매/판매·가격·재고 — ⬜ | T2 | ⚪
- **3.6** 강화/크래프팅 — ⬜ | T3 | ⚪
- **3.7** 아이템 등급/레어도 + 도감 — ⬜ | T2 | 🟢
- **3.8** 소모품/포션 — HP/MP 회복 (인벤토리 소비 → GAS 효과) — ✅ | T2 | ⚪ (**α 로직 슬라이스 완료 2026-06-10**: MVI Side Effect 패턴. `ConsumeItem` gRPC(서버 권위 차감) + 클라 System `ConsumeItemAsync`(proto 은닉) + `ConsumableCatalog`(클라 SO, itemId→stat/amount/policy, 서버 미공유=회복은 클라 권위) + `InventoryModel.UseItem` 의도 → consume 성공 시 **Side Effect** `OnConsumableUsed`(회복)·`OnToast`(메시지) 발행 → `ConsumableEffectHandler`가 GAS `ASC.ApplyEffect`로 적용(HUD 자동). 권위=인벤토리 수량(서버)/HP 회복(클라). `InventoryGrpcServiceTests` 9/9(ConsumeItem 4) + `InventoryModelTests` 5/5(UseItem Side Effect 2). 서버빌드·Unity 컴파일 0오류. **β UI 코드 완료 2026-06-10**: 슬롯 클릭(`ItemContentsSlot` IPointerClickHandler)→`Inventory.OpenActionPanel`(Canvas 직속 `ItemActionPanel` 슬롯 오른쪽 배치 + 풀스크린 `BackDropButton` 뒤 클릭 닫기)→사용 버튼→`UseItem`, `OnToast`→로그. 슬롯 클릭=`itemButton`(Button), 패널=온디맨드 Addressable(`InstantiateAsync`↔`ReleaseInstance`). **플레이 검증 완전 통과(사용자, 2026-06-10)**: 슬롯 클릭→패널→사용→ConsumeItem(서버 차감)→Side Effect→**GAS ASC.ApplyEffect HP 회복**+토스트 전 경로 동작(`ConsumableCatalog.asset` potion_hp_small→Health 저작 완료, `[ConsumableEffectHandler] 효과 적용` 로그 확인). Unity 컴파일 0오류. 상세=codemap §2.21. **→ 3.8 코어 완결 ✅.** **후속(폴리시)**: 소모품만 사용가능 제한(현 gold도 차감)·정식 토스트 위젯(7.x))

### 4. 콘텐츠 시스템
- **4.1 몬스터** — ✅ | T1 | ⚪ (코어 완료, 상세 = `§M3`)
  - **4.1.1** 패킷 `S_SpawnMonster`(1810)/`S_MonsterState`(1811)/`S_MonsterDead`(1812) + Union — ✅ | T1 | ⚪
  - **4.1.2** 서버 스폰 — `Room` 몬스터 보유(단일 `RoomTickService`, MonsterManager 분리 안 함) + `monsterSpawns[]`/패트롤/경계 Map Editor 저작→파싱 — ✅ | T1 | 🟢
  - **4.1.3** 서버 AI 틱 — `MonsterAiMath`(Patrol/Chase/Attack + bounds clamp) → `S_MonsterState` — ✅ | T1 | ⚪
  - **4.1.4** 서버 전투 — 플레이어→몬스터 피격/사망(GAS, `S_MonsterDead`) + 몬스터→플레이어 공격(`S_ApplyEffect`) 양방향 — ✅ | T1 | 🔵
  - **4.1.5** 클라 `MonsterEntity` 스폰/보간/사망 (`RemoteDriver` 패턴 재사용) — ✅ | T1 | ⚪
  - **4.1.6** 몬스터 웨이브/스폰 페이즈 — ⬜ | T2 | ⚪ (**= 단일 SpawnSystem 승격 1순위 트리거**. 착수 시 [spawn-system-evolution.md](spawn-system-evolution.md)대로 `SpawnRequest`+`SpawnSystem` 신설, 기존 `Room.SpawnMonsters` 재사용. 4.4 퀘스트·4.6.1 존 스폰도 같은 라우터에 합류)
- **4.2 던전 클리어/보상(DungeonResult)** — ✅ | T1 | 🟢 (클리어+실패 루프 + Exp 보상 전원 지급 완료. 결과패널 아트·PlayMode 실행은 Unity)
  - **4.2.1** 패킷 `S_DungeonClear`(1820) + Union — ✅ | T1 | ⚪
  - **4.2.2** SocketServer 클리어 감지 → `DungeonClearMessage` → Redis Stream — ✅ | T1 | 🟢
  - **4.2.3** GameServer `DungeonResultConsumer` → 보상 산정(경험치) — ✅ | T1 | 🟢 (Shared 카탈로그 expReward → 참가자 전원 Exp 지급·RoomId 멱등. 아이템은 범위 제외)
  - **4.2.4** 보상 지급 — 2.3 Progression.AddExp 호출(RoomId 멱등 Redis SET, at-most-once) — ✅ | T1 | 🟢 (Inventory·Outbox는 범위 제외, Exp 전용)
- **4.3** 던전 메타 — `DungeonRoom.DungeonId` 추가(=9.2 부채) — ⬜ | T1 | 🟢
- **4.4** 퀘스트(Quest) — 수주/진행/완료·보상 (`Quest` 신규) — ⬜ | T2 | ⚪
- **4.5** NPC/대화(Dialogue) — 상호작용·대화 트리 (`Npc` 신규) — ⬜ | T2 | ⚪
- **4.6 월드/존(World)** — 오픈월드 PVE 맛보기 (`World` 신규) — ⬜ | T2 | ⚪ (**Main 몬스터의 전제**: 현재 Main은 소켓 미연결이라 서버 권위 몬스터 없음 → Main에 몬스터를 내려면 **Main이 네트워크 World 세션**이어야 함. SocketServer `Room`→`World` 일반화 시 드랍/줍기(3.3)가 그대로 적용. 상세 = [loot-drop.md](loot-drop.md) §6)
  - **4.6.1** 존 맵·존 전환·포탈 — ⬜ | T2 | ⚪
  - **4.6.2** 텔레포트/패스트트래블 — ⬜ | T2 | ⚪
  - **4.6.3** Main 몬스터/드랍 = **Client 로컬 시뮬·렌더 + 서버 검증(B-lite)** — ✅ | T1 | 🟢 (**플레이 검증 완료 2026-06-13** — 서버 솔루션 374 + PlayMode E2E 그린 + 플레이: 슬롯 스폰→킬→`ClaimKill` 서버 roll 지급(potion+gold)→5s 재스폰, 쿨다운 파밍 차단 전부 확인. **승격 2026-06-13**: 구 "클라 신뢰+GrantItem 가드"는 **무한 파밍 핵** → 폐기. **결정 정본 = [authority-model.md §4b](authority-model.md)**. 서버가 map 스폰 데이터 보유(SO→bake→Shared 교리) → `ClaimKill(mapId, slotId)` gRPC: ①슬롯∈map ②per-user 쿨다운(Redis claim) → **서버 권위 DropTableRoll → GrantItemAsync**. `GrantItem(itemId,qty)`는 Main 경로 제거(치팅 진입점 봉쇄). 서버 실시간 AI 없음(map데이터+클레임상태만). **증분**: ①spawn-layouts에 Main map+`SpawnSlot{id,monsterType,respawnCooldownMs}` ②클라 슬롯기반 스폰 ③`ClaimKill` 서버검증+roll+grant ④클라 줍기→ClaimKill 교체 ⑤GrantItem Main제거 ⑥테스트(슬롯/쿨다운 거부·E2E). B-full(서버 권위 풀 시뮬)=co-op 필요 시 YAGNI)
- **4.7 상호작용 오브젝트** (`IInteractable` 확장) — 🔄 | T2 | ⚪
  - **4.7.1** 문/상자/채집 노드·파괴 가능 오브젝트 — 🔄 | T2 | ⚪
  - **4.7.2** 함정/환경 기믹 — ⬜ | T3 | ⚪
- **4.8** 보스/특수 몬스터 — ⬜ | T3 | ⚪

### 5. 소셜 / 멀티플레이
- **5.1** 파티 — 방=파티(현 로비 일부 충족) + 인게임 파티 UI — 🔄 | T2 | ⚪
- **5.2** 인게임 핑/이모트 — Co-op 의사소통 — ⬜ | T2 | ⚪
- **5.3** 플레이어 간 거래(Trade) — ⬜ | T3 | ⚪
- **5.4** 길드/클랜 — ⬜ | T3 | ⚪
- **5.5** 랭킹/리더보드 — ⬜ | T3 | ⚪
- **5.6** 친구/소셜 — ⬜ | T3 | ⚪

### 6. 메타 / 영속
- **6.1** 캐릭터 진행 영속 — 레벨·인벤토리·장비 DB 영속 (2.3/3.1/3.2 합류) — ⬜ | T1 | 🟢
- **6.2** 던전 결과 → 로비 복귀 — 결과 처리 + 기존 던전→Main 복귀 재사용 — ✅ | T1 | ⚪ (M4: 클리어/실패 → `GameHud` 패널 → `ReturnToLobby` 재사용, MPPM 1판 루프 시각 검증)
- **6.3** 설정/옵션 — 그래픽·사운드·키 바인딩 + 영속 — ⬜ | T2 | ⚪
- **6.4** 재접속/세션 복구 — 인게임 끊김 복구 — 🔄 | T2 | 🟢 (**재접속 유예 창(grace) 구현 2026-06-09**, 상세 = codemap §2.17. 크래시/끊김 시 방에 인원 남으면 `PlayerState`를 60s(`Room.ReconnectGraceMs`) 보존 → 재접속 시 보존 상태로 던전 즉시 복귀, 만료 시 `RoomTickService` 스윕 정리. **9.4 부채수정이 만든 재접속 회귀(크래시=재입장 불가)를 해소** — 명시퇴장만 즉시 제거, 크래시는 유예+AI 유령필터. SocketServer.Tests 72/72 + PlayMode **복귀 플로우 Green/Red 6종**(보존위치 복귀·유예중 보류 / 명시퇴장·전원끊김·유예만료 거부) + 기존 재접속 2 = `SocketE2ETests` **19/19** + `GameSessionConnectorE2ETests` 3/3. **잔여**: 재접속 실패/방종료 **클라 팝업**, 방 자체 유예(전원 끊김 시 방 60s 생존))
- **6.5** 통계/플레이 기록 — ⬜ | T3 | ⚪
- **6.6** 도전과제/업적 — ⬜ | T3 | ⚪
- **6.7** 튜토리얼/온보딩 — ⬜ | T3 | ⚪

### 7. UI / UX (클라 프레젠테이션 — MVI, View는 자기 Model만 참조)
- **7.1** 결과/보상 화면 (대응 6.2/4.2) — 🔄 | T1 | ⚪ (DungeonClear 프리팹 GameHud 배선 완료(`dungeonClearView` 할당, 전체화면·기본 비활성). **DungeonFailed.prefab 미존재 → `dungeonFailedView` 미할당**. 프리팹 아트 잔여는 Unity)
- **7.2** 인벤토리/장비 UI (대응 3.1/3.2) — 🔄 | T2 | ⚪ (2026-06-07 MVI 스택 코드 완료 → **2026-06-08 열기·슬롯·아이콘 플레이 검증 통과**, 상세 = codemap §2.15. Network→System→Presentation(`InventoryModel`+`ItemDisplayCatalog`)→GUI(`Inventory`/`UniversalSlot`/`ItemContentsSlot`) MVI. **2026-06-08 수정**: ① 열기 버그(`InventoryViewController`가 `MainLifetimeScope` 누락 → Main 씬 무반응) 수정 ② 슬롯 재설계(`UniversalSlot`=컨테이너 고정 30 + `ItemContentsSlot` 동적 Content, 타입별 슬롯 대비, 두 prefab Addressable 로드) ③ I키=`GameHud.Update` Keyboard 폴링(임시, 래퍼 재생성 후 InputRouter 이관) ④ `ItemDisplayCatalog` Resources 경로 버그 수정 + 서버 itemId 정렬(아이콘 표시). **잔여**: 탭(Material/Quest/Etc) 토글 배선, I키 정석 배선. **장비 UI(3.2 연동) 코드 완료 2026-06-16**: ① **공통 enum 통일** — GUI/서버 `EquipmentSlot`→`Shared.Gameplay.Equipment.EquipmentType`(8슬롯: Header/Armor/Shoose/Glove/Shield/Weapon/Ring/Necklace, 카탈로그는 Weapon/Armor만 채움) + proto enum 8값 확장·재생성·DLL 재배치. ② **클라 MVI 신설** — System(`IEquipmentService`/`EquipmentService`, OnChanged 이벤트) → Presentation(`EquipmentModel`/`State`/`Intent`/`SlotModel`) → GUI(`Equipment` View, EquipmentModel만 주입). ③ **장착 트리거** — `ItemActionPanel` 분류별 버튼(장비=장착/소모품=사용, 그외 비활성) → `InventoryModel.EquipItem`(IEquipmentService 주입) → 성공 시 OnChanged로 장비창 자동갱신. ④ **열닫 연동** — I키/HUD=인벤토리+장비 쌍 토글(`InventoryViewController`가 `EquipmentViewController` Show/Hide 참조), K키=장비 단독 토글(`InGameIntent.ToggleEquipment`), 각 X버튼=독립 닫힘. Addressable `Equipment.prefab`. **검증**: 서버 306/306·장비통합 8·**E2E 6/6**(enum 통일 후)·클라 컴파일 0에러·InventoryModelTests 5/5. 상세 codemap §2.27. **잔여(사용자 Unity)**: Equipment.prefab 슬롯 인스펙터 할당(QuickSetting)·플레이 검증. **후속 작업 2026-06-16**: ⑤ **장비 8종 추가**(8슬롯 1종씩: sword_basic/armor_leather + helmet_iron/boots_leather/gloves_leather/shield_wooden/ring_power/necklace_vitality) → 서버 `ItemCatalog`/`EquipmentCatalog`(코드) + 클라 `ItemDisplayCatalog.asset`(표시, 아이콘은 사용자 할당). ⑥ **몬스터 드랍**: 유일 스폰 몬스터 slime 에 장비 8종 추가(0.15~0.3 확률, `DropTableDefinition.asset`→Export→`drop-tables.json`→양 서버 리빌드). goblin 미스폰이라 potion만 유지. ⑦ **UI 입력 차단 버그픽스**: 인벤토리/장비창 열린 동안 WASD 이동 누수 → `UiInputCaptureBehaviour`+`IInputContext.EnterUi/ExitUi`(refcount) 적용(DungeonRoomLobbyView와 동일, `InventoryModel`/`EquipmentModel`에 `BeginUiCapture/EndUiCapture` 추가). ⑧ **InputRouterTests 9개 픽스**: 테스트 Setup에 `_actions.Player.Enable()` 누락(InputRouter가 맵 활성화를 GlobalInputInitializer에 위임하도록 바뀐 뒤 미반영) → 추가. **검증**: 서버 빌드 0 + DropTableCatalog 6 + DropTableRoll 5 + GameServer 장비/인벤 74 + InputRouterTests **10/10** + EquipmentE2E **6/6** + SocketE2E 26/27. **별건 회귀 수정 완료 2026-06-17(E2E 검증 통과)**: SocketE2E `RawSocket_몬스터에게_죽으면_S_PlayerDead` 회귀를 근본 수정. ① **1차 오진(2026-06-16, 미검증)**: 픽스처 `test_brute`가 2.4 Defense(`max(1,AD−Def)`)로 약해진 게 원인인 줄 알고 `attackDamage 5→9999`로 올렸으나, Unity 브리지 복구 후 실제 E2E를 돌리니 **여전히 실패**. ② **진짜 원인 = 입장 전 사망 레이스**: 몬스터는 `_playerStates`(GameStart 시 초기화) 기준으로 공격하며 소켓 join 여부를 안 봄. test_brute 가 (0,0,0)·호스트 slot0 도 (0,0,0) → 9999=첫 100ms 틱 즉사인데, 그 시점엔 테스트 collector 가 아직 미입장 → `S_PlayerDead` 가 빈 방에 발행돼 **유실**(이후 호스트는 `_downed`라 재공격 안 됨 → 영영 못 받음). 2.4 이전 5뎀일 땐 ~2초 걸려 죽어 join 이후라 캐치됐던 것(=잠복 결함이 즉사로 표면화). ③ **수정(방향 A)**: `PlayerState.HasJoined` 추가 — `InitPlayerState`=false(미입장), `C_PlayerJoin` 성공 시 `Room.MarkJoined`(구 `MarkReconnected` 개명)가 true. `TickMonsters` 타깃 필터에 `HasJoined` 추가 → 아직 입장 안 한(로딩 중) 플레이어를 몬스터가 죽이는 고스트-공격 결함 자체를 제거. test_brute 9999 는 유지(입장 후엔 결정론적 즉사 = 픽스처 의도). **검증: SocketServer 단위 103/103 + 서버 솔루션 빌드 0오류 + socketserver 리빌드·재배포 후 PlayMode SocketE2ETests 21/21 그린**(사망·재접속 포함). 영향 단위테스트(MonsterAttackTests·PlayerHpServerAuthorityTests 몬스터데미지)에 `MarkJoined` 보정. **⑨ 장비 해제 + 인벤토리 연동 2026-06-16**: ⓐ Equipment View — 미착용 슬롯은 Icon·Button GameObject 비활성/착용 시 활성, 슬롯 Button 클릭 → `ItemActionPanel`(Addressable) 팝업 → Unequip 버튼 → `EquipmentIntent.Unequip(slot)`. ⓑ **패널 로직 공용화** — Inventory의 패널 열기/배치/닫기를 `ItemActionPanelController`(GUI/Common)로 추출, Inventory·Equipment 공유(DRY). `ItemActionPanel.Bind`에 onUnequip/canUnequip 추가(소모품=use, 장비=equip, 장비창=unequip). ⓒ **착용↔인벤토리 동기** — `InventoryModel`이 착용 itemId를 표시에서 제외 + `IEquipmentService.OnChanged` 구독 → 장착 시 인벤토리에서 사라지고 장비창에 나타남, 해제 시 반대(양쪽 동시 갱신). **검증**: 클라 컴파일 0 + InventoryModelTests **6/6**(착용 제외 필터 신규). **잔여(사용자 Unity)**: Equipment.prefab 슬롯/버튼·ItemActionPanel unEquipButton 인스펙터 할당, 플레이 검증)
- **7.3** 캐릭터 정보/스탯창 (대응 2.3/2.4) — ⬜ | T2 | ⚪
- **7.4** 퀘스트 UI/추적 HUD (대응 4.4) — ⬜ | T2 | ⚪
- **7.5** 대화 UI (대응 4.5) — ⬜ | T2 | ⚪
- **7.6** 상점 UI (대응 3.5) — ⬜ | T2 | ⚪
- **7.7** 미니맵 HUD (대응 4.6) — ⬜ | T2 | ⚪
- **7.8** 설정 메뉴 (대응 6.3) — ⬜ | T2 | ⚪

### 8. 오디오
- **8.1** BGM/환경음 — ⬜ | T3 | ⚪
- **8.2** 전투 SFX/타격감 (HitStop 연계) — ⬜ | T2 | ⚪
- **8.3** UI SFX — ⬜ | T3 | ⚪

### 9. 인프라 / 품질 / 기술 부채
- **9.1** SocketServer IP 하드코딩 → appsettings.json — ✅ | 높음 | 🟢 (2026-06-07: 실은 이미 `ServerOptions`(Server 섹션)로 env 구성됨 — docker `Server__Ip/AdvertiseIp`. 남은 갭 = `appsettings.json`에 `Server` 블록 명시(자기문서화) + docker `AdvertiseIp`에 "원격 배포 시 호스트 IP 교체" 주석. 코드 변경 0)
- **9.2** `DungeonRoom.DungeonId` 부재 (= 4.3) — ⬜ | 중간 | 🟢 (**M5 폴리싱 태스크로 이관** — M5 §"메타/콘텐츠"에 명시. B트랙이 MapId 카탈로그로 우회했고, 다중 던전/선택 UI 생길 때 도입. 지금은 YAGNI)
- **9.3** Auth: 로그인 시 이전 세션 강제 만료 — ✅ | 중간 | 🟢 (2026-06-07: **이미 end-to-end 구현 확인 + 회귀 테스트 추가**. ① 로그인=`UserSessionRepository.CreateSessionAsync`가 기존 세션 DB+캐시 제거 ② **`ValidateTokenAsync`가 매 요청 sid 클레임→세션 저장소 존재 검증**(`AuthService.cs:242`), `AuthInterceptor`가 호출 → 기기A 세션 제거 즉시 다음 요청 거부(**15분 창 없음**) ③ refresh 바인딩 실패도 세션 제거. 테스트: `새_기기_로그인_시_이전_세션은_강제_만료된다` + Fake 충실도 수정)
- **9.4** `Room.Leave` 시 `_playerStates` 정리 누락 — ✅ | 낮음 | ⚪ (2026-06-07: `Room.Leave`가 session.UserId로 `_playerStates.Remove` — 떠난 플레이어 유령 잔류(AI 타깃/위치) 차단. SocketServer.Tests +1)
- **9.5** Redis Consumer name `socket-1` 고정 → 동적 생성 — ✅ | 낮음 | ⚪ (2026-06-07: `socket-{Environment.MachineName}` — 수평 확장 시 PEL 충돌 방지, 컨테이너 hostname 안정적이라 재시작 PEL 복구 유지)
- **9.6** `GetRooms` count/페이징 정책 — 🔄 | 낮음 | ⚪ (2026-06-07: **N+1 해소 완료** — 방마다 2왕복 → `GetPlayersByRoomIdsAsync` 1쿼리 + 유저 1쿼리 배치.
  **잔여(보류 — 방 수 적어 YAGNI, 필요 시 착수)**: 페이징/총개수. 계획 = `lobby.proto` `GetRoomsRequest`에 `page`/`pageSize`, `GetRoomsResponse`에 `total_count` 추가(공개계약 변경 → **클라 `Generated/` 재생성 필수**) → `IDungeonLobbyService.GetActiveDungeonRoomsAsync(skip, take)` + 전체 카운트 반환. proto 수정 동반이라 명시 승인 후 진행)
- **9.7** status.md stale → plan.md 일원화 — ✅ | 낮음 | 🟢 (2026-06-07: stale status.md **삭제** + 참조(CLAUDE.md·AGENTS.md·plan.md) 정리. 현황 진실원 = plan.md 단일화)
- **9.8** 부하 테스트 + 전체 E2E 회귀 자동화 — ⬜ | 마감 | ⚪
- **9.9** 배포 문서 + 포트폴리오 챕터 마감 — ⬜ | 마감 | ⚪
- **9.10** 컨슈머 복원력 — 일시적 Redis 오류(`LOADING`/연결끊김)에 BackgroundService 컨슈머가 outer catch로 루프 종료 → **영구히 죽던 버그**(게임시작 체인 끊김 실사례). `Shared.Infrastructure/MessageQueue/ResilientStreamConsumer`로 중앙화(while 재시도 + 지수백오프+지터, poison 메시지 격리). `GameStartRequestedConsumer`·`GameSessionReadyConsumer`·`RoomLifecycleConsumer` 이관. SocketServer.Tests 복원력 3 + 양 서버 재배포 검증 — ✅ | 높음 | 🟢

---

## 🟢 세션 트랙 — 서버 도메인(이 세션) vs 🔵 GAS 세션

GAS 세션(2.*·4.1.4)과 **파일·패킷 충돌 없이 병행** 가능한 서버 도메인 트랙:

```
3.1 Inventory ──→ 2.3 Progression ──→ 3.4 Wallet ──→ 4.2 DungeonResult(보상지급)
      │                                                      │
      └──→ 3.2 Equipment (2.4 스탯합산)            6.1 영속 ◀─┘
(+ 부채 9.2 DungeonId · 9.1 IP · 9.3 세션만료 동반 해소)
```

**즉시 착수 후보**: **3.1 인벤토리 도메인**(3.1.1 엔티티부터). 모든 보상/장비/상점/루트의 공통 전제 — 서버 권위 영속 + Cache-Aside 쇼케이스.

---

## 📋 작업 순서 (M1 → M6, 실행 정렬 로드맵)

> 각 단계 = **서버 + 클라 + 테스트(TDD)** 3축, 끝에 **MPPM 2-client** 검증.
> Character 아키텍처(CA)는 의존성에 맞춰 M1(합성/Driver)·M2(GAS)·M5(툴)에 통합.
> **안 함**: ECS/DOTS·Unity Timeline 전투구동·SO를 데이터 진실원·fixed-point/롤백(Co-op)·재작성.

### ✅ M1 — 인게임 진입: "던전에서 서로 움직임이 보인다" (완료) [WBS 1.6]
선행: M0. **현 Locomotion FSM 그대로**(전투 미포함). Dungeon 씬은 그린필드 → 게임플레이 DI/스폰 신규 구성.
- [x] 소켓 진입 흐름(구 A-1/A-2): `SocketApiClient` 등록·`DungeonLifetimeScope`·`GameSessionConnector`(OnGameSessionReady→TCP→`C_PlayerJoin`, 재시도·중복 가드), `C_Auth` 제거, Redis 검증 재작성, 포트폴리오 챕터 11
- [x] 던전→Main 복귀(`InGameModel` MVI·`GameHud`·`GameHudController` Addressable)
- [x] 던전 퇴장 일관성(`PlayerLeftRoomMessage` 일반화·`RemovePlayerFromRoomAsync`·멱등성) — [codemap §2.1](codemap.md)
- [x] 원격 동기화 데이터 토대 — `ISocketPacketState.GetAllPlayers()` (※ reconciler 클래스는 제거, diff는 presenter/spawner 내부로)
- [x] **`AddressableLoader`(Util)** 통합 — Lobby/Hud 중복 로드 일원화
- [x] **`CharacterSpawner`(생성/삭제)** + **Character 합성 + `Driver`(LocalInput/Network)** [CA-4] — Dungeon 씬 게임플레이 DI 구성
- [x] **GameHud HP/MP ↔ GAS 연동** — `GameplayAttribute.OnChanged`→`ASC.OnAttributeChanged`→`LocalPlayerContext`→`InGameModel`→`SliderBall`. EditMode(릴레이)·PlayMode(prefab 동적생성 렌더) 테스트 통과
- [x] 로컬 PlayerCharacter 스폰(SpawnIndex, 결정론 Resolve) + 이동→`C_Move` 송신(`MoveSyncSender`) — PlayMode `CharacterSpawnMultiClientTests` 검증
- [x] 원격 NetworkCharacter 스폰 + 스냅샷 보간(`RemoteDriver`) — 다중 원격·늦은 입장 동적 스폰 PlayMode 3/3 통과. 맵 비주얼=`MapLoader`(MapDefinition.visualPrefab). 저작=`MapEditorWindow`(SO→JSON bake). `monsterSpawns[]` 스키마 선반영(M3 스폰 로직 대기)
- [x] 전원 입장 → 인게임 진입(로딩 게이트 + Fader) — 기존 `S_GameStatus(InProgress)` 재사용(새 패킷 X): `GameStatusPacketHandler`→`ISocketPacketState.OnDungeonReady`→ ① `InGameModel.IsDungeonReady` ② `GameSessionConnector`가 `LoadSceneAsync(holdUntil)`로 **전원 입장까지 Loading 유지("다른 플레이어를 기다리는 중…") → 완료 시 Fader reveal**(30s 타임아웃 가드). 흐름 로그 全 hop. **E2E `SocketE2ETests` 8/8** + EditMode 릴레이 5/5 + `GameSessionConnectorTests` 3/3
- [x] **MPPM 2-client** "서로 보임·이동" 검증 — 선행 블로커 2건 ✅해소([mppm-testing.md](mppm-testing.md)): ① 서버 이미지 리빌드(E2E 8/8 그린) ② `MainLifetimeScope` DI 누락 수정. 데이터 선행 ✅ → **MPPM 2-창 시각 검증 완료(사람 조작, 2026-06-03)**. **M1 전체 완료.**

### 🔄 M2 — 전투 코어 (Character 두 축 리팩터 동반) [WBS 2.1·2.2·2.5.1·2.6.2]
선행: M1. 상세 기획: [effect-system.md](effect-system.md)

**Effect/버프 시스템 — 1단계 (클라 단독·sync-ready, M1 중에도 착수 가능)**
- [x] **EF-1a**: Attribute 재구조 — Resource vs Stat(`EAttributeKind`) 분리, `SetCurrent`(파생·정수 `GameplayEffectMath.Aggregate`). 즉발은 Resource만 영구 변경
- [x] **EF-1b**: `GameplayEffectDefinition`/`ActiveGameplayEffect`/`EEffectCategory`/`EDurationPolicy`/`EStackPolicy`/`GameplayEffectCatalog`(string id 시드) — 순수 데이터, Sprite 없음
- [x] **EF-1c**: ASC `ApplyEffect`/`RemoveEffect`/`Tick`(만료) + 추가/제거/만료 시 `RecalculateStats` → `OnActiveEffectsChanged`, `GetActiveEffectSnapshots`
- [x] **EF-1d**: `EffectIconCatalog`(Presentation SO·표시전용·카테고리→Sprite+polarity 색) + `BuffView` DTO + `InGameState.Buffs` + `InGameModel` 중계
- [x] **EF-1e**: `GameHud` 버프슬롯 풀 렌더 + `BattleEffectSlot.Bind`+로컬 카운트다운. EditMode 122/122 + PlayMode(슬롯 렌더·HP/MP) 통과
- [x] **EF-1f**: `EffectIconCatalog` 에셋(`GameData/Resources/Effects/`) — fg4_icons PSD 멀티스프라이트 + 버프/디버프 색. `DungeonLifetimeScope` Resources 폴백 자동 로드. **UI Toolkit 커스텀 인스펙터**

**Effect/버프 시스템 — 2단계 (M2 합류·서버 권위)**
- [x] **EF-2a**: `Shared.Gameplay`(netstandard2.1) 결정론 코어 — enums/`GameplayAttributeModifier`/`GameplayEffectMath.Aggregate`/`EffectTiming`. xUnit golden **9/9**(클라 EF-1 parity). sln 등록
- [x] **EF-2c (서버)**: `S_ApplyEffect`/`S_RemoveEffect` 패킷(`Shared.Packet`, Union **1640/1641**) — StartTick+EffectId만. SocketServer.Tests **11/11**
- [x] **EF-2b**: DLL 단일 소스 — `Shared.Gameplay.dll`을 `Client/Assets/Plugins/`에 배치, 클라 중복 순수 8개 삭제. DLL 타입을 클라와 **동일 ns**로 둬 클라 코드 수정 0. EditMode **122/122**
- [x] **EF-2c (클라)**: 패킷 미러 — `ClientCodegen` 재생성으로 클라 반영(`Network/Socket/Packets/EffectPacket.cs`). 회귀 122 통과
- 🔄 **EF-2d** (서버 전투 CA-1/3 선행 — 대부분 combat-gated):
  - [x] **ASC 서버-권위 적용 시드**: `ApplyEffectAuthoritative(def, instanceId, stacks)` — 서버 InstanceId 키·멱등 재적용·`RemoveEffect(serverId)`. EditMode 124/124.
  - [x] **서버 emit(테스트 등급)**: `CombatHandler.HandleAttack`(`C_Attack`→디버프 `S_ApplyEffect` 방 브로드캐스트). SocketServer.Tests **13/13**
  - [x] **수신 핸들러 배선(클라)**: `SocketEffectApply`+`Effect{Apply,Remove}PacketHandler` → `EffectReceiver`(카탈로그+`AuthSession` 라우팅). EditMode **127/127**
  - [x] **E2E 2-클라 검증**: `SocketE2ETests` — A `C_Attack`→`S_ApplyEffect`→B 수신 **1/1**
  - [ ] **남은 정밀화**: 공유 시계(서버 tick, StartTick 정밀 만료) + 클라 예측/정정 + 원격 캐릭터 ASC 라우팅 — CA-3 합류 시

**Character 리팩터 + 서버 권위 전투**
- [x] **CA-1**: 두 축 분리 — Action을 Locomotion FSM에서 들어냄. **`CharacterAgent` 무접촉**. EditMode **129/129**.
  - [x] **Attack (증분1)**: `AttackState`/`StateKind.Attack` 제거. 발동=`PlayerCharacterAgent.HandleAttackInput`(`ConsumeAttackPressed`→히트리셋+애니). 데미지는 기존 GAS 체인 유지. `LocomotionStateMachineTests` 추가.
  - [x] **Interact (증분2)**: `InteractState`/`StateKind.Interact` 제거. 발동=`HandleInteractInput`(`ConsumeInteractPressed`→`InteractionDetector.CurrentInteractable.Interact(gameObject)`). 실작동 구 Character-ns 경로 유지.
  - [x] **규칙갱신**: `.claude/rules/unity-gameplay-state.md` → "두 축 분리"로 갱신.
  - [ ] **별도 정리(후속)**: `Game.Gameplay.Input.InteractionSystem`(휴면 중복) 제거 또는 일원화 결정. 아이템 인벤토리(3.1) 합류 시 instigator 흐름 확정.
- [x] **CA-2**: `Shared.Gameplay`에 SkillTimeline 스키마 + 결정론 코어 — `SkillTimeline`·`SkillTimelineMath`·`HitboxMath.Overlaps`·`SkillCatalog`(`basic_swing`). 적중=`OnHitEffectIds`→GE→EF-2d `S_ApplyEffect` 재사용. xUnit **17/17**.
- ✅ **CA-3**: BasicAttack end-to-end — 서버 권위 판정 + GE 데미지, 클라 송신/연출/HitStop (정밀화는 §197 후속)
  - [x] **증분① 서버 권위 적중 판정**: `CombatHandler` 업그레이드 — `C_Attack`→Room 위치/yaw로 `SkillCatalog`+`HitboxMath.Overlaps` 적중 재계산(권위) → `basic_attack_dmg` `S_ApplyEffect` 브로드캐스트. SocketServer.Tests 15/15 + E2E 1/1.
  - [x] **증분②a 클라 데미지 적용**: 클라 `GameplayEffectCatalog`에 `basic_attack_dmg`(Instant Health -10) → `EffectReceiver` 수신 시 ASC Health 즉발 감소. EditMode **130/130**.
  - [x] **증분②b 인게임 송신+이중데미지 제거+HitStop**: 공격 시 **C_Attack 송신**(`CombatSyncSender`+`OnAttackPerformed`+`CharacterSpawner.AttachCombatSyncSender`) + 로컬 권위 데미지 제거(데미지는 서버 `S_ApplyEffect`만). **HitStop**=`HitStopController`(per-actor `Animator.speed`) → `PlayerCharacter.prefab` 루트 부착. EditMode **131/131**.
  - [x] **증분③ 2-client 공격·피격 동기화 검증**(E2E): 신선 Docker 서버 대상 `SocketE2ETests` **8/8 그린** — combat 케이스(`RawSocket_호스트가_정면의_게스트를_공격하면_서버권위_적중_S_ApplyEffect`: 호스트 정면 1유닛의 게스트 공격 → 서버 hitbox 판정 → 게스트 `S_ApplyEffect{basic_attack_dmg}` 수신) 포함. ※MPPM 2-창 시각 검증(HitStop 연출)은 사람 조작 영역.
  - [x] **증분④ 죽은 ability 클러스터 정리**(YAGNI): 서버 권위 이관으로 미사용·미부착이던 로컬 GAS *ability* 경로 삭제 — `CharacterHitEventReceiver`·`HitDetector`(+프리팹 `AttackPoint`)·`BasicAttackAbility`·`Ability`·`AbilityActivationContext`·`BasicAttackAbilityTests` + ASC ability 멤버. GAS *effect*(ASC.ApplyEffect·GameplayEffect·버프)는 유지. 컴파일 0 + EditMode **128/128**(ability 테스트 3개만 감소). codemap §2.7.

### M3 — 몬스터 [WBS 4.1]
선행: M2.

**설계 결정**: 몬스터 HP = **서버 권위**(플레이어 HP는 기존 클라 결정론 유지 — 의도된 비대칭, 몬스터는 서버 소유 NPC). 이동 = **서버 시뮬 + 클라 보간**(`RemoteDriver` 모델, 클라엔 몬스터 AI/물리 없음). **단일 `RoomTickService`**가 전 방 순회(몬스터 상태는 `Room` 동거), AI *수식*만 순수 `MonsterAiMath`로 분리(단위테스트). 스폰/패트롤/맵경계는 **Map Editor 저작 → spawn-layouts.json → 서버 파싱**(클라 런타임은 미사용 — 받은 위치에 인스턴스+보간만).

- [x] **① 패킷** — `S_SpawnMonster`(1810)/`S_MonsterState`(1811)/`S_MonsterDead`(1812) + Union + 클라 미러 재생성(codegen 자동) [4.1.1] — SocketServer.Tests 직렬화 **4/4**
- [x] **②a 데이터모델** — `MapDefinition.bounds`(MapBounds: centerX/Z·sizeX/Z) + `MonsterSpawn.patrolPoints`(List<Vector3>) + 공유 `MapBounds`(Clamp/Contains·무경계 가드)·`MonsterSpawnDef`(Patrol) + `MapDataExporter` DTO 확장(patrol/bounds, Bake+Import 양방향). ServerAll·Game.Gameplay·Editor 빌드 0오류
- [x] **②b 에디터** — `MonsterSpawnMarker`·`PatrolPointMarker`·`MapBoundsMarker`(저작 마커) + `MapEditorWindow` Add Monster/Patrol·Load·SaveAndExport write-back·기즈모 라벨. Game.Gameplay·Editor 빌드 0오류(※실저작은 Unity에서 사람이)
- [x] **②c 서버 파싱** — `MapSpawnLayout`(Bounds·Monsters) 확장 + `SpawnLayoutTable.Parse(Stream)` 공개·monsters/patrol/bounds 파싱 + `dungeon_01` JSON 시드(slime+4패트롤+40×40 경계, 클라/서버 양본). SocketServer.Tests **9/9**(파싱 합성JSON·clamp·임베디드) [4.1.2]
- [x] **③ 서버 상태/스폰** — `Server.Monster`(`MonsterState`+`MonsterPhase`·`MonsterCatalog`) + `Room` 몬스터 보유(`SpawnMonsters`/`GetAllMonsters`/`GetMonster`/`RemoveMonster`·`Bounds`) + `CreateRoom` 스폰 + `RoomJoinLeaveHandler` 입장 시 `S_SpawnMonster`×N 로스터. SocketServer.Tests **26/26** [4.1.2]
- [x] **④ 틱+AI(이동)** — `MonsterAiMath.Step`(순수: Chase/Attack/Patrol/Idle + **매 틱 bounds.Clamp**) + `Room.TickMonsters`(락 안 step→S_MonsterState 목록) + `RoomTickService`(BackgroundService 10Hz, Program.cs 등록). SocketServer.Tests **33/33**(AI 7). ※몬스터→플레이어 공격발동(쿨다운→`S_ApplyEffect`)은 ⑤에서 합류 [4.1.3]
- [x] **⑤ 피격/사망(플레이어→몬스터, GAS)** — `CombatEffectCatalog`(effectId→`GameplayAttributeModifier`) + `Room.DamageMonster`(`GameplayEffectMath.Aggregate`로 HP 차감·0이하 제거) + `CombatHandler.ApplyAttackToMonsters`(몬스터 hitbox 판정 → `S_MonsterState`/`S_MonsterDead`). SocketServer.Tests **38/38**(피격 5) [4.1.4]
  - [x] **⑤b 몬스터→플레이어 공격** — `MonsterAiMath.Step`이 aggro 타깃 인덱스 반환 → `Room.TickMonsters(dt, nowMs)`가 Attack 페이즈+쿨다운(`AttackCooldownMs`) 경과 시 최근접 플레이어에 `S_ApplyEffect{monster_attack_dmg}` 발행(`RoomTickService`가 nowMs 전달). 클라 `GameplayEffectCatalog`에 `monster_attack_dmg`(Instant Health -5). SocketServer.Tests **43/43**(공격 2). ※원격 피격자 HP는 `EffectReceiver` 로컬 라우팅만(원격 ASC 라우팅은 CA-3 후속 부채)
- [x] **⑥ 클라 렌더** — `MonsterPacketHandler` 3종(디스패처 자동매핑) + `ISocketPacketState` 몬스터 상태/이벤트 + `SocketMonsterSnapshot` + `MonsterSpawner`(IAsyncStartable, DI 등록) + `MonsterEntity`(`RemoteDriver`류 보간) + `CharacterPrefabSettings.MonsterPrefab`. Game.Network/Gameplay/VContainer/Tests.EditMode 빌드 0오류 + EditMode 릴레이 테스트 4개. ※**Unity에서 사람이**: 몬스터 프리팹(+`MonsterEntity`) 제작 → `DungeonLifetimeScope.monsterPrefab` 할당 → EditMode 실행/플레이 확인 [4.1.5]
- [x] **⑦ E2E(작성)** — `SocketE2ETests`에 몬스터 3종 추가: 입장→`S_SpawnMonster` 로스터 수신 / 반복 공격(최신 위치 재조준)→`S_MonsterDead` / 사거리 진입→`S_ApplyEffect{monster_attack_dmg}` 수신. `SocketPacketCollector.TryGetLatest` 추가. PlayMode 빌드 0오류 + 서버(⑤b) 재배포. ※실제 실행(Unity PlayMode, Docker 대상)은 대기

### ✅ M4 — 던전 루프 완성 (= DoD 달성) [WBS 2.3·4.2·6.2·7.1]
선행: M3. **DoD 달성 완료 (2026-06-07 MPPM 2-창 수동 검증 통과).** A 트랙(클리어 루프)·B 트랙(Exp 보상+실패+UI) 코드 + 1판 루프 시각 검증 모두 닫힘.

**전투 모델 결정 (2026-06-05) — 플레이어→몬스터 = 서버 권위, 트리거만 클라**
- **모델(= 기존 M3 ⑤ 유지)**: 클라 좌클릭 → `C_Attack{skillId}` 송신(트리거) → 서버 `CombatHandler`가 시전자 위치/yaw로 hitbox 재계산(권위 판정) → `Room.DamageMonster`(서버 HP·데미지 산정) → `S_MonsterState`/`S_MonsterDead` 브로드캐스트.
- **역할 분리**: 클라 = **트리거만**(어떤 몬스터/데미지 모름). 서버 = **판정·데미지·HP·전멸·브로드캐스트 전부 권위**.
- **후속(M5)**: 데미지 산식을 GAS 스탯(공격력/방어력) 기반으로 승격. 현재는 `CombatEffectCatalog` 고정값.
- **판단 기준**: 왜 서버 권위인가 = [authority-model.md](authority-model.md) 4축(치팅/일관성/반응성/결정론). **데미지 숫자 표시 = A안**(서버 응답값, 클라가 이전HP−새HP 델타로 플로팅 텍스트, 패킷 무변경 / 막타는 마지막 HP 근사). 연출만 입력 즉발.
- **확인 필요**: 현 서버 권위 경로(`ApplyAttackToMonsters`)가 **실제 플레이에서 동작하는지**(몬스터 콜라이더/위치 추적·hitbox 튜닝). 코드는 존재(M3 ⑤) → A 트랙은 *검증·배선*이지 신규 구축 아님.

**A 트랙 — 클리어 루프 골격** (보상 없이 먼저 관통 → DoD "모양"). DungeonId는 보상 산정 전제라 B로 미룸(A는 DB 스키마 변경 없이 관통).
- [x] **전투 검증(사용자 플레이)** — 클라 공격 체인은 **코드 완성·배선 확인됨**(좌클릭→`PressAttack`→`CombatSyncSender`→`C_Attack`→서버 hitbox→`DamageMonster`; `CharacterSpawner.cs:69`서 부착). 사용자 던전 플레이로 클리어 플로우+이동 검증 완료. **검증 중 발견 버그 수정(2026-06-07)**: ① 고fps에서 이동 교착 — `CharacterMotor` 속도 램프를 `controller.velocity` → `m_speed`(직전 의도속도) 기반으로 변경(첫 프레임 변위가 `CharacterController.minMoveDistance`보다 작아 velocity가 0에 묶이던 deadlock 해소) ② `DungeonClear` 패널을 `GameHud` 프리팹에 배선(`dungeonClearView` 할당) ③ `runInBackground=true`(포커스 상실 시 시뮬레이션 멈춤 방지) ④ `InputSystemIntegrationTests` 입력맵 Enable 누락 수정
- [x] **클리어 감지** — `Room.TryMarkCleared`(스폰됨 & 전멸 최초 1회) → `S_DungeonClear`(Union **1820**) 방 브로드캐스트 + `DungeonClearMessage{RoomId,MapId,Participants[]}` 발행(`IDungeonResultPublisher`→`DungeonResultMessageQueue`, `stream:game:dungeon:result`). `CombatHandler.ApplyAttackToMonsters`가 처치 후 발화. **SocketServer.Tests 47/47**(클리어 4 신규)
- [x] **DungeonResultConsumer** — `ResilientStreamConsumer`(§9.10) 위임 + `DungeonClearMessageQueue`(Consumer Group) + DI. A단계는 수신·로그만(보상 자리 `TODO(B)`)
- [x] **클라 결과→복귀** — codegen 미러(`S_DungeonClear`) → `DungeonClearPacketHandler`→`ISocketPacketState.OnDungeonCleared`→`InGameModel`→`InGameState.IsDungeonCleared`→`GameHud.dungeonClearPanel` 토글(미할당 무해)+기존 `ReturnToLobby` 재사용. **Unity 컴파일 0오류**. ※결과 패널 아트(GameHud 프리팹)는 Unity에서 사람이
- [x] **A 트랙 E2E** — `SocketE2ETests.RawSocket_몬스터_전멸하면_양쪽_S_DungeonClear_수신`: dungeon_01 슬라임 1마리 처치(재조준 루프 재사용) → 호스트+게스트 양쪽 `S_DungeonClear{RoomId}` 수신. **Docker 리빌드·재배포 후 PlayMode 실행 → SocketE2ETests 12/12 그린**(신규 클리어 1 + 회귀 11)

**B 트랙 — 보상 채우기** (Exp 전용 + 실패 경로 + UI)

> **범위 확정(2026-06-06)**: 보상 = **Exp만**(인벤토리 제외). 던전→Exp 매핑은 **Shared 카탈로그(MapId 키)** — DB DungeonId/`DungeonRoom.DungeonId` 도입 **안 함**(서버 간 직접 참조 금지·정적 기획데이터, MapId가 이미 DungeonClearMessage로 흐름). 실패 트리거 = **참가자 전원 다운**(`C_PlayerDead`→서버 집계→`S_DungeonFailed`). Progression은 별도 `user_progressions`(미래 캐릭터 귀속 대비). 상세 = [[character-swap-direction]].

- [x] **진행/성장(Exp) 도메인** [2.3] — `user_progressions` 테이블(users 1:1, Lv1/Exp/UpdatedAt)+엔티티 `AddExp`·`IProgressionRepository`/`Repository`(Cache-Aside+Delete, lazy get-or-create)·`IProgressionService`/`ProgressionService`·DI(UserInstaller)·RedisKeys. 단위 8 + Testcontainers 통합 6 그린. ※레벨업 산식·스탯 성장은 M5
- [x] **던전 Exp 카탈로그(Shared)** — `MapSpawnLayout.ExpReward` + `spawn-layouts.json`(양본) `expReward:100`(dungeon_01). SocketServer.Tests 7(파싱·임베디드)
- [x] **보상 산정·지급** — `DungeonResultConsumer`가 `SpawnLayoutTable.Get(MapId).ExpReward` → 참가자 전원 `ProgressionService.AddExp`. RoomId 멱등(Redis SET claim-first, at-most-once). `IConnectionMultiplexer`+`IServiceScopeFactory`. Testcontainers 통합 2(전원 지급·멱등) + **실 Redis Stream E2E 1**(발행→Consumer Group→DB Exp, `DungeonResultRewardE2ETests`) [4.2.3/4.2.4]
- [x] **클리어 팝업 Exp 표시** — `S_DungeonClear.RewardExp`(SocketServer가 카탈로그값 실음) → 클라 `MarkDungeonCleared(exp)`→`InGameState.RewardExp`→`GameHud`가 `DungeonClear` 패널 표시+`SetReward`. `DungeonClear.cs` Bind/SetReward, return 버튼→ReturnToLobby [7.1]
- [x] **실패 경로(전원 다운)** — `C_PlayerDead`(1822)/`S_DungeonFailed`(1821) 패킷 + `Room._outcome`(Interlocked, 클리어/실패 배타)+`TryMarkFailed`(전원 다운 1회) + `DungeonLifecycleHandler` + 클라 로컬HP0→`C_PlayerDead`송신·`DungeonFailed` 패널→ReturnToLobby. 단위(Room 6)+EditMode 4+E2E 1. ※클라 컴파일/PlayMode 실행·결과패널 아트는 Unity(사람)
- [x] **완전한 Co-op 1판 루프 E2E** (MPPM 2-client) → 로비 복귀 [6.2] — E2E 코드 작성(클리어 RewardExp·전원다운 실패) + **MPPM 2-창 수동 플레이로 클리어→보상→로비 복귀 전체 1판 시각 통과 확인(사람, 2026-06-07)**. 이동 교착 버그 수정 후 이동/서로보임도 동작 확인됨. **= M4 DoD 달성, M4 전체 완료.**

### 🔄 M5 — 폴리시 + PVE 맛보기 (현재 작업) [WBS 2.4·2.6·2.7·3.2~3.8·4.4~4.7·5.*·6.3~6.4·7.2~7.8·8.*]
> **✅ 직전 완료 (2026-06-13, T1 잔여 전부 닫힘)**:
> 1. **회복 수치 단일소스** — ✅ **완료(클라 컴파일 검증 통과 2026-06-13)**: 서버 bake JSON + 클라 `ConsumableCatalogSeeder`로 던전 회복 미러 회귀 복구 + Editor `ConsumableEffectExporter`. 교리 = [gas-architecture.md §2.5](gas-architecture.md), 상세 codemap §2.6c. **Unity 컴파일 0오류 확인 완료** — 잔여 검증 없음.
> 2. **4.6.3 Main 획득 B-lite 서버 검증 (T1)** — ✅ **완료(플레이 검증 2026-06-13)**: 서버 374 그린 + PlayMode E2E + 플레이(슬롯 스폰→킬→`ClaimKill` 서버roll 지급→5s 재스폰, 쿨다운 파밍차단). 무한파밍 핵 차단(`GrantItem` 제거). 저작=`MapDefinition` SO→bake. 설계 = [main-spawn-claim.md](main-spawn-claim.md).
> 3. **2.5.1 사망/리스폰** — ✅ **완료(플레이 검증 2026-06-13)**: Main 타이머 리스폰(`LocalRespawnController`)+`LocalMonster` 근접공격(사망 트리거)+다운→3s 부활. 다운포즈 Animator 클립=클라 발전 시 보류(로그 대체). 던전 다운잠금·서버권위 HP 기존 완료. 던전 내 부활=2.5.2(T2).
>
> **🟢 현재 트랙 (2026-06-13~): 서버 도메인 완성** — RPG 코어 서버 기능을 의존성 순서로 마무리한다. 우선순위(의존성 정렬):
> 1. **2.3 레벨업/스탯 성장** — ✅ **완료(서버 28 + Unity 컴파일 0오류 + PlayMode E2E `ProgressionE2ETests` 2(Docker) + 전체 PlayMode 68/68, 2026-06-14)**. ① 데이터 = `LevelTableDefinition` SO(클라, 1~60) → `LevelTableExporter` bake → `Shared.Infrastructure.Progression.LevelTable`(임베디드 `level-table.json`, 거듭제곱 Exp `round(100·L^1.5)` + 레벨별 스탯 룩업). ② 도메인 = `UserProgression.AddExp(amount, ILevelCurve)` 레벨업 루프(remainder 이월·60만렙 고정), `ILevelCurve`(Domain) ↔ `LevelTableCurve`(Infra, LevelTable 위임). **DB는 Level/Exp만 영속(마이그레이션 0), 스탯은 레벨 룩업 파생=단일소스.** ③ 클라 노출 = `progression.proto` GetProgression(레벨/Exp/expToNext/스탯, userId=JWT) + `ProgressionGrpcService`(서버, LevelTable 룩업 합성) + Generated/Network 래퍼(ClientCodegen 재생성) + System `IProgressionService`(proto 은닉) + Presentation `ProgressionModel`(MVI pull, Main·Dungeon 스코프 등록). **검증: 서버 — 전체 솔루션 빌드 0오류 + 진행 테스트 28 그린(LevelTable 7·UserProgression 8·Service 3·gRPC 3·통합 7). 클라 — 생성 래퍼/인터페이스가 수기 코드와 일치 확인, 컴파일은 Inventory 동일 패턴 미러. ⚠️ Unity 클라 컴파일 최종 확인은 대기**(force refresh 후 Unity 브리지 도메인리로드 중 무응답 — 포커스 후 재시도 필요). **잔여 = 스탯창 prefab 비주얼 저작(7.3, 사용자 Unity) + Unity 컴파일 확정.**
> 2. **2.4 스탯 산식** — 레벨/장비/버프 합산 서버 권위 재계산. 2.3 + 3.2 합류 지점.
> 3. **3.2 장비(Equipment)** — 착용 슬롯 + 스탯 모디파이어 → 2.4 합산. (인벤토리 3.1 위에 적층)
> 4. **3.4 재화(Wallet)** — 골드 보유·증감(서버 권위). 상점 전제.
> 5. **3.5 상점(Shop)** — 구매/판매·가격·재고. 3.4 + 3.1 소비.
> 6. **3.7 아이템 등급/도감** · **6.1 캐릭터 진행 영속 합류**(2.3/3.1/3.2 DB 통합).
> 7. 부채 정리: **9.6 GetRooms 페이징**(proto 변경 동반 — 승인 후) · **4.3/9.2 DungeonId**(다중 던전 UI 합류 시).
>
> **✅ 완료(2026-06-14): 몬스터 카탈로그(SO) + Main 킬 Exp 보상** — ① 몬스터 정의를 SO 저작 카탈로그로 승격(`MonsterCatalogDefinition`→bake `monsters.json`→`Shared.Infrastructure.Monsters.MonsterCatalog`, 하드코딩 `Server.Monster.MonsterCatalog` 어댑터화). exp/스탯은 몬스터 정의에 — 스포너는 위치/슬롯만(비대 X). ② Main 처치 = **킬 즉시 exp(`ClaimMonsterExp`)** + 아이템은 줍기(`ClaimKill`) — exp/아이템 **독립 청구·독립 쿨다운**(사용자 결정: "쓰러트리면 즉시 exp, 아이템은 오브 줍기 유지"). 클라 킬 즉시 `MainMonsterSpawner`→exp 로그. **검증 완료: Shared 34·SocketServer 101·GameServer 전체 278(통합+E2E) + PlayMode `MainLootE2ETests` 통과 + 플레이 확인(킬 즉시 exp 로그, 사용자 2026-06-14).** 상세 codemap §2.24.
>
> **✅ 완료(2026-06-14): Main 클라 스탯 반영 + 레벨/exp 로그** — Main(클라 로컬 전투)이 현재 레벨 스탯을 반영. 2.4(던전=서버권위 스탯전투)의 **Main 대응판**(클라 권위 로컬). 이전: `LocalCombat` 데미지 고정 10(레벨/AttackPower 무관) → 이제 레벨업하면 다음 스윙부터 강해짐.
>   - ① **클라 스탯 홀더** = `PlayerProgressionHolder`(`Game.System.Progression`, **Gameplay 가 Presentation 미참조라 System 에 둠** — ProgressionModel(MVI)과 별개). `GetProgression`(서버권위) 결과 캐시(`Current`: Level/Exp/ExpToNext/AttackPower/Defense). `IAsyncStartable.StartAsync`로 **Main 진입(로그인 직후) 1회** + 킬마다 `RefreshAsync`. MainLifetimeScope `RegisterEntryPoint(...).AsSelf()`(던전 미등록=서버 권위).
>   - ② **킬 후 레벨/exp 로그** = `MainMonsterSpawner.ClaimExpAsync` 성공 → `holder.RefreshAsync` → `[Progression] 현재 Lv N · Exp X/Y (다음까지 Z)`(만렙=`(만렙)`). 서버 GetProgression 이 단일 진실.
>   - ③ **LocalCombat 데미지 = AttackPower 기반** = 고정 10 → `StatCombatMath.MeleeDamage(BaseDamage 10, holder.AttackPower, 0)`(Shared 결정론, 던전과 동일 산식). 홀더 미갱신 시 AP=0 → base 그대로 폴백. LocalCombat 은 `[Inject] Construct` 메서드 주입, `CharacterSpawner.AttachLocalCombat`서 `AddComponent` 후 `_container.Inject(combat)`.
>   - **DLL 재배치 필수였음**: `StatCombatMath`(2.4 추가)가 클라 `Plugins/Shared.Gameplay.dll`(stale)에 미포함 → 컴파일 실패. `Shared.Gameplay` Release 재빌드 → `Client/Assets/Plugins/Shared.Gameplay/`에 재복사(공개 API 변경이라 필수, codemap §공유코어 단일소스 패턴).
>   - **검증**: 변경 어셈블리 dotnet build 0오류(Game.System·Game.Gameplay·Game.VContainer). ⚠️ **Unity 에디터 재임포트(새 dll meta)+플레이 확인은 사용자**. (Game.Input.csproj CS2001은 이동된 Input 파일 stale 생성참조 — 본 작업 무관.)
>   - 비고: 스탯 진실원=서버(GetProgression). 클라는 표시·로컬전투 적용만. 장비/버프(3.2) 합류 시 `GetStatsAsync` 합산이 그대로 흘러옴. 정식 ASC 어트리뷰트 연동(GAS)은 선택(지금은 홀더로 충분, YAGNI).
>   - **후속(2026-06-14): HUD exp 게이지 연결** — GameHud 에 추가된 `expSlider`+`expValue` 를 `holder.OnChanged → InGameModel(ExpChanged) → InGameState → GameHud.RenderExp` 로 배선(HP/MP·버프와 동일 MVI 경로, ProgressionModel 직접주입 X). 던전 HUD 표시 위해 홀더를 DungeonLifetimeScope 에도 등록(표시 전용). EditMode `InGameExpRelayTests` 2/2 그린. 상세 codemap §2.25b.
- [ ] 애니메이션(MotionMatching V2 액션 블렌딩, 🟣)·HUD 다듬기·스킬1~2·아이템 최소·사운드(8.*)
- [ ] 장비/루트/재화/상점/소모품 [3.2~3.8] + 관련 UI [7.2~7.8]
- [ ] 전투 보조(회피·CC·타겟팅·Co-op 부활) [2.6·2.5.2]
- [ ] **CA-5**: Skill Timeline 에디터 툴(공유 JSON read/write) [2.7]
- [ ] **던전 메타: `DungeonRoom.DungeonId` 도입** [4.3·9.2] — 다중 던전/던전 선택 UI 합류 시. 엔티티 4곳(Clone/FromRedis/ParseFromRedis/ToHashEntry)+EF 마이그레이션+Redis Hash 스키마. (M4까지는 MapId 카탈로그로 우회)
- [ ] PVE 오픈월드 맛보기 — 월드/존·퀘스트·NPC/대화·상호작용 [4.4~4.7]
- [ ] 소셜(핑/이모트)·설정/옵션·재접속 [5.2·6.3·6.4]

### M6 — 마감 [WBS 9.8·9.9]
- [ ] 데모 영상/gif·부하/E2E 검증·배포/포트폴리오 문서

---

## ✅ 완료된 Phase (= WBS 1.* 상세)

| Phase | 내용 |
|-------|------|
| 서버 인프라 | Clean Architecture, JWT 인증, DeviceId Binding, Token Rotation |
| 던전 로비 | gRPC 방 CRUD, SubscribeRoom Streaming |
| 게임 시작 E2E | Outbox → Redis Stream → SocketServer 방 생성 → IP:Port 알림 |
| SocketServer | C_Auth, C_PlayerJoin, C_Move/S_Move, Ping/Pong |
| 채팅 | Redis Streams, Global/Room/Whisper |
| 분산 로그 | Serilog + Graylog, TraceId 전파 |
| DB/캐시 | PostgreSQL + Redis Cache-Aside, Testcontainers 통합 테스트 |
| 클라이언트 OutGame | gRPC 로그인/로비 UI, VContainer DI, MVI 아키텍처 |

---

## 참고 파일

- **일정/이슈 트래킹**: GitHub Project [Multiplay ActionRPG Roadmap](https://github.com/users/SeoBYP/projects/2) — WBS 노드 56개가 Issue로 등록됨(필드 Status·Tier·Owner·Milestone). **plan.md = 설계·이력 진실원 / Project = 일정·진척 뷰** (역할 분리).
- 코드맵 + 설계 결정 로그: [`docs/wiki/codemap.md`](codemap.md)
- 패킷 규칙: [`docs/wiki/packets.md`](packets.md)
- SocketServer 규칙: [`docs/wiki/socketserver.md`](socketserver.md)
- 서버 흐름: [`docs/wiki/gameflow.md`](gameflow.md)
- GAS 아키텍처(Tag·Effect·Ability·Cue·발동권위): [`docs/wiki/gas-architecture.md`](gas-architecture.md)
- Effect/버프 시스템: [`docs/wiki/effect-system.md`](effect-system.md)

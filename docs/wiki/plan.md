# 작업 플랜

> 새 채팅이면 이 파일부터 읽는다. 상태가 바뀌면 그 자리에서 갱신한다(체크박스·WBS 마커).
> **이 파일엔 "지금·다음"만 둔다.** 지난 이력은 `git log` 와 [codemap.md](codemap.md) §2(결정 로그)에 있다.
> 마지막 갱신: 2026-10-15 — 보안·데이터 무결성 결함 트랙 착수(RED 테스트 19건) · 문서 정리.

---

## 🔴 현재 작업 — 보안·데이터 무결성 결함 수정 (TDD)

2026-10-14 서버·클라 전수 리뷰에서 나온 결함 중 **클라 하나로 악용되거나 데이터가 사라지는 것**부터 고친다.
위치·재현 시나리오·수정 가이드는 [cleanup-backlog.md](cleanup-backlog.md) §P0.

**작업 방식 (2026-10-15 합의)**: 테스트·문서 = Claude 작성 / **프로덕션 코드 = 사용자가 직접 작성**, Claude 는 방향 가이드.
한 항목씩 `RED 확인 → 사용자 구현 → GREEN → 전체 스위트` 순서로 닫는다.

**RED 실측 (2026-10-15)**: GameServer `실패 9 / 통과 458` · SocketServer `실패 10 / 통과 308`. 실패는 전부 신규 테스트, 기존 457·307 은 그린 유지.

순서 = 싸고 효과 큰 것부터. `RED 테스트` 클래스가 전부 그린이 되면 체크한다.

- [ ] **G1** GameServer 기동마다 Redis `FlushAllDatabases()` — `RedisStartupPreservationTests` — 🔄 xUnit GREEN(2026-10-27), Docker 리빌드 후 E2E 남음
- [ ] **S1** 플레이어가 몬스터·테스트 어빌리티 발동(105 = 데미지 9999) — `AttackAbilityTrustTests` — 🔄 기준 확정(2026-10-27): 발동자는 세션으로 확정(패킷에 ID 추가 안 함), `Gas.HasAbility(abilityId)` 로 보유 검사. 보유 = 기본기 상수 + `PlayerInfo.AbilityIds`(아래 스킬 장착)
- [ ] **S3** `C_Move` 무검증 — NaN·순간이동, NaN 이면 거리 검사 전부 통과 — `MoveInputValidationTests` — 최대 이동 속도 결정
- [ ] **G3** 지갑·인벤토리·Exp lost update / 이중 차감 통과 — `EconomyConcurrencyTests`
- [ ] **G2** 퀘스트 보상 동시 요청 시 중복 지급 — `QuestClaimConcurrencyTests`
- [ ] **G4** 착용 장비를 팔아도 스탯 유지 — `EquippedItemOwnershipTests` — ❓ 판매 거부 vs 조회 시 필터
- [ ] **S2** 입장한 연결이 다른 UserId 로 재-Join → 남의 자리 탈취 — `PlayerJoinIdentityTests` — ❓ 새 연결 경로는 join 토큰(공개 계약)

**GREEN 판정 명령**

```bash
dotnet test ServerAll/GameServer/GameServer.Tests/GameServer.Tests.csproj
dotnet test ServerAll/SocketServer/SocketServer.Tests/SocketServer.Tests.csproj
```

- 통합 테스트는 Testcontainers 다 — Docker Desktop 이 막 뜬 직후엔 컨테이너 기동 실패로 수백 건이 한꺼번에 빨개진다(2026-10-14 실측 144건). 재실행해서 확인한다.
- G3·G2 는 경합 재현 테스트라 **고치기 전엔 드물게 우연히 통과**할 수 있다. 고친 뒤엔 항상 통과해야 정상이다.
- G1 은 고친 뒤 **Docker E2E 전체**를 돌린다 — 기동 시 FLUSH 에 기대던 E2E 가 있을 수 있다(미실측).

**다음 묶음 (P1 — 정상 플레이 중 고착·유실)**: S4·S5·G5·C1·C2·C3·C4. 착수 전에 RED 테스트부터 만든다. 목록 = cleanup-backlog §P1.

### 스킬 트리 · 장착 (S1 의 연장)

```
기본기(basic_swing·heavy_swing·combo_a~d) → 모든 플레이어 상시 보유, 서버 상수, DB 없음
트리 스킬 → user_skills(배움) / user_skill_slots(장착) → StartGameAsync 가 PlayerInfo.AbilityIds 로 스냅샷 전달
```

결정(2026-10-27): **슬롯 4개** · **던전 안 장착 변경은 거부**(유저의 방이 Starting/Playing 이면 Equip/Unequip 거부 — 로드아웃은 시작 시점 스냅샷이라 SocketServer 는 DB 를 모른다). 미결: 포인트 규칙(레벨당 포인트·노드 비용).

- [ ] Phase 1 — ASC `GrantAbility`/`HasAbility` + `ActorStore.AddPlayer(abilityIds)` + 기본기 상수 + `PlayerInfo.AbilityIds`(빈 목록 = 기본기만) → S1 GREEN
- [ ] Phase 2 — Entity·Migration(`user_skills` PK(UserId,AbilityId) / `user_skill_slots` PK(UserId,Slot) + UNIQUE(UserId,AbilityId))·Repository·`SkillService` Learn/Equip/Unequip. RED 먼저: 동시 학습 포인트 초과 · 미학습 장착 · 같은 스킬 두 슬롯 · 슬롯 범위(0~3) · 던전 중 변경 거부
- [ ] Phase 3 — `skill.proto`(공개 계약, 승인 후) + 클라 Generated 재생성 + 트리·슬롯 UI · 슬롯 키 → 장착 스킬 networkId 로 `C_Attack`

---

## 🎯 최종 목표

**DoD**: 2명이 접속 → 로비에서 방 생성·시작 → 던전 입장 → 몬스터 협력 처치 → 클리어 → 보상 → 로비 복귀. 전 과정 서버 권위 + E2E + 데모 영상.
**DoD 는 M4 에서 달성됐다(2026-06-07).** 지금은 폴리시·콘텐츠(M5)와 마감(M6) 단계.

**범위 제외 (2026-06-03 결정)**: 던전 난이도/시즌 · 가챠 · 우편함.

| 마일스톤 | 목표 | 상태 |
|---|---|---|
| M0 | 인증·로비·채팅·소켓·DB/캐시 기반 | ✅ |
| M1 | 인게임 진입 — 스폰·이동·원격 보간 | ✅ |
| M2 | 전투 코어 — 두 축 분리(GAS) + 서버 권위 Attack/Hit/Damage | 🔄 정밀화만 남음 |
| M3 | 몬스터 — 스폰·AI·양방향 전투 | ✅ |
| M4 | 던전 루프 — 클리어 → 보상 → 로비 복귀 (= DoD) | ✅ |
| M5 | 폴리시·콘텐츠 + PVE 맛보기 | 🔄 |
| M6 | 마감 — 데모·부하/E2E·배포·포트폴리오 문서 | ⬜ |

## 마일스톤 잔여 작업

**M2**
- [ ] 전투 정밀화 — 공유 시계(서버 tick 기반 만료) · 클라 예측/정정 · 원격 캐릭터 ASC 라우팅

**M5**
- [ ] 정적 기획데이터 단일 저작 — 서버 완료, **클라 잔여 4건**: ① `ItemDisplayCatalog`+`ConsumableCatalog` → 단일 아이템 SO + `ItemCatalogExporter`(모달 없는 `BakeAll`) ② 클라 중복 enum(`ItemGrade`·`ShopCategory`×2) → `Shared.Gameplay` 타입으로 ③ SO↔bake 드리프트 EditMode 테스트 ④ Docker 리빌드 후 E2E
- [ ] 아이템 숫자 id 2단계 — DB 3테이블 마이그레이션 · Redis Hash field · proto 5파일 `string item_id`→`int32` · `LootPackets` · 클라
- [ ] CA-5 Phase 1b — 어빌리티 타임라인에 실제 SFX/VFX 에셋 배선 (사용자 저작 작업)
- [ ] dungeon_03~05 실제 환경 아트 — 지금은 `Plane.prefab` 임시 배정(빈 공간 방지만 됨)
- [ ] 애니 튜닝 — ARPGWarrior 전환 페이즈는 전부 닫힘, 남은 건 폴리시
- [ ] PVE 오픈월드 맛보기 [4.6]
- [ ] 핑/이모트 · 설정/옵션 · 재접속 마무리 [5.2·6.3·6.4]

**M6**
- [ ] 데모 영상/gif · 부하 + 전체 E2E 회귀 자동화 [9.8] · 배포 문서 + 포트폴리오 마감 [9.9]

---

## ⚠ 재발 방지 메모 (실제로 밟은 함정)

- **스킬·콤보 타이밍의 진실원 = SkillTimeline**(SO 저작 → bake). 서버 cadence 와 클라 선입력이 같은 값을 본다. 프리팹 매직넘버·서버 하드코딩 금지.
- **Animator: Trigger + `hasExitTime` = 트리거 소실.** 체인 전이는 `hasExitTime=false`. 원격 연출엔 AnyState 안전망.
- **VContainer 는 C# 기본값 인자를 채우지 않는다** — 모든 생성자 인자를 해석하려다 루트가 통째로 죽는다(`No such registration of type: System.Double`).
- **`Shared.Gameplay` 를 고치면 `dotnet build` 가 `Client/Assets/Plugins/` DLL 로 자동 복사**한다. 공개 API 를 바꾸면 클라 컴파일을 Unity 에서 확인.
- **로컬 `dotnet build` 는 미커밋 파일 때문에 항상 통과할 수 있다.** 커밋본 검증 = `git archive HEAD ServerAll | tar -x` 후 빌드.
- **E2E 전 Docker 이미지 신선도 확인** — 옛 이미지면 전부 타임아웃(거짓 실패).
- **E2E 는 패킷 수신까지만 본다** — 애니 재생 같은 연출은 실프리팹 Animator 가드 테스트로 따로 잡는다.
- **테스트가 그린이어도 악의적·동시 요청은 검증되지 않는다**(2026-10-14 리뷰에서 결함 30여 건). 신뢰 경계·경합 테스트를 기능과 함께 만든다.

---

## 🌳 전체 범위 — WBS 트리

> **🔗 GitHub Project 자동 동기화**: 이 섹션의 노드 줄 상태 마커(✅/🔄/⬜)를 바꿔 커밋하면 post-commit 훅이 [Project #2](https://github.com/users/SeoBYP/projects/2)에 반영한다(`.claude/scripts/sync-github-project.py`).
> **형식 유지 필수**: 한 줄 = 한 노드, `- **x.y** 이름 — 상태 | Tier | Owner`. 새 `x.y` 노드를 추가하면 **GitHub 이슈가 자동 생성**된다. 그룹 1 은 동기화 제외.
> 범례 — 상태 `✅`완료 `🔄`진행 `⬜`미착수 · Tier `T1` 코어 `T2` 확장 `T3` 선택 · Owner 🟢 서버 · 🔵 GAS/전투 · 🟣 애니 · ⚪ 미배정

### 1. 기반 시스템 — ✅ 완료 (M0/M1)
- 1.1 인증 · 1.2 유저/프로필 · 1.3 로비/매칭 · 1.4 채팅 · 1.5 게임 세션 연결 · 1.6 인게임 진입 · 1.7 인프라(PostgreSQL·Redis·Docker·Graylog·Testcontainers) · 1.8 GAS 기반

### 2. 캐릭터 시스템
- **2.1** 전투 코어 — 서버 권위 Attack/Hit/Damage — 🔄 | T1 | 🔵 (잔여 = M2 정밀화)
- **2.2** 스킬/어빌리티 — Ability SO 저작 → bake → 서버 검증 — 🔄 | T1 | 🔵
- **2.3** 진행/성장(Progression) — 레벨·Exp·스탯 테이블 — ✅ | T1 | 🟢
- **2.4** 스탯 산식 — 레벨/장비/버프 합산 서버 권위 — 🔄 | T2 | 🟢 (버프 합산 미완)
- **2.5 사망/부활** — ✅ | T1 | 🔵
  - 2.5.1 사망 — 던전 다운 잠금 / Main 타이머 리스폰 — ✅ · 2.5.2 Co-op 부활 — ✅
- **2.6 전투 보조** — ✅ | T2 | 🔵
  - 2.6.1 회피(i-frame) — ✅ · 2.6.2 CC(스턴·슬로우·넉백) — ✅ · 2.6.3 락온 — ✅
- **2.7** 스킬 트리/습득·강화 — ⬜ | T3 | ⚪
- **2.8** 직업/클래스 — ⬜ | T3 | ⚪

### 3. 아이템 / 경제 시스템
- **3.1** 인벤토리(Inventory) — 스택형 소유, 서버 권위 영속 — ✅ | T1 | 🟢
- **3.2** 장비(Equipment) — Weapon/Armor 슬롯 · 스탯 합산 — ✅ | T2 | 🟢 (⚠ 결함 G4)
- **3.3** 루트/드랍(Loot) — 던전 드랍·줍기 + Main 경로 — ✅ | T2 | 🟢
- **3.4** 재화(Wallet) — 골드 서버 권위 — ✅ | T2 | 🟢 (⚠ 결함 G3)
- **3.5** 상점(Shop) — 구매/판매 — ✅ | T2 | 🟢
- **3.6** 강화/크래프팅 — ⬜ | T3 | ⚪
- **3.7** 아이템 등급/레어도 + 도감 — ✅ | T2 | 🟢 (도감은 보류)
- **3.8** 소모품/포션 — 인벤토리 소비 → GAS 효과 — ✅ | T2 | ⚪

### 4. 콘텐츠 시스템
- **4.1 몬스터** — ✅ | T1 | ⚪
  - 4.1.1~4.1.5 패킷·스폰·AI·양방향 전투·클라 렌더 — ✅ · 4.1.6 웨이브/스폰 페이즈 — ⬜ (착수 시 [spawn-system-evolution.md](spawn-system-evolution.md))
- **4.2 던전 클리어/보상(DungeonResult)** — ✅ | T1 | 🟢
- **4.3** 던전 메타 — `DungeonRoom.MapId` + 던전 선택 UI — ✅ | T1 | 🟢
- **4.4** 퀘스트(Quest) — 수주/진행/보상 — ✅ | T2 | 🟢 (DB 전용·캐시 없음 / Claimed 선마킹 후 지급 = 지급 실패해도 재수령 불가 — codemap §2.32 · ⚠ 결함 G2)
- **4.5** NPC/대화(Dialogue) — 대화 트리 + 그래프 저작툴 + 퀘스트 연동 — ✅ | T2 | 🟢
- **4.6** 월드/존(World) — 오픈월드 PVE 맛보기 — ⬜ | T2 | ⚪ (서버 권위 Main 몬스터는 Main 이 네트워크 World 세션이어야 성립)
  - 4.6.1 존 전환·포탈 — ⬜ · 4.6.2 패스트트래블 — ⬜ · 4.6.3 Main 몬스터/드랍(클라 시뮬 + 서버 검증) — ✅
- **4.7** 상호작용 오브젝트 — 🔄 | T2 | ⚪
  - 4.7.1 문/상자/채집 — 🔄 · 4.7.2 함정/기믹 — ⬜
- **4.8** 보스/특수 몬스터 — ⬜ | T3 | ⚪

### 5. 소셜 / 멀티플레이
- **5.1** 파티 — 방=파티 + 파티 HP HUD — 🔄 | T2 | ⚪
- **5.2** 인게임 핑/이모트 — ⬜ | T2 | ⚪
- **5.3** 플레이어 간 거래(Trade) — ⬜ | T3 | ⚪
- **5.4** 길드/클랜 — ⬜ | T3 | ⚪
- **5.5** 랭킹/리더보드 — ⬜ | T3 | ⚪
- **5.6** 친구/소셜 — ⬜ | T3 | ⚪

### 6. 메타 / 영속
- **6.1** 캐릭터 진행 영속 — 레벨·인벤토리·장비·지갑 — ✅ | T1 | 🟢
- **6.2** 던전 결과 → 로비 복귀 — ✅ | T1 | ⚪
- **6.3** 설정/옵션 — ⬜ | T2 | ⚪
- **6.4** 재접속/세션 복구 — 60s 유예 + 재접속 인수 — 🔄 | T2 | 🟢
- **6.5** 통계/플레이 기록 — ⬜ | T3 | ⚪
- **6.6** 도전과제/업적 — ⬜ | T3 | ⚪
- **6.7** 튜토리얼/온보딩 — ⬜ | T3 | ⚪

### 7. UI / UX
- **7.1** 결과/보상 화면 — ✅ | T1 | 🟢
- **7.2** 인벤토리/장비 UI — 🔄 | T2 | ⚪
- **7.3** 캐릭터 정보/스탯창 — ✅ | T2 | 🟢
- **7.4** 퀘스트 UI/추적 HUD — ✅ | T2 | 🟢
- **7.5** 대화 UI — ⬜ | T2 | ⚪
- **7.6** 상점 UI — 구매·판매 — ✅ | T2 | 🟢
- **7.7** 미니맵 HUD — ⬜ | T2 | ⚪
- **7.8** 설정 메뉴 — ⬜ | T2 | ⚪

### 8. 오디오
- **8.1** BGM/환경음 — ⬜ | T3 | ⚪
- **8.2** 전투 SFX/타격감 — ⬜ | T2 | ⚪
- **8.3** UI SFX — ⬜ | T3 | ⚪

### 9. 인프라 / 품질 / 기술 부채
- **9.1** SocketServer IP 하드코딩 → appsettings.json — ✅ | 높음 | 🟢
- **9.2** `DungeonRoom.DungeonId` 부재 (= 4.3) — ✅ | 중간 | 🟢
- **9.3** Auth: 로그인 시 이전 세션 강제 만료 — ✅ | 중간 | 🟢
- **9.4** `Room.Leave` 시 플레이어 상태 정리 누락 — ✅ | 낮음 | ⚪
- **9.5** Redis Consumer name 고정 → 머신명 기반 — ✅ | 낮음 | ⚪
- **9.6** `GetRooms` count/페이징 정책 — ✅ | 낮음 | 🟢
- **9.7** status.md stale → plan.md 일원화 — ✅ | 낮음 | 🟢
- **9.8** 부하 테스트 + 전체 E2E 회귀 자동화 — ⬜ | 마감 | ⚪
- **9.9** 배포 문서 + 포트폴리오 챕터 마감 — ⬜ | 마감 | ⚪
- **9.10** 컨슈머 복원력 — `ResilientStreamConsumer` 중앙화 — ✅ | 높음 | 🟢

> 결함·정리 대상은 WBS 노드로 만들지 않는다(이슈가 자동 생성되므로). [cleanup-backlog.md](cleanup-backlog.md) 가 관리한다.

---

## 참고 파일

- 결함·정리 대상: [cleanup-backlog.md](cleanup-backlog.md)
- 코드 위치 + 설계 결정 로그: [codemap.md](codemap.md)
- 권위 모델(클라 vs 서버): [authority-model.md](authority-model.md) · GAS: [gas-architecture.md](gas-architecture.md)
- 패킷: [packets.md](packets.md) · SocketServer: [socketserver.md](socketserver.md) · 서버 흐름: [gameflow.md](gameflow.md)
- 멀티플레이 테스트: [mppm-testing.md](mppm-testing.md)
- 일정 뷰: GitHub Project [#2](https://github.com/users/SeoBYP/projects/2) (진실원은 이 파일)

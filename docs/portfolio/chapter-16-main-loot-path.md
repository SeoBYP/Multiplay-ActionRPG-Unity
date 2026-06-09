# 챕터 16 학습 로그 — Main 싱글 루트 경로 (클라 권위 + 데이터 주도 드랍)

> 3.3 B. 챕터 15(던전 = 서버 권위 루트)의 후속. **Main(싱글 PVE)** 에서 몬스터를 잡으면
> 클라가 로컬로 드랍을 판정하고, 주우면 `GrantItem` gRPC 한 번으로 서버 인벤토리에 영속된다.
> 핵심 = **권위 비대칭**(던전 서버권위 vs Main 클라권위+서버 가드)을 한 인벤토리 도메인으로 수렴시킨 것.

## 한눈에 — 두 경로의 차이

```
던전 (Co-op·서버권위)                         Main (싱글·클라권위)
──────────────────────                        ──────────────────────
몬스터 sim/사망  : SocketServer                몬스터 sim/사망  : Client(LocalMonster)
드랍 roll        : SocketServer                드랍 roll        : Client(같은 DropTableRoll)
바닥/줍기 중재   : SocketServer(경쟁)          바닥/줍기        : Client(경쟁 없음=싱글)
지급(영속)       : GameServer ← Redis Stream   지급(영속)       : GameServer ← gRPC GrantItem
                   (SocketServer 발행)                            (Client 직접 호출 + 서버 가드)
```

**공통점은 지급(GameServer `GrantItemAsync` → Create/Update)뿐.** 그 앞단(누가 드랍을 판정·중재하나)은 완전히 다르다. 던전은 치팅·일관성 때문에 서버 권위 0클라신뢰, Main은 싱글이라 클라 권위를 수용하되 **영속 지급만 서버 경계로 가드**한다.

---

## 설계 결정과 근거

### 1. Main 지급은 왜 클라 신뢰 + 서버 가드인가

던전은 동시 접속·경쟁·치팅 방지가 필수라 SocketServer가 모든 걸 판정한다. Main은 **싱글 PVE** — 동기화할 상대도, 경쟁할 상대도 없다. 여기서까지 SocketServer를 거치게 하면(네트워크 World) 과설계다. 그래서 Main은 **클라가 로컬에서 다 끝내고, 줍은 순간 GameServer에 gRPC 1번**만 친다.

위조 가능성(클라가 가짜 아이템 요청)은 인지하되, **싱글 구간 + 서버 가드**로 수용한다:

```
신규 rpc GrantItem(item_id, qty) → GrantItemResponse{result, new_quantity}
  가드 3겹:
   ① 인증     = AuthInterceptor 가 [AllowAnonymous] 없는 모든 RPC에 JWT 검증 자동 적용
   ② 수량 상한 = InventoryGrpcService.MaxGrantPerCall=99 (gRPC 진입점에만)
   ③ 정의 검증 = GrantItemAsync 가 ItemCatalog.Get(itemId)==null 이면 실패
```

**가드 배치의 핵심**: 수량 상한을 **gRPC 진입점에만** 두고 도메인 `GrantItemAsync`엔 넣지 않았다. 던전 서버권위 경로(`LootGrantConsumer`)도 같은 `GrantItemAsync`를 호출하는데, 거기에 cap을 걸면 정당한 대량 지급이 막힌다. "신뢰 못 하는 클라 진입점"에만 제한을 거는 것이 옳다.

---

### 2. DropTable 데이터화 — SO 저작 + 단일 소스 + 공유 roll

처음 DropTable은 SocketServer의 하드코딩 정적 클래스였다(챕터 15). Main이 같은 드랍을 굴리려면 클라도 이 데이터·로직이 필요한데, 두 가지 제약이 충돌했다:

- **클라는 ScriptableObject로 편집하고 싶다**(디자이너 친화).
- **서버는 `.asset`(SO)을 못 읽는다** — `Shared.Gameplay`는 Unity 밖 컴파일 DLL이고 SocketServer도 Unity 에셋을 런타임 로드 못 한다.

해법은 이미 이 프로젝트가 spawn-layouts에서 쓰던 패턴 — **SO 저작 → JSON bake → 양쪽 로드**. 단, roll *로직*은 결정성 일관을 위해 공유 DLL에 두어 3층으로 분리했다:

```
[클라/디자이너]                 [공유 포맷]            [로드]
DropTableDefinition (SO)  ─bake→ drop-tables.json ─┬→ 서버: DropTableCatalog.Parse (Shared.Infrastructure, 임베디드)
  Inspector 편집                                    └→ 클라: SO 직접 Resources.Load
        │                                              │
        └──────── roll 로직 = Shared.Gameplay.DropTableRoll.Roll(entries, rng) ────┘  (서버·클라 공유 DLL)
```

- **데이터** = SO(단일 저작 소스) → JSON으로 bake(서버가 읽는 유일한 길).
- **roll** = `Shared.Gameplay` 순수 함수(JSON 의존 없음 → 클라 DLL을 가볍게 유지). 서버 던전과 Main이 *같은 함수*로 굴려 결과가 일관된다.
- **파싱** = `Shared.Infrastructure`(서버, System.Text.Json). 클라는 SO를 직접 읽으니 JSON 파서가 불필요.

`Tools/Loot/Export`(SO→JSON)·`Import`(부트스트랩)는 spawn-layouts의 `MapDataExporter`와 같은 컨벤션.

---

### 3. Main 로컬 전투는 던전 컴포넌트를 재사용하지 않는다

던전의 `MonsterEntity`·`GroundItemEntity`는 "서버 명령을 받아 표시·중계"하는 전용이다(보간, `C_PickupItem` 송신). Main은 클라가 *판정*하는 역할이라 근본적으로 다르다. 억지로 한 컴포넌트로 합치면 양쪽 조건 분기로 더 복잡해진다 — 그래서 **별도 클라 권위 컴포넌트**를 뒀다:

```
LocalMonster   : HP·간단 AI(Idle/Chase)·TakeDamage→OnDied (서버 MonsterEntity 보간과 무관)
LocalCombat    : PlayerCharacterAgent.OnAttackPerformed 구독
                 → Physics.OverlapSphere 로 근처 LocalMonster 수집
                 → 서버와 동일한 HitboxMath.Overlaps(SkillCatalog "basic_swing") 정밀 판정 → TakeDamage
LocalGroundItem: IInteractable, E 줍기 → IInventoryGrpcService.GrantItemAsync → 성공 시 디스폰
MainMonsterSpawner: 비-Joined(Main)일 때만 스폰, OnDied → DropTableRoll → LocalGroundItem 스폰
```

**재사용한 것은 "로직"이지 "컴포넌트"가 아니다** — 적중 판정 `HitboxMath`, roll `DropTableRoll`은 던전 서버와 같은 `Shared.Gameplay` DLL을 공유한다. 그래서 던전과 Main의 "타격감·드랍 확률"이 한 소스에서 나온다.

공격 발동도 던전 코드를 안 건드렸다. `PlayerCharacterAgent.OnAttackPerformed`(기존 이벤트)에 던전은 `CombatSyncSender`(C_Attack), Main은 `LocalCombat`이 각각 구독 — `CharacterSpawner`가 씬 분기(Joined=던전 / 아니면 Main)로 어느 쪽을 붙일지 결정한다.

---

### 4. 몬스터 수집은 레지스트리 대신 Physics

`LocalCombat`이 때릴 대상을 찾을 때, 몬스터들을 별도 리스트(레지스트리)로 관리할 수도 있다. 하지만 몬스터엔 어차피 콜라이더가 있으니 **`Physics.OverlapSphere` + `GetComponentInParent<LocalMonster>`** 로 광역 수집 후 `HitboxMath`로 정밀 판정하면 레지스트리 동기화(추가/제거 누락) 없이 끝난다. Unity 관용 방식 — YAGNI.

---

## 트러블슈팅 (이번 작업의 실제 디버깅)

### `Game.System` 네임스페이스가 `System`을 가렸다

`MainMonsterSpawner`에서 `private readonly System.Random _rng`이 컴파일 에러(`'Random' does not exist in 'Game.System'`). 프로젝트에 `Game.System` 네임스페이스가 있어, `Game.Gameplay.Character` 안에서 `System.Random`의 `System`이 `Game.System`으로 해석됐다. → `global::System.Random`으로 명시 해소. (CLAUDE.md 테스트 규칙이 경고하던 바로 그 함정 — 테스트 네임스페이스뿐 아니라 런타임 코드에서도 발생.)

### Docker 빌드만 실패 — 전이 의존 누락 (`NETSDK1004`)

DropTable 데이터화에서 `Shared.Infrastructure → Shared.Gameplay` 참조를 추가하자, **로컬 sln 빌드는 통과**하는데 **GameServer Docker 빌드만** `NETSDK1004: project.assets.json not found`로 실패했다.

- 원인: GameServer가 이제 `Shared.Infrastructure`를 통해 `Shared.Gameplay`를 **전이 의존**하는데, `GameServer/Dockerfile`의 레이어 캐시용 csproj 선택 복사 목록에 `Shared.Gameplay.csproj`가 없어 restore가 그 프로젝트의 assets를 못 만들었다.
- 로컬은 sln 전체를 restore하니 안 잡힘 — **Docker의 선택적 restore가 의존 그래프 변경을 못 따라간 것**.
- → Dockerfile에 `COPY Shared/Shared.Gameplay/Shared.Gameplay.csproj` 한 줄 추가로 해소. (socketserver는 원래 Shared.Gameplay를 참조해 무영향.)

교훈: 공유 프로젝트의 참조 그래프를 바꾸면 **Dockerfile의 명시적 restore 목록**도 따라 갱신해야 한다. 로컬 빌드 그린이 Docker 빌드 그린을 보장하지 않는다.

---

## 검증

| 영역 | 방식 |
| --- | --- |
| `GrantItem` 가드(정상·수량상한·미존재·미인증) | E2E `InventoryE2ETests` 6/6 (Docker) |
| 드랍 roll 확률/수량 | 단위 `DropTableRollTests` (Shared.Gameplay) |
| 임베디드 데이터 파싱(SO→Export 반영) | 단위 `DropTableCatalogTests` (SocketServer.Tests 73/73) |
| 클라 SO→roll→GrantItem→인벤토리 **체인** | E2E `MainLootE2ETests` 1/1 (Docker) |
| 적중 판정 | 단위 `HitboxMathTests` (Shared.Gameplay) |
| 공격→사망→드랍→E 줍기→인벤토리 아이콘 | **플레이 검증(사람)** — MonoBehaviour 글루는 자동 E2E 밖 |

**자동 테스트(로직·서버 계약)와 플레이(MonoBehaviour 글루)가 상호 보완**하는 구도. 클라 데이터+공유 roll+서버 지급의 체인은 E2E로 잡고, 프리팹 인스턴스화·콜라이더 감지·실시간 줍기는 플레이로 잡았다.

---

## 핵심 키워드 정리

| 키워드 | 한 줄 설명 |
| --- | --- |
| 권위 비대칭 | 던전=서버권위(클라신뢰0) / Main=클라권위+서버 가드. 지급(GameServer)만 공통 |
| GrantItem 가드 | 인증(인터셉터)+수량상한(진입점 한정)+catalog 검증. cap은 도메인이 아닌 gRPC 진입점에 |
| DropTable 3층 | 데이터=SO→JSON / roll=Shared.Gameplay DLL(공유) / 파싱=Shared.Infrastructure(서버) |
| 공유 roll·hitbox | 던전·Main이 같은 DropTableRoll/HitboxMath DLL → 확률·타격감 단일 소스 |
| 컴포넌트 분리 | LocalMonster/LocalCombat/LocalGroundItem = 클라권위 신규(던전 보간 컴포넌트 재사용 X) |
| OnAttackPerformed 분기 | 같은 이벤트에 던전=CombatSyncSender / Main=LocalCombat, CharacterSpawner가 씬 분기 |
| Physics 수집 | 레지스트리 대신 OverlapSphere+GetComponentInParent (Unity 관용, YAGNI) |
| `global::System` | Game.System 네임스페이스가 System을 가림 → global:: 로 명시 |
| Docker 전이 의존 | 참조 그래프 변경 시 Dockerfile 선택 restore 목록도 갱신(로컬 그린 ≠ Docker 그린) |

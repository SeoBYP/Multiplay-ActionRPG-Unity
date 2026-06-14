# 스폰 시스템 진화 — 단일 SpawnSystem(이벤트 드리븐) 준비

> **지금 만들지 않는다(YAGNI).** 스폰 *원인*이 늘어나는 시점에 이 문서대로 1:1 승격한다.
> 핵심 = **"왜 스폰(원인)"과 "어떻게 스폰(방법)"의 분리** — 원인은 여럿, 방법은 하나.

## 왜 지금 안 만드나

서버 스폰 *원인*이 현재 **1개**뿐이다(던전 시작). EventBus·`SpawnRequest`·`SpawnSystem` 라우터를 지금 넣으면 쓰지도 않는 배관 = 과추상화(CLAUDE.md 원칙 1). "나쁜 구조"(`SpawnByField/SpawnByQuest/...` 함수 난립)는 **원인이 3개 이상**일 때 생기는 문제다. 우리는 아직 거기 안 갔다.

## 현재 상태

```
[원인]                         [방법=단일 진입점]
던전 시작(RoomManager)  ─────▶  Room.SpawnMonsters(layout.Monsters, bounds)   ← 서버 권위, 유일 진입점
Main 슬롯 리스폰 타이머  ─────▶  MainMonsterSpawner.Spawn(slot)               ← 클라 권위(B-lite), 서버 스폰 아님
```

- **서버(던전)**: `RoomManager`(게임 시작)가 `SpawnLayoutTable` 레이아웃을 읽어 `Room.SpawnMonsters` **1회** 호출. AI·생명주기는 `RoomTickService` Tick.
- **클라(Main, B-lite)**: `MainMonsterSpawner`가 슬롯 기반 스폰 + 쿨다운 재스폰. 서버는 `ClaimKill`로 **검증만**(소유 아님). 정본 = [main-spawn-claim.md](main-spawn-claim.md).

## 지금 지키는 불변식 (= 준비의 핵심)

**모든 서버 몬스터 생성은 `Room.SpawnMonsters` 한 점으로 수렴한다.**
새 스폰 원인이 생겨도 `_monsters` 에 직접 추가하지 말고 이 메서드를 경유한다(코드 주석으로 고정). → 미래 라우터가 **이 한 점만 감싸면** 전환 끝. 데이터는 `MonsterSpawnDef`(맵 단위, SO→bake 교리 [gas-architecture §2.5](gas-architecture.md))를 유지.

## 미래 목표 (원인 ≥3 시 승격)

```
[스폰 원인 = producer]                 [통일]                      [방법 = consumer]
 DungeonStart (RoomManager)  ┐
 Wave            (4.1.6)     ├─▶ SpawnRequest{ cause, spawnGroupId } ─▶ SpawnSystem.Handle
 Quest           (4.4)       │      (EventBus 또는 직접 호출)             ├ SpawnGroup 조회
 AreaEnter       (4.6.1)     │                                           ├ 중복 활성 체크(WorldState)
 RespawnTimer(서버)          ┘                                           └ Room.SpawnMonsters(재사용)
```

- **producer**(Wave/Quest/Area 시스템)는 `SpawnRequest`만 발행 — 어떻게 스폰되는지 모른다.
- **consumer**(`SpawnSystem`)는 `SpawnGroup` 조회·중복 체크 후 기존 `Room.SpawnMonsters`로 위임. 방법은 그대로.
- **데이터 확장**: 현재 `MonsterSpawnDef`(맵 단위) → 필요 시 `SpawnGroup`(원인별 묶음, wave/quest 키)으로. SO→bake 교리 유지.
- 위치 = **SocketServer**(서버 권위 스폰). 첫 producer = 기존 `RoomManager` 호출.

## 승격 트리거 (이 조건 전엔 만들지 않음)

다음 중 **첫 번째가 실제 착수될 때** 이 문서대로 `SpawnSystem`/`SpawnRequest`를 신설한다:

- **4.1.6** 몬스터 웨이브/스폰 페이즈
- **4.4** 퀘스트 스폰
- **4.6.1** 존/포탈(존 진입 스폰)
- 또는 **Main B-full**(서버 권위 Main, co-op 오픈월드화)

전환 절차: ① `SpawnRequest`+`SpawnSystem` 신설 ② `RoomManager`의 직접 호출을 첫 producer로 전환 ③ 새 원인을 producer로 추가. **기존 `Room.SpawnMonsters`·관련 테스트는 그대로 재사용**(방법은 안 바뀐다).

## Main(클라) 측 합류

Main은 클라 권위(B-lite)라 현재 서버 `SpawnSystem`과 별개다. **Main co-op화(B-full)** 시 Main도 서버 `SpawnSystem`의 한 원인(AreaEnter)으로 합류하고, 그때 `LocalMonster`는 던전처럼 `MonsterEntity` 보간으로 대체된다. 트리거 = Main co-op. ([authority-model §4b](authority-model.md) / main-spawn-claim "안 한 것").

## 한 줄 요약

> 지금은 `Room.SpawnMonsters` **단일 진입점 + 이 문서**가 준비의 전부다. 스폰 원인이 늘면 그 한 점을 `SpawnSystem` 이벤트 라우터로 감싸 "원인↔방법"을 분리한다 — 코드 미리 안 짜고, 갈아끼울 자리만 명확히 비워둔다.

using Xunit;

// 테스트 클래스 병렬 실행을 끈다.
//
// **이유(실제로 터졌다)**: `Server.Diagnostics.CombatTrace` 는 **static 로거**다(패킷 핸들러가 static 이라
// DI 가 닿지 않아 내린 설계 — AC-C1a). 그래서 `CombatTraceTests` 가 fake 로거를 설치한 사이
// 다른 클래스(`Room.Tick` 를 부르는 MonsterAttackTests·BossMultiAbilityTests 등)가 병렬로 돌면
// 그 fake 에 **남의 로그가 섞여 들어온다** — 실제로 `PlayerToPlayer` 를 기대한 자리에서
// `MonsterToPlayer` 가 잡혀 실패했다. 클래스가 하나 늘어 스케줄이 바뀌자 드러났고,
// 그전까지 통과하던 건 운이었다.
//
// 대안이었던 "같은 Collection 으로 묶기" 는 **새로 Room.Tick 을 부르는 클래스가 생길 때마다
// 조용히 깨진다**(빠뜨리면 다시 플래키). 이 스위트는 전체 90ms 라 직렬화 비용이 사실상 0 이므로
// 어셈블리 단위로 끄는 쪽이 안전하고 단순하다.
//
// 근본적으로는 CombatTrace 의 static 상태가 원인이지만, 그건 핸들러 static 설계에서 온 것이라
// 여기서 바꾸지 않는다(진단 전용·기본 Off).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

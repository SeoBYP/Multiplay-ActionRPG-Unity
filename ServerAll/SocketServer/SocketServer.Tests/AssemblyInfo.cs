using Xunit;

// 테스트 클래스 병렬 실행을 끈다.
//
// **이유**: `Server.Diagnostics.CombatTrace` 는 **static 로거**(패킷 핸들러가 static 이라 DI 가 닿지 않는다)다.
// `CombatTraceTests` 가 fake 로거를 설치한 사이 다른 클래스(`Room.Tick` 호출)가 병렬로 돌면
// fake 에 **남의 로그가 섞여 플래키**해진다.
//
// "같은 Collection 으로 묶기" 는 **Room.Tick 을 부르는 클래스가 새로 생길 때마다 조용히 깨진다**.
// 스위트가 짧아 직렬화 비용이 사실상 0 이므로 어셈블리 단위로 끈다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// Main(싱글) 획득 서버 검증 — B-lite. 클라가 "어느 슬롯을 죽였다"(mapId,slotId)만 보고하면
/// 서버가 map 스폰 데이터로 슬롯 검증 + per-user 쿨다운(재청구 차단) + 서버 권위 DropTableRoll → 지급한다.
/// 보상 내용은 서버가 결정 — 클라가 itemId/qty 를 임의 지정하던 무한파밍 핵(구 GrantItem)을 차단.
/// 정본 = docs/wiki/main-spawn-claim.md / authority-model §4b.
/// </summary>
public interface IMainSpawnClaimService
{
    Task<MainClaimResult> ClaimKillAsync(long userId, string mapId, int slotId, CancellationToken ct = default);
}

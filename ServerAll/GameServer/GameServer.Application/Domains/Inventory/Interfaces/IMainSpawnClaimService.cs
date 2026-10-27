namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// Main(싱글) 획득 서버 검증. 클라는 "어느 슬롯을 죽였다"(mapId,slotId)만 보고하고,
/// 서버가 스폰 데이터로 슬롯 검증 + per-user 쿨다운 + DropTableRoll 후 지급한다. 보상 내용은 서버가 결정.
/// </summary>
public interface IMainSpawnClaimService
{
    /// <summary>아이템 청구(전리품 줍기 시점) — 슬롯 검증 + 아이템 쿨다운 + roll 지급.</summary>
    Task<MainClaimResult> ClaimKillAsync(long userId, string mapId, int slotId, CancellationToken ct = default);

    /// <summary>경험치 청구(킬 즉시) — 슬롯 검증 + 아이템과 독립된 exp 쿨다운 → 몬스터 정의 ExpReward 적립.</summary>
    Task<MainExpClaimResult> ClaimExpAsync(long userId, string mapId, int slotId, CancellationToken ct = default);
}

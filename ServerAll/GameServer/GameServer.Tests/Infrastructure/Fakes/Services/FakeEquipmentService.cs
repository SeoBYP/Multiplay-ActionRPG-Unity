using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;

namespace GameServer.Tests.Infrastructure.Fakes.Services;

/// <summary>
/// IEquipmentService 스텁. ProgressionService 합산 테스트용 — 착용 스탯만 미리 세팅해 반환.
/// 장착/해제/조회는 합산 검증에 불필요하므로 최소 구현.
/// </summary>
public class FakeEquipmentService : IEquipmentService
{
    /// <summary>GetEquippedStatsAsync 가 반환할 합산 스탯(기본 0).</summary>
    public EquipmentStatModifier EquippedStats { get; set; } = default;

    public Task<EquipmentStatModifier> GetEquippedStatsAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(EquippedStats);

    public Task<List<UserEquipment>> GetEquippedAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(new List<UserEquipment>());

    public Task<EquipResult> EquipAsync(long userId, string itemId, CancellationToken ct = default)
        => Task.FromResult(EquipResult.Ok(default, itemId));

    public Task<EquipResult> UnequipAsync(long userId, EquipmentType slot, CancellationToken ct = default)
        => Task.FromResult(EquipResult.Ok(slot, string.Empty));
}

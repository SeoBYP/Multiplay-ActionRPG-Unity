using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Domain.Entities.User;
using Shared.Infrastructure.Progression;

namespace GameServer.Application.Domains.Progression;

/// <summary>
/// 진행 서비스 구현. 보상 산정/멱등은 호출자(DungeonResultConsumer) 담당 — 여기선 단일 유저 적립/조회만.
/// GetStatsAsync 가 단일 합산 권위: base(레벨 룩업) + 장비 합산(EquipmentService 에 응집).
/// </summary>
public sealed class ProgressionService(
    IProgressionRepository repository,
    IEquipmentService equipment) : IProgressionService
{
    public async Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            var current = await repository.GetAsync(userId, ct);
            return current?.Exp ?? 0;
        }

        var updated = await repository.AddExpAsync(userId, amount, ct);
        return updated.Exp;
    }

    public async Task<UserProgression> GetProgressionAsync(long userId, CancellationToken ct = default)
    {
        // row 가 없으면 기본 Lv1/Exp0 뷰(영속하지 않음)
        var current = await repository.GetAsync(userId, ct);
        return current ?? UserProgression.Create(userId);
    }

    public async Task<PlayerStats> GetStatsAsync(long userId, CancellationToken ct = default)
    {
        var progression = await GetProgressionAsync(userId, ct);
        var s = LevelTable.StatsAt(progression.Level);
        var eq = await equipment.GetEquippedStatsAsync(userId, ct);

        return new PlayerStats(
            progression.Level,
            s.MaxHealth + eq.MaxHealth,
            s.MaxMana + eq.MaxMana,
            s.AttackPower + eq.AttackPower,
            s.Defense + eq.Defense,
            s.Strength + eq.Strength,
            s.Dexterity + eq.Dexterity,
            s.Intelligence + eq.Intelligence);
    }
}

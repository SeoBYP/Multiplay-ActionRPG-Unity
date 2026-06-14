using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Domain.Entities.User;
using Shared.Infrastructure.Progression;

namespace GameServer.Application.Domains.Progression;

/// <summary>
/// 진행(레벨·경험치) 도메인 서비스 구현. Repository 의 lazy get-or-create + 적립/조회를 위임한다.
/// 던전 보상 산정/멱등은 호출자(DungeonResultConsumer)가 담당 — 여기선 단일 유저 적립/조회만.
/// </summary>
public sealed class ProgressionService(IProgressionRepository repository) : IProgressionService
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
        // 적립 이력이 없으면 row 가 없다(lazy create) → 기본 Lv1/Exp0 뷰로 응답(영속하지 않음).
        var current = await repository.GetAsync(userId, ct);
        return current ?? UserProgression.Create(userId);
    }

    public async Task<PlayerStats> GetStatsAsync(long userId, CancellationToken ct = default)
    {
        // 단일 합산 권위(authority-model §4c). 오늘 = 레벨 테이블 룩업.
        // 장비(3.2)/버프가 생기면 여기서 modifier 를 더해 합산한다(SocketServer 는 결과만 받음).
        var progression = await GetProgressionAsync(userId, ct);
        var s = LevelTable.StatsAt(progression.Level);
        return new PlayerStats(progression.Level, s.MaxHealth, s.MaxMana, s.AttackPower,
            s.Defense, s.Strength, s.Dexterity, s.Intelligence);
    }
}

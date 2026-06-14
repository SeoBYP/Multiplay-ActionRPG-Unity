using GameServer.Application.Domains.Progression;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class ProgressionServiceTests
{
    private static ProgressionService CreateService(out FakeProgressionRepository repo)
    {
        repo = new FakeProgressionRepository();
        return new ProgressionService(repo);
    }

    [Fact]
    public async Task 경험치를_적립하면_누적된_Exp를_반환한다()
    {
        var service = CreateService(out _);

        await service.AddExpAsync(userId: 1L, amount: 50);
        var total = await service.AddExpAsync(userId: 1L, amount: 30);

        Assert.Equal(80L, total);
    }

    [Fact]
    public async Task 적립_금액이_0이하이면_적립하지_않고_현재_Exp를_반환한다()
    {
        var service = CreateService(out _);
        await service.AddExpAsync(1L, 80); // 80 < Lv1 임계(100) → 레벨업 없이 누적만

        var afterZero = await service.AddExpAsync(1L, 0);
        var afterNegative = await service.AddExpAsync(1L, -10);

        Assert.Equal(80L, afterZero);
        Assert.Equal(80L, afterNegative);
    }

    [Fact]
    public async Task 진행이_없는_유저에_0이하_적립이면_0을_반환한다()
    {
        var service = CreateService(out _);

        var result = await service.AddExpAsync(userId: 99L, amount: 0);

        Assert.Equal(0L, result);
    }

    [Fact]
    public async Task 적립_이력이_없는_유저의_스탯은_레벨1_테이블값이다()
    {
        var service = CreateService(out _);

        var stats = await service.GetStatsAsync(userId: 1L);

        Assert.Equal(1, stats.Level);
        Assert.Equal(100, stats.MaxHealth);   // level-table.json Lv1
        Assert.Equal(10, stats.AttackPower);
        Assert.Equal(5, stats.Defense);
    }

    [Fact]
    public async Task 레벨업_후_스탯은_오른_레벨의_테이블값이다()
    {
        var service = CreateService(out _);
        await service.AddExpAsync(1L, 120); // Lv1 임계(100) 초과 → Lv2

        var stats = await service.GetStatsAsync(1L);

        Assert.Equal(2, stats.Level);
        Assert.Equal(120, stats.MaxHealth);  // 100 + 1*20
        Assert.Equal(13, stats.AttackPower); // 10 + 1*3
    }
}

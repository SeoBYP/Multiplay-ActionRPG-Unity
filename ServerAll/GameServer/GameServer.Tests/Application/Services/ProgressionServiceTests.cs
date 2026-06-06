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
        await service.AddExpAsync(1L, 100);

        var afterZero = await service.AddExpAsync(1L, 0);
        var afterNegative = await service.AddExpAsync(1L, -10);

        Assert.Equal(100L, afterZero);
        Assert.Equal(100L, afterNegative);
    }

    [Fact]
    public async Task 진행이_없는_유저에_0이하_적립이면_0을_반환한다()
    {
        var service = CreateService(out _);

        var result = await service.AddExpAsync(userId: 99L, amount: 0);

        Assert.Equal(0L, result);
    }
}

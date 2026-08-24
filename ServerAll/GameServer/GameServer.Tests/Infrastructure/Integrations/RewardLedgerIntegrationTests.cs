using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Application.Domains.Reward.Interfaces;
using GameServer.Infrastructure.Domains.Equipment;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.Progression;
using GameServer.Infrastructure.Domains.Reward;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// 보상 지급 원장 — exactly-once 계약 검증.
///
/// Redis 키 잠금으로는 지급과 기록이 다른 저장소라 "지급됐는데 기록이 없다 / 기록됐는데 지급이 없다" 창이 남았다.
/// 원장은 지급과 같은 트랜잭션에 있으므로 그 창이 없어야 한다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class RewardLedgerIntegrationTests(RepositoryTestFixture fixture)
{
    /// <summary>실 DI 체인으로 스코프 1개를 만든다(지급 1건 = 스코프 1개 — 프로덕션과 동일).</summary>
    private ServiceProvider BuildScopeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(fixture.DbConnectionString));
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IProgressionService, ProgressionService>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<GameServer.Application.Domains.Codex.Interfaces.ICodexRepository, GameServer.Infrastructure.Domains.Codex.CodexRepository>();
        services.AddScoped<GameServer.Application.Domains.Codex.Interfaces.ICodexService, GameServer.Application.Domains.Codex.CodexService>();
        services.AddScoped<IRewardLedger, RewardLedger>();
        return services.BuildServiceProvider();
    }

    private async Task<long> ExpOfAsync(long userId)
    {
        await using var ctx = fixture.CreateDbContext();
        var row = await ctx.UserProgressions.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
        return row?.Exp ?? 0;
    }

    private async Task<int> LedgerCountAsync(string grantKey)
    {
        await using var ctx = fixture.CreateDbContext();
        return await ctx.RewardGrants.AsNoTracking().CountAsync(g => g.GrantKey == grantKey);
    }

    [Fact]
    public async Task 같은_GrantKey_로_두_번_호출해도_지급은_한_번만_일어난다()
    {
        long userId = 96001;
        var key = $"dungeon:96000:{userId}";

        await using var p1 = BuildScopeProvider();
        bool first = await GrantExpAsync(p1, key, userId, 50);

        await using var p2 = BuildScopeProvider();
        bool second = await GrantExpAsync(p2, key, userId, 50);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(50, await ExpOfAsync(userId));
        Assert.Equal(1, await LedgerCountAsync(key));
    }

    [Fact]
    public async Task 지급이_실패하면_원장도_함께_롤백돼_재시도가_가능하다()
    {
        long userId = 96002;
        var key = $"dungeon:96000:{userId}";

        await using var p1 = BuildScopeProvider();
        using (var scope = p1.CreateScope())
        {
            var ledger = scope.ServiceProvider.GetRequiredService<IRewardLedger>();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ledger.GrantOnceAsync(
                    new RewardGrantRequest(key, userId, "exp", "", 50),
                    _ => throw new InvalidOperationException("grant failed")));
        }

        // 지급이 안 됐으면 원장에도 남으면 안 된다 — 남으면 재시도가 영원히 "이미 지급됨" 으로 막힌다.
        Assert.Equal(0, await LedgerCountAsync(key));
        Assert.Equal(0, await ExpOfAsync(userId));

        // 재시도는 정상적으로 지급된다.
        await using var p2 = BuildScopeProvider();
        bool retried = await GrantExpAsync(p2, key, userId, 50);

        Assert.True(retried);
        Assert.Equal(50, await ExpOfAsync(userId));
        Assert.Equal(1, await LedgerCountAsync(key));
    }

    private static async Task<bool> GrantExpAsync(ServiceProvider provider, string grantKey, long userId, long amount)
    {
        using var scope = provider.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IRewardLedger>();
        var progression = scope.ServiceProvider.GetRequiredService<IProgressionService>();
        return await ledger.GrantOnceAsync(
            new RewardGrantRequest(grantKey, userId, "exp", "", amount),
            token => progression.AddExpAsync(userId, amount, token));
    }
}

using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.Progression;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Domains.Wallet;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// G3 — 경제 쓰기 경로의 동시성(lost update / 이중 차감 통과).
///
/// 지갑·인벤토리·Exp 저장소는 모두 "추적 쿼리로 읽기 → 메모리에서 검사·증감 → SaveChanges" 다.
/// 동시성 토큰도 원자 UPDATE 도 없어서 Postgres READ COMMITTED 에서 두 요청이 같은 값을 읽고
/// 각자 덮어쓴다.
///
///   요청A ─ read(100) ─┐
///                     ├─ 둘 다 "충분" ─▶ write(0) ×2   → 1개 값으로 2개 구매
///   요청B ─ read(100) ─┘
///
/// 실제 요청처럼 <b>요청마다 DbContext 를 새로 만들고</b>, 출발 게이트로 동시에 풀어 경합을 만든다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class EconomyConcurrencyTests(RepositoryTestFixture fixture)
{
    private const int Concurrency = 10;
    private const int Potion = 1001; // maxStack 99

    private async Task<long> CreateUserAsync()
    {
        using var context = fixture.CreateDbContext();
        var userRepo = new UserRepository(fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        return (await userRepo.CreateAsync()).UserId;
    }

    private WalletRepository Wallet() => new(fixture.RedisConnection, fixture.CreateDbContext(), NullLogger<WalletRepository>.Instance);
    private InventoryRepository Inventory() => new(fixture.RedisConnection, fixture.CreateDbContext(), NullLogger<InventoryRepository>.Instance);
    private ProgressionRepository Progression() => new(fixture.RedisConnection, fixture.CreateDbContext(), NullLogger<ProgressionRepository>.Instance);

    /// <summary>
    /// 같은 순간에 <paramref name="count"/> 개 요청을 푼다. 예외는 "실패한 요청"으로 센다
    /// (원자화 후 동시성 예외로 거절되는 것도 올바른 결과이기 때문).
    /// </summary>
    private static async Task<List<T?>> RaceAsync<T>(int count, Func<Task<T>> request)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(async () =>
        {
            await gate.Task;
            try { return (T?)await request(); }
            catch { return default; }
        })).ToList();

        gate.SetResult();
        return (await Task.WhenAll(tasks)).ToList();
    }

    [Fact]
    public async Task 잔액_100에서_100골드_결제가_동시에_와도_한_번만_성공한다()
    {
        var userId = await CreateUserAsync();
        await Wallet().AddBalanceAsync(userId, 100);

        var results = await RaceAsync(Concurrency, () => Wallet().TrySpendBalanceAsync(userId, 100));

        Assert.Equal(1, results.Count(r => r is not null));
        Assert.Equal(0, await Wallet().GetBalanceAsync(userId));
    }

    [Fact]
    public async Task 골드_적립이_동시에_와도_합계가_유실되지_않는다()
    {
        var userId = await CreateUserAsync();
        await Wallet().AddBalanceAsync(userId, 1); // 행을 먼저 만든다(lazy create 경합은 별개 문제)

        await RaceAsync(Concurrency, () => Wallet().AddBalanceAsync(userId, 10));

        using var ctx = fixture.CreateDbContext();
        var row = await ctx.UserWallets.FindAsync(userId);
        Assert.Equal(1 + 10 * Concurrency, row!.Balance);
    }

    [Fact]
    public async Task 아이템_차감이_동시에_와도_보유량보다_많이_차감되지_않는다()
    {
        // 판매 복제: 5개 보유, 1개씩 판매 10회 동시 → 성공은 최대 5회여야 한다.
        var userId = await CreateUserAsync();
        await Inventory().AddQuantityAsync(userId, Potion, 5, maxStack: 99);

        var results = await RaceAsync(Concurrency, () => Inventory().RemoveQuantityAsync(userId, Potion, 1));

        Assert.True(results.Count(r => r is not null) <= 5,
            $"5개 보유인데 차감 성공이 {results.Count(r => r is not null)}회 — 성공 1회마다 골드가 지급된다.");
    }

    [Fact]
    public async Task 아이템_적립이_동시에_와도_수량이_유실되지_않는다()
    {
        var userId = await CreateUserAsync();
        await Inventory().AddQuantityAsync(userId, Potion, 1, maxStack: 99);

        await RaceAsync(Concurrency, async () => (await Inventory().AddQuantityAsync(userId, Potion, 1, maxStack: 99)).Quantity);

        using var ctx = fixture.CreateDbContext();
        var row = ctx.InventoryItems.Single(i => i.UserId == userId && i.ItemId == Potion);
        Assert.Equal(1 + Concurrency, row.Quantity);
    }

    [Fact]
    public async Task 경험치_적립이_동시에_와도_합계가_유실되지_않는다()
    {
        // 던전 보상 컨슈머와 Main 킬 exp 가 같은 유저에게 겹치는 상황. 레벨업 경계(100) 아래로 유지.
        var userId = await CreateUserAsync();
        await Progression().AddExpAsync(userId, 5);

        await RaceAsync(Concurrency - 1, async () => (await Progression().AddExpAsync(userId, 5)).Exp);

        using var ctx = fixture.CreateDbContext();
        var row = await ctx.UserProgressions.FindAsync(userId);
        Assert.Equal(1, row!.Level);
        Assert.Equal(5 * Concurrency, row.Exp);
    }
}

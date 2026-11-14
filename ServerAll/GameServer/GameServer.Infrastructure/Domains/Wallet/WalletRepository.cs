using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain.Entities.Wallet;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Wallet;

/// <summary>
/// 재화(골드) 잔액 저장소. Cache-Aside + Delete.
/// 캐시 = 유저당 String 1키(잔액 정수). 지갑이 없거나 잔액 0 도 캐시한다(인벤 Hash 와 달리
/// String "0" 은 MISS 와 구분 가능 → 폴백 트래픽 절감). 음수 잔액은 도메인이 막는다.
/// </summary>
public class WalletRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<WalletRepository> logger) : IWalletRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    
    public async Task<long> GetBalanceAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var cached = await _database.StringGetAsync(RedisKeys.UserWallet(userId));
            if (cached.HasValue && long.TryParse(cached.ToString(), out var bal))
                return bal;

            // AsNoTracking: 오래 유지되는 DbContext 의 stale 방지
            var wallet = await context.UserWallets
                .AsNoTracking()
                .SingleOrDefaultAsync(w => w.UserId == userId, ct);

            var balance = wallet?.Balance ?? 0;
            await SetCacheAsync(userId, balance);
            return balance;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get wallet balance for user {UserId}", userId);
            throw;
        }
    }

    public async Task<long> AddBalanceAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        try
        {
            if (!await context.UserWallets.AnyAsync(w => w.UserId == userId, ct))
                await context.AddIfMissingAsync(UserWallet.Create(userId), ct);
            
            await context.UserWallets
                .Where(w => w.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance + amount), ct);
            
            // 캐시는 덮어쓰지 않고 DEL (stale·부분갱신 방지)
            await DeleteCacheAsync(userId);

            return await context.UserWallets.AsNoTracking()
                .Where(w => w.UserId == userId)
                .Select(w => w.Balance)
                .SingleAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add {Amount} gold for user {UserId}", amount, userId);
            throw;
        }
    }

    public async Task<long?> TrySpendBalanceAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return null;

        try
        {
            //var wallet = await context.UserWallets.SingleOrDefaultAsync(...);   // ① 읽기: 100
            // if (wallet is null || !wallet.TrySpend(amount)) return null;        // ② 확인: C# 메모리 안에서 100 >= 100
            // await context.SaveChangesAsync(ct);                                  // ③ 쓰기: UPDATE ... SET balance = 0
            // 위에 코드에서 요청 10개가 동시에 오면 이렇게 됩니다.
            // 요청1: ① 100 읽음 ─ ② OK ─ ③ balance=0 저장
            // 요청2: ① 100 읽음 ─ ② OK ─ ③ balance=0 저장   ← 요청1이 저장하기 전에 ①을 끝냄
            //     ...
            // 요청10: 똑같음
            //     → 10번 모두 성공(골드 1000 사용), 잔액은 0
            var rows = await context.UserWallets
                .Where(w => w.UserId == userId && w.Balance >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - amount), ct);

            if (rows == 0)
                return null;

            await DeleteCacheAsync(userId);

            return await context.UserWallets.AsNoTracking()
                .Where(w => w.UserId == userId)
                .Select(w => w.Balance).SingleAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to spend {Amount} gold for user {UserId}", amount, userId);
            throw;
        }
    }

    private async Task SetCacheAsync(long userId, long balance)
    {
        var key = RedisKeys.UserWallet(userId);
        var transaction = _database.CreateTransaction();
        _ = transaction.StringSetAsync(key, balance);
        _ = transaction.KeyExpireAsync(key, RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to cache wallet balance: transaction rolled back");
    }

    private async Task DeleteCacheAsync(long userId)
    {
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(RedisKeys.UserWallet(userId));
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete wallet cache");
    }
}
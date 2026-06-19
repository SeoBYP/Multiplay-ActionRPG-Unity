using GameServer.Application.Domains.Wallet.Interfaces;
using GameServer.Domain.Entities.Wallet;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Wallet;

/// <summary>
/// 재화(골드) 잔액 저장소. Cache-Aside + Delete (networking.md).
///   GetBalance : Redis String HIT 즉시 반환 / MISS → DB(AsNoTracking) → 캐시 SET(TTL)
///   Add/Spend  : DB 갱신(없으면 lazy create) → SaveChanges → 캐시 DEL
///
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

            // cache-aside 읽기 전용 → AsNoTracking (long-lived DbContext stale 방지)
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
        try
        {
            // 쓰기 경로 — 추적 쿼리로 로드(없으면 lazy create) 후 누적.
            var wallet = await context.UserWallets
                .SingleOrDefaultAsync(w => w.UserId == userId, ct);

            if (wallet is null)
            {
                wallet = UserWallet.Create(userId, Math.Max(0, amount));
                await context.UserWallets.AddAsync(wallet, ct);
            }
            else
            {
                wallet.Add(amount);
            }

            await context.SaveChangesAsync(ct);

            // Update 는 캐시를 덮어쓰지 않고 DEL (stale·부분갱신 위험 회피).
            await DeleteCacheAsync(userId);

            return wallet.Balance;
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
            var wallet = await context.UserWallets
                .SingleOrDefaultAsync(w => w.UserId == userId, ct);

            // 지갑 없음 또는 잔액 부족 → 실패(변화 없음).
            if (wallet is null || !wallet.TrySpend(amount))
                return null;

            await context.SaveChangesAsync(ct);
            await DeleteCacheAsync(userId);

            return wallet.Balance;
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

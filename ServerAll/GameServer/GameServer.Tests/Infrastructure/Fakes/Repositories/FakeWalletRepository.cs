using GameServer.Application.Domains.Wallet.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

/// <summary>실제 WalletRepository 의 증감/잔액부족 동작을 인메모리로 모사.</summary>
public class FakeWalletRepository : IWalletRepository
{
    private readonly Dictionary<long, long> _balances = new();

    public Task<long> GetBalanceAsync(long userId, CancellationToken ct = default)
        => Task.FromResult(_balances.GetValueOrDefault(userId, 0));

    public Task<long> AddBalanceAsync(long userId, long amount, CancellationToken ct = default)
    {
        var next = _balances.GetValueOrDefault(userId, 0) + Math.Max(0, amount);
        _balances[userId] = next;
        return Task.FromResult(next);
    }

    public Task<long?> TrySpendBalanceAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return Task.FromResult<long?>(null);

        var current = _balances.GetValueOrDefault(userId, 0);
        if (amount > current)
            return Task.FromResult<long?>(null);

        _balances[userId] = current - amount;
        return Task.FromResult<long?>(_balances[userId]);
    }
}

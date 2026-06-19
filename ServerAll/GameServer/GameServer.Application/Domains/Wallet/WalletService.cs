using GameServer.Application.Domains.Wallet.Interfaces;

namespace GameServer.Application.Domains.Wallet;

/// <summary>
/// 재화(골드) 서비스 구현. 잔액 증감을 저장소에 위임한다(서버 권위). 골드는 통화 — 카탈로그 검증 없음.
/// 멱등/보상 산정은 호출자(루트·상점) 책임 — 여기선 단건 증감만.
/// </summary>
public sealed class WalletService(IWalletRepository repository) : IWalletService
{
    public Task<long> GetBalanceAsync(long userId, CancellationToken ct = default)
        => repository.GetBalanceAsync(userId, ct);

    public async Task<long> AddAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return await repository.GetBalanceAsync(userId, ct); // 무변동 — 현재 잔액 반환

        return await repository.AddBalanceAsync(userId, amount, ct);
    }

    public async Task<WalletSpendResult> TrySpendAsync(long userId, long amount, CancellationToken ct = default)
    {
        if (amount <= 0)
            return WalletSpendResult.Fail(await repository.GetBalanceAsync(userId, ct), "amount must be positive");

        // 차감은 잔액 검증을 저장소가 원자적으로 수행(부족/없음 → null).
        var remaining = await repository.TrySpendBalanceAsync(userId, amount, ct);
        if (remaining is null)
            return WalletSpendResult.Fail(await repository.GetBalanceAsync(userId, ct), "insufficient balance");

        return WalletSpendResult.Ok(remaining.Value);
    }
}

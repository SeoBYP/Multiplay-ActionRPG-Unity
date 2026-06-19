using GameServer.Application.Domains.Wallet;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class WalletServiceTests
{
    private readonly FakeWalletRepository _repository = new();
    private readonly WalletService _service;

    public WalletServiceTests()
    {
        _service = new WalletService(_repository);
    }

    [Fact]
    public async Task 지갑이_없으면_잔액은_0이다()
    {
        Assert.Equal(0, await _service.GetBalanceAsync(1L));
    }

    [Fact]
    public async Task 골드_적립은_누적되고_적립후_잔액을_반환한다()
    {
        var first = await _service.AddAsync(1L, 100);
        var second = await _service.AddAsync(1L, 50);

        Assert.Equal(100, first);
        Assert.Equal(150, second);
        Assert.Equal(150, await _service.GetBalanceAsync(1L));
    }

    [Fact]
    public async Task 적립_금액이_0이하이면_무변동이고_현재_잔액을_반환한다()
    {
        await _service.AddAsync(1L, 100);

        Assert.Equal(100, await _service.AddAsync(1L, 0));
        Assert.Equal(100, await _service.AddAsync(1L, -50));
        Assert.Equal(100, await _service.GetBalanceAsync(1L));
    }

    [Fact]
    public async Task 잔액_이하_차감은_성공하고_남은_잔액을_반환한다()
    {
        await _service.AddAsync(1L, 100);

        var result = await _service.TrySpendAsync(1L, 30);

        Assert.True(result.Success);
        Assert.Equal(70, result.Balance);
        Assert.Equal(70, await _service.GetBalanceAsync(1L));
    }

    [Fact]
    public async Task 잔액보다_많이_차감하면_실패하고_변화없다()
    {
        await _service.AddAsync(1L, 50);

        var result = await _service.TrySpendAsync(1L, 60);

        Assert.False(result.Success);
        Assert.Equal(50, result.Balance);   // 실패 시 현재 잔액 보고
        Assert.Equal(50, await _service.GetBalanceAsync(1L));
    }

    [Fact]
    public async Task 차감_금액이_0이하이면_실패한다()
    {
        await _service.AddAsync(1L, 50);

        Assert.False((await _service.TrySpendAsync(1L, 0)).Success);
        Assert.False((await _service.TrySpendAsync(1L, -10)).Success);
        Assert.Equal(50, await _service.GetBalanceAsync(1L));
    }
}

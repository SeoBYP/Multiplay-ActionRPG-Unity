using GameServer.Domain.Entities.Wallet;

namespace GameServer.Tests.Domain.Entities;

public class UserWalletTests
{
    [Fact]
    public void Create_하면_시작_잔액으로_시작한다()
    {
        var wallet = UserWallet.Create(userId: 1L, balance: 100);

        Assert.Equal(1L, wallet.UserId);
        Assert.Equal(100, wallet.Balance);
        Assert.True(wallet.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_기본_잔액은_0이다()
    {
        var wallet = UserWallet.Create(1L);

        Assert.Equal(0, wallet.Balance);
    }

    [Fact]
    public void Create_는_잘못된_인자에_예외를_던진다()
    {
        Assert.Throws<ArgumentException>(() => UserWallet.Create(0L));
        Assert.Throws<ArgumentException>(() => UserWallet.Create(-1L));
        Assert.Throws<ArgumentException>(() => UserWallet.Create(1L, -10));
    }

    [Fact]
    public void 잔액을_더하면_누적된다()
    {
        var wallet = UserWallet.Create(1L, 50);

        wallet.Add(30);
        wallet.Add(20);

        Assert.Equal(100, wallet.Balance);
    }

    [Fact]
    public void 더하는_금액이_0이하이면_무시된다()
    {
        var wallet = UserWallet.Create(1L, 50);

        wallet.Add(0);
        wallet.Add(-10);

        Assert.Equal(50, wallet.Balance);
    }

    [Fact]
    public void 보유_이하_차감은_성공하고_잔액이_준다()
    {
        var wallet = UserWallet.Create(1L, 100);

        Assert.True(wallet.TrySpend(30));
        Assert.Equal(70, wallet.Balance);
    }

    [Fact]
    public void 잔액보다_많이_차감하면_실패하고_변화없다()
    {
        var wallet = UserWallet.Create(1L, 50);

        Assert.False(wallet.TrySpend(60));
        Assert.Equal(50, wallet.Balance);
    }

    [Fact]
    public void 차감_금액이_0이하이면_실패한다()
    {
        var wallet = UserWallet.Create(1L, 50);

        Assert.False(wallet.TrySpend(0));
        Assert.False(wallet.TrySpend(-5));
        Assert.Equal(50, wallet.Balance);
    }
}

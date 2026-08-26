using Server.Loot;

namespace Server.Tests.Loot;

/// <summary>
/// 바닥 아이템 저장소 — 스폰·조회·<b>줍기 경쟁 중재</b>.
///
/// <para>예전엔 이 셋이 Room 안에 있어서 줍기 한 줄을 검증하려면 방·세션·플레이어를 다 세워야 했다.
/// 저장소가 위치를 <b>인자로</b> 받게 되면서 방 없이 직접 칠 수 있다.</para>
/// </summary>
public class GroundItemStoreTests
{
    [Fact]
    public void GroundId는_1부터_순차_발급된다()
    {
        var store = new GroundItemStore();

        var a = store.Spawn(itemId: 1001, qty: 1, 0f, 0f, 0f);
        var b = store.Spawn(itemId: 1002, qty: 3, 1f, 0f, 1f);

        Assert.Equal(1, a.GroundId);
        Assert.Equal(2, b.GroundId);
        Assert.Equal(1002, b.ItemId);
        Assert.Equal(3, b.Qty);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void 사거리_안에서_주우면_아이템을_가져가고_바닥에서_사라진다()
    {
        var store = new GroundItemStore();
        var item = store.Spawn(1001, 1, x: 5f, y: 0f, z: 5f);

        var picked = store.TryPickup(pickerX: 5f, pickerZ: 6f, groundId: item.GroundId); // 1m

        Assert.NotNull(picked);
        Assert.Equal(item.GroundId, picked!.GroundId);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void 사거리_밖에서는_줍지_못하고_바닥에_남는다()
    {
        var store = new GroundItemStore();
        var item = store.Spawn(1001, 1, 0f, 0f, 0f);

        float outOfRange = GroundItemStore.PickupRange + 0.1f;
        Assert.Null(store.TryPickup(outOfRange, 0f, item.GroundId));
        Assert.Equal(1, store.Count); // 실패해도 사라지지 않는다
    }

    [Fact]
    public void 사거리_경계는_포함이다()
    {
        var store = new GroundItemStore();
        var item = store.Spawn(1001, 1, 0f, 0f, 0f);

        Assert.NotNull(store.TryPickup(GroundItemStore.PickupRange, 0f, item.GroundId));
    }

    [Fact]
    public void 같은_아이템을_두_번_주울_수_없다_경쟁_중재()
    {
        // 동시 픽업의 본질 = "제거에 성공한 1명만 가져간다". 순차로도 같은 불변식이 성립해야 한다.
        var store = new GroundItemStore();
        var item = store.Spawn(1001, 1, 0f, 0f, 0f);

        Assert.NotNull(store.TryPickup(0f, 0f, item.GroundId));
        Assert.Null(store.TryPickup(0f, 0f, item.GroundId)); // 경쟁 패배
    }

    [Fact]
    public void 동시에_주워도_정확히_한_명만_성공한다()
    {
        var store = new GroundItemStore();
        var item = store.Spawn(1001, 1, 0f, 0f, 0f);

        int winners = 0;
        Parallel.For(0, 64, _ =>
        {
            if (store.TryPickup(0f, 0f, item.GroundId) is not null)
                Interlocked.Increment(ref winners);
        });

        Assert.Equal(1, winners);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void 없는_GroundId는_null이다()
    {
        var store = new GroundItemStore();

        Assert.Null(store.TryPickup(0f, 0f, groundId: 999));
    }

    [Fact]
    public void All은_현재_바닥_전체를_낸다()
    {
        var store = new GroundItemStore();
        store.Spawn(1001, 1, 0f, 0f, 0f);
        var second = store.Spawn(1002, 1, 1f, 0f, 1f);
        store.TryPickup(1f, 1f, second.GroundId);

        var all = store.All();

        Assert.Single(all);
        Assert.Equal(1001, all[0].ItemId);
    }
}

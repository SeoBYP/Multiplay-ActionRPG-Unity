using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Shop;
using Game.System.Shop;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using PresShopCategory = Game.Presentation.Shop.ShopCategory;
using SysShopCategory = Game.System.Shop.ShopCategory;

namespace Game.Tests.PlayMode.Shop
{
    /// <summary>
    /// 클라 상점 Model(MVI) 로직 검증 — Docker 불필요(Fake 서비스).
    /// 진열 로드, 구매 성공/실패 토스트, 선택·수량 클램프, 탭 선택.
    /// </summary>
    [TestFixture]
    public class ShopModelTests
    {
        private sealed class FakeShopService : IShopService
        {
            private readonly ShopResult _getResult;
            private readonly IReadOnlyList<ShopItemData> _items;
            private readonly ShopResult _buyResult;
            private readonly long _buyGold;
            private readonly int _buyQty;

            public int BuyCallCount { get; private set; }
            public string LastBoughtItemId { get; private set; }

            public FakeShopService(ShopResult getResult, IReadOnlyList<ShopItemData> items,
                ShopResult buyResult = ShopResult.Success, long buyGold = 0, int buyQty = 0)
            {
                _getResult = getResult;
                _items = items;
                _buyResult = buyResult;
                _buyGold = buyGold;
                _buyQty = buyQty;
            }

            public UniTask<(ShopResult Result, IReadOnlyList<ShopItemData> Items)> GetShopAsync(CancellationToken ct = default)
                => UniTask.FromResult<(ShopResult, IReadOnlyList<ShopItemData>)>(
                    (_getResult, _getResult == ShopResult.Success ? _items : Array.Empty<ShopItemData>()));

            public UniTask<(ShopResult Result, long Gold, int NewQuantity)> BuyAsync(string itemId, int qty, CancellationToken ct = default)
            {
                BuyCallCount++;
                LastBoughtItemId = itemId;
                return UniTask.FromResult((_buyResult, _buyGold, _buyQty));
            }

            public UniTask<(ShopResult Result, long Gold, int RemainingQuantity)> SellAsync(string itemId, int qty, CancellationToken ct = default)
                => UniTask.FromResult((ShopResult.Success, 0L, 0));
        }

        private static List<ShopItemData> Sample() => new()
        {
            new ShopItemData("sword_basic", 200, 50, SysShopCategory.Weapon, Array.Empty<ShopStatData>()),
            new ShopItemData("potion_hp_small", 50, 10, SysShopCategory.Potion, Array.Empty<ShopStatData>()),
        };

        private static async UniTask Settle()
        {
            await UniTask.Yield();
            await UniTask.Yield();
            await UniTask.Yield();
        }

        [UnityTest]
        public IEnumerator Refresh_하면_진열이_State에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            var model = new ShopModel(new FakeShopService(ShopResult.Success, Sample()));
            ShopState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(ShopIntent.Refresh.Instance);
            await Settle();

            Assert.AreEqual(2, latest.Items.Count);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 구매_성공하면_골드갱신되고_성공토스트가_발행된다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeShopService(ShopResult.Success, Sample(), buyResult: ShopResult.Success, buyGold: 800, buyQty: 1);
            var model = new ShopModel(fake);
            ShopState latest = null;
            ShopToastMessage? toast = null;
            using var sub = model.State.Subscribe(s => latest = s);
            using var toastSub = model.OnToast.Subscribe(t => toast = t);

            model.Accept(ShopIntent.Refresh.Instance);
            await Settle();
            model.Accept(new ShopIntent.SelectItem("sword_basic"));
            model.Accept(ShopIntent.Buy.Instance);
            await Settle();

            Assert.AreEqual(1, fake.BuyCallCount);
            Assert.AreEqual("sword_basic", fake.LastBoughtItemId);
            Assert.IsTrue(toast.HasValue);
            Assert.IsTrue(toast.Value.Success);          // 성공 토스트
            Assert.AreEqual(800, latest.Gold);           // 서버 권위 잔액 반영

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 구매_실패하면_실패토스트가_발행되고_골드는_불변() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeShopService(ShopResult.Success, Sample(), buyResult: ShopResult.Failed);
            var model = new ShopModel(fake);
            ShopState latest = null;
            ShopToastMessage? toast = null;
            using var sub = model.State.Subscribe(s => latest = s);
            using var toastSub = model.OnToast.Subscribe(t => toast = t);

            model.Accept(ShopIntent.Refresh.Instance);
            await Settle();
            model.Accept(new ShopIntent.SelectItem("potion_hp_small"));
            model.Accept(ShopIntent.Buy.Instance);
            await Settle();

            Assert.IsTrue(toast.HasValue);
            Assert.IsFalse(toast.Value.Success);         // 실패 토스트
            Assert.AreEqual(0, latest.Gold);             // 차감/갱신 없음

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 아이템을_선택하지_않고_구매하면_실패토스트이고_BuyAsync_미호출() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeShopService(ShopResult.Success, Sample());
            var model = new ShopModel(fake);
            ShopToastMessage? toast = null;
            using var toastSub = model.OnToast.Subscribe(t => toast = t);

            model.Accept(ShopIntent.Refresh.Instance);
            await Settle();
            model.Accept(ShopIntent.Buy.Instance); // 선택 없이 구매
            await Settle();

            Assert.AreEqual(0, fake.BuyCallCount);       // 서버 호출 안 함
            Assert.IsTrue(toast.HasValue);
            Assert.IsFalse(toast.Value.Success);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator 아이템_선택하면_선택되고_수량은_1로_초기화된다() => UniTask.ToCoroutine(async () =>
        {
            var model = new ShopModel(new FakeShopService(ShopResult.Success, Sample()));
            ShopState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(ShopIntent.Refresh.Instance);
            await Settle();
            model.Accept(new ShopIntent.SetQuantity(5));
            model.Accept(new ShopIntent.SelectItem("sword_basic"));

            Assert.AreEqual("sword_basic", latest.SelectedItemId);
            Assert.AreEqual(1, latest.Quantity); // 선택 시 1로 초기화

            model.Dispose();
            await UniTask.CompletedTask;
        });

        [UnityTest]
        public IEnumerator 수량은_1미만으로_내려가지_않는다() => UniTask.ToCoroutine(async () =>
        {
            var model = new ShopModel(new FakeShopService(ShopResult.Success, Sample()));
            ShopState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(new ShopIntent.SetQuantity(0));
            Assert.AreEqual(1, latest.Quantity);
            model.Accept(new ShopIntent.SetQuantity(-3));
            Assert.AreEqual(1, latest.Quantity);
            model.Accept(new ShopIntent.SetQuantity(7));
            Assert.AreEqual(7, latest.Quantity);

            model.Dispose();
            await UniTask.CompletedTask;
        });

        [UnityTest]
        public IEnumerator 탭_선택하면_SelectedCategory가_바뀐다() => UniTask.ToCoroutine(async () =>
        {
            var model = new ShopModel(new FakeShopService(ShopResult.Success, Sample()));
            ShopState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(new ShopIntent.SelectTab(PresShopCategory.Weapon));
            Assert.AreEqual(PresShopCategory.Weapon, latest.SelectedCategory);

            model.Accept(new ShopIntent.SelectTab(null)); // 전체
            Assert.IsNull(latest.SelectedCategory);

            model.Dispose();
            await UniTask.CompletedTask;
        });
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Inventory;
using Game.System.Inventory;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Inventory
{
    /// <summary>
    /// 클라 인벤토리 Model(MVI) 로직 검증 — Docker 불필요(Fake 서비스).
    /// Refresh→State 반영, 카탈로그 없을 때 폴백, 탭 선택, 실패 처리.
    /// </summary>
    [TestFixture]
    public class InventoryModelTests
    {
        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly InventoryResult _result;
            private readonly IReadOnlyList<InventoryItemData> _items;

            public FakeInventoryService(InventoryResult result, IReadOnlyList<InventoryItemData> items)
            {
                _result = result;
                _items = items;
            }

            public UniTask<(InventoryResult Result, IReadOnlyList<InventoryItemData> Items)> GetInventoryAsync(CancellationToken ct = default)
                => UniTask.FromResult((_result, _items));
        }

        [UnityTest]
        public IEnumerator Refresh_하면_서비스_아이템이_State에_반영된다() => UniTask.ToCoroutine(async () =>
        {
            var items = new List<InventoryItemData>
            {
                new InventoryItemData("potion_hp_small", 3),
                new InventoryItemData("sword_iron", 1),
            };
            var model = new InventoryModel(new FakeInventoryService(InventoryResult.Success, items), catalog: null);

            InventoryState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(InventoryIntent.Refresh.Instance);
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(2, latest.Items.Count);
            Assert.AreEqual("potion_hp_small", latest.Items[0].ItemId);
            Assert.AreEqual(3, latest.Items[0].Quantity);
            // 카탈로그 없음 → 폴백: 이름=itemId, 분류=Etc.
            Assert.AreEqual("potion_hp_small", latest.Items[0].DisplayName);
            Assert.AreEqual(ItemCategory.Etc, latest.Items[0].Category);

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator SelectTab_하면_SelectedCategory가_바뀐다() => UniTask.ToCoroutine(async () =>
        {
            var model = new InventoryModel(
                new FakeInventoryService(InventoryResult.Success, Array.Empty<InventoryItemData>()), catalog: null);

            InventoryState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(new InventoryIntent.SelectTab(ItemCategory.Consumable));
            Assert.AreEqual(ItemCategory.Consumable, latest.SelectedCategory);

            model.Accept(new InventoryIntent.SelectTab(null)); // All
            Assert.IsNull(latest.SelectedCategory);

            model.Dispose();
            await UniTask.CompletedTask;
        });

        [UnityTest]
        public IEnumerator 서비스_실패면_Error가_설정된다() => UniTask.ToCoroutine(async () =>
        {
            var model = new InventoryModel(
                new FakeInventoryService(InventoryResult.Failed, Array.Empty<InventoryItemData>()), catalog: null);

            InventoryState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(InventoryIntent.Refresh.Instance);
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.IsNotNull(latest.Error);
            Assert.AreEqual(0, latest.Items.Count);

            model.Dispose();
        });
    }
}

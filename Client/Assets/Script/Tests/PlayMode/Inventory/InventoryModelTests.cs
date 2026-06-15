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
            private readonly InventoryResult _consumeResult;

            public string LastConsumedItemId { get; private set; }
            public int ConsumeCallCount { get; private set; }

            public FakeInventoryService(
                InventoryResult result,
                IReadOnlyList<InventoryItemData> items,
                InventoryResult consumeResult = InventoryResult.Success)
            {
                _result = result;
                _items = items;
                _consumeResult = consumeResult;
            }

            public UniTask<(InventoryResult Result, IReadOnlyList<InventoryItemData> Items)> GetInventoryAsync(CancellationToken ct = default)
                => UniTask.FromResult((_result, _items));

            public UniTask<(InventoryResult Result, int Remaining)> ConsumeItemAsync(string itemId, int qty, CancellationToken ct = default)
            {
                ConsumeCallCount++;
                LastConsumedItemId = itemId;
                return UniTask.FromResult((_consumeResult, _consumeResult == InventoryResult.Success ? 0 : 0));
            }
        }

        /// <summary>착용 세트를 미리 세팅한 Fake 장비 서비스(필터링 검증용).</summary>
        private sealed class FakeEquipmentService : Game.System.Equipment.IEquipmentService
        {
            private readonly List<Game.System.Equipment.EquippedItemData> _equipped = new();
#pragma warning disable CS0067
            public event Action OnChanged;
#pragma warning restore CS0067
            public FakeEquipmentService(params string[] equippedItemIds)
            {
                foreach (var id in equippedItemIds)
                    _equipped.Add(new Game.System.Equipment.EquippedItemData(Shared.Gameplay.Equipment.EquipmentType.Weapon, id));
            }

            public UniTask<(Game.System.Equipment.EquipmentResult Result, IReadOnlyList<Game.System.Equipment.EquippedItemData> Items)> GetEquippedAsync(CancellationToken ct = default)
                => UniTask.FromResult((Game.System.Equipment.EquipmentResult.Success, (IReadOnlyList<Game.System.Equipment.EquippedItemData>)_equipped));

            public UniTask<(Game.System.Equipment.EquipmentResult Result, Shared.Gameplay.Equipment.EquipmentType Slot)> EquipAsync(string itemId, CancellationToken ct = default)
                => UniTask.FromResult((Game.System.Equipment.EquipmentResult.Success, Shared.Gameplay.Equipment.EquipmentType.Weapon));

            public UniTask<Game.System.Equipment.EquipmentResult> UnequipAsync(Shared.Gameplay.Equipment.EquipmentType slot, CancellationToken ct = default)
                => UniTask.FromResult(Game.System.Equipment.EquipmentResult.Success);
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
        public IEnumerator Refresh_시_착용중인_아이템은_인벤토리에서_제외된다() => UniTask.ToCoroutine(async () =>
        {
            // sword_basic 은 장착 중 → 인벤토리 표시에서 제외(장비창에 나타남). potion 만 남는다.
            var items = new List<InventoryItemData>
            {
                new InventoryItemData("potion_hp_small", 3),
                new InventoryItemData("sword_basic", 1),
            };
            var model = new InventoryModel(
                new FakeInventoryService(InventoryResult.Success, items),
                new FakeEquipmentService("sword_basic"),
                inputContext: null, catalog: null);

            InventoryState latest = null;
            using var sub = model.State.Subscribe(s => latest = s);

            model.Accept(InventoryIntent.Refresh.Instance);
            await UniTask.Yield();
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(1, latest.Items.Count);
            Assert.AreEqual("potion_hp_small", latest.Items[0].ItemId);

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

        [UnityTest]
        public IEnumerator UseItem_차감성공하면_OnConsumableUsed와_OnToast가_발행된다() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeInventoryService(
                InventoryResult.Success, Array.Empty<InventoryItemData>(), consumeResult: InventoryResult.Success);
            var model = new InventoryModel(fake, catalog: null);

            string used = null;
            string toast = null;
            using var u = model.OnConsumableUsed.Subscribe(id => used = id);
            using var t = model.OnToast.Subscribe(msg => toast = msg);

            model.Accept(new InventoryIntent.UseItem("potion_hp_small"));
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual("potion_hp_small", fake.LastConsumedItemId); // consume 먼저
            Assert.AreEqual("potion_hp_small", used);                    // Side Effect A
            Assert.IsNotNull(toast);                                     // Side Effect B

            model.Dispose();
        });

        [UnityTest]
        public IEnumerator UseItem_차감실패하면_OnConsumableUsed는_발행되지_않고_실패토스트만() => UniTask.ToCoroutine(async () =>
        {
            var fake = new FakeInventoryService(
                InventoryResult.Success, Array.Empty<InventoryItemData>(), consumeResult: InventoryResult.Failed);
            var model = new InventoryModel(fake, catalog: null);

            bool usedFired = false;
            string toast = null;
            using var u = model.OnConsumableUsed.Subscribe(_ => usedFired = true);
            using var t = model.OnToast.Subscribe(msg => toast = msg);

            model.Accept(new InventoryIntent.UseItem("potion_hp_small"));
            await UniTask.Yield();
            await UniTask.Yield();

            Assert.AreEqual(1, fake.ConsumeCallCount);
            Assert.IsFalse(usedFired, "차감 실패인데 회복 Side Effect가 발행됨");
            Assert.IsNotNull(toast, "실패 토스트가 없음");

            model.Dispose();
        });
    }
}

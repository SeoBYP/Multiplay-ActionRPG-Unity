using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Inventory;
using R3;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 인벤토리 화면의 MVI Model.
    ///   View는 Accept(Intent)만 호출하고, Model은 State만 발행한다.
    ///   itemId → 표시데이터 합성은 ItemDisplayCatalog가 담당.
    /// GUI(Inventory View)는 이 Model만 주입받는다(IInventoryService·proto 비노출).
    /// </summary>
    public sealed class InventoryModel : IDisposable
    {
        private readonly IInventoryService _service;
        private readonly ItemDisplayCatalog _catalog;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<InventoryState> _state = new(InventoryState.Initial);
        public ReadOnlyReactiveProperty<InventoryState> State => _state.ToReadOnlyReactiveProperty();

        public InventoryModel(IInventoryService service, ItemDisplayCatalog catalog = null)
        {
            _service = service;
            _catalog = catalog;
        }

        public void Accept(InventoryIntent intent)
        {
            switch (intent)
            {
                case InventoryIntent.Refresh:
                    RefreshAsync().Forget();
                    break;
                case InventoryIntent.SelectTab tab:
                    _state.Value = _state.Value.WithSelectedCategory(tab.Category);
                    break;
            }
        }

        private async UniTaskVoid RefreshAsync()
        {
            _state.Value = _state.Value.WithLoading();
            try
            {
                var (result, items) = await _service.GetInventoryAsync(_cts.Token);
                if (result != InventoryResult.Success)
                {
                    _state.Value = _state.Value.WithError(result.ToString());
                    return;
                }

                var models = new List<InventoryItemModel>(items.Count);
                foreach (var data in items)
                {
                    var entry = _catalog != null ? _catalog.Get(data.ItemId) : null;
                    models.Add(new InventoryItemModel(
                        data.ItemId,
                        data.Quantity,
                        entry?.displayName ?? data.ItemId,
                        entry?.icon,
                        entry?.category ?? ItemCategory.Etc));
                }

                _state.Value = _state.Value.WithItems(models);
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}

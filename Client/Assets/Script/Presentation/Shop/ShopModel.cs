using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Inventory;
using Game.System.Input;
using Game.System.Shop;
using Game.System.Wallet;
using R3;
using SystemShopCategory = Game.System.Shop.ShopCategory;

namespace Game.Presentation.Shop
{
    /// <summary>
    /// 상점 화면의 MVI Model. View는 Accept(Intent)만 호출하고 Model은 State만 발행한다(InventoryModel 동형).
    /// 구매=서버 권위(IShopService.BuyAsync) → 성공 시 골드/토스트 갱신. 가격·검증은 서버.
    /// GUI(Shop View)는 이 Model만 주입받는다(IShopService·IWalletService·proto 비노출).
    /// </summary>
    public sealed class ShopModel : IDisposable
    {
        private readonly IShopService _shop;
        private readonly IWalletService _wallet;
        private readonly IInputContext _inputContext;
        private readonly ItemDisplayCatalog _catalog;
        private readonly GradeSpriteCatalog _gradeCatalog;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<ShopState> _state = new(ShopState.Initial);
        public ReadOnlyReactiveProperty<ShopState> State => _state.ToReadOnlyReactiveProperty();

        private readonly Subject<ShopToastMessage> _onToast = new();
        /// <summary>일회성 토스트(구매 성공/실패 + 결과 플래그) — View 가 색 구분 표시 후 종료.</summary>
        public Observable<ShopToastMessage> OnToast => _onToast;

        public ShopModel(IShopService shop, IWalletService wallet = null,
            IInputContext inputContext = null, ItemDisplayCatalog catalog = null,
            GradeSpriteCatalog gradeCatalog = null)
        {
            _shop = shop;
            _wallet = wallet;
            _inputContext = inputContext;
            _catalog = catalog;
            _gradeCatalog = gradeCatalog;
        }

        // 창이 열린 동안 게임플레이 입력 점유(InventoryModel 과 동일 — IInputContext refcount).
        public void BeginUiCapture() => _inputContext?.EnterUi();
        public void EndUiCapture() => _inputContext?.ExitUi();

        public void Accept(ShopIntent intent)
        {
            switch (intent)
            {
                case ShopIntent.Refresh:
                    RefreshAsync().Forget();
                    break;
                case ShopIntent.SelectTab tab:
                    _state.Value = _state.Value.With(selectedCategory: tab.Category, clearCategory: tab.Category == null);
                    break;
                case ShopIntent.SelectItem select:
                    _state.Value = _state.Value.With(selectedItemId: select.ItemId, quantity: 1);
                    break;
                case ShopIntent.SetQuantity qty:
                    _state.Value = _state.Value.With(quantity: Math.Max(1, qty.Quantity));
                    break;
                case ShopIntent.Buy:
                    BuyAsync().Forget();
                    break;
            }
        }

        private async UniTaskVoid RefreshAsync()
        {
            _state.Value = _state.Value.With(isLoading: true, clearError: true);
            try
            {
                var (result, items) = await _shop.GetShopAsync(_cts.Token);
                if (result != ShopResult.Success)
                {
                    _state.Value = _state.Value.With(isLoading: false, error: result.ToString());
                    return;
                }

                var models = new List<ShopItemModel>(items.Count);
                foreach (var data in items)
                {
                    var entry = _catalog != null ? _catalog.Get(data.ItemId) : null;
                    var stats = new List<ShopStatLine>(data.Stats.Count);
                    foreach (var s in data.Stats)
                        stats.Add(new ShopStatLine(s.Stat, s.Amount));

                    var gradeBg = _gradeCatalog != null && entry != null ? _gradeCatalog.Get(entry.grade) : null;
                    models.Add(new ShopItemModel(
                        data.ItemId,
                        entry?.displayName ?? data.ItemId,
                        entry?.icon,
                        data.BuyPrice,
                        data.SellPrice,
                        ToCategory(data.Category),
                        stats,
                        gradeBg,
                        entry?.description));
                }

                long gold = await GetGoldAsync();
                _state.Value = _state.Value.With(items: models, gold: gold, isLoading: false, clearError: true);
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        private async UniTaskVoid BuyAsync()
        {
            var state = _state.Value;
            var selected = state.Selected;
            if (selected == null)
            {
                _onToast.OnNext(new ShopToastMessage("아이템을 선택하세요", false));
                return;
            }

            try
            {
                var (result, gold, _) = await _shop.BuyAsync(selected.ItemId, state.Quantity, _cts.Token);
                if (result != ShopResult.Success)
                {
                    // 실패 사유는 서버 권위(골드 부족·구매 불가 등). 클라가 단정하지 않고 일반 메시지로 안내.
                    _onToast.OnNext(new ShopToastMessage("구매에 실패했습니다. 골드 또는 구매 조건을 확인하세요.", false));
                    return;
                }

                _onToast.OnNext(new ShopToastMessage($"{selected.DisplayName} {state.Quantity}개 구매 완료", true));
                _state.Value = _state.Value.With(gold: gold); // 서버 권위 잔액 반영
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        private async UniTask<long> GetGoldAsync()
        {
            if (_wallet == null)
                return _state.Value.Gold;

            var (result, gold) = await _wallet.GetWalletAsync(_cts.Token);
            return result == WalletResult.Success ? gold : _state.Value.Gold;
        }

        private static ShopCategory ToCategory(SystemShopCategory c) => c switch
        {
            SystemShopCategory.Weapon => ShopCategory.Weapon,
            SystemShopCategory.Armor => ShopCategory.Armor,
            SystemShopCategory.Accessory => ShopCategory.Accessory,
            SystemShopCategory.Potion => ShopCategory.Potion,
            _ => ShopCategory.Unspecified,
        };

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _onToast.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.Equipment;
using Game.System.Input;
using Game.System.Inventory;
using Game.System.Shop;
using Game.System.Wallet;
using R3;

namespace Game.Presentation.Inventory
{
    /// <summary>일회성 인벤토리 토스트(성공/실패). 실패는 View 가 팝업으로 띄운다(Shop 의 ShopToastMessage 동형).</summary>
    public readonly struct InventoryToast
    {
        public readonly string Message;
        public readonly bool Success;
        public InventoryToast(string message, bool success) { Message = message; Success = success; }
    }

    /// <summary>
    /// 인벤토리 화면의 MVI Model.
    ///   View는 Accept(Intent)만 호출하고, Model은 State만 발행한다.
    ///   itemId → 표시데이터 합성은 ItemDisplayCatalog가 담당.
    /// GUI(Inventory View)는 이 Model만 주입받는다(IInventoryService·proto 비노출).
    /// </summary>
    public sealed class InventoryModel : IDisposable
    {
        private readonly IInventoryService _service;
        private readonly IEquipmentService _equipment;
        private readonly IWalletService _wallet;
        private readonly IShopService _shop;
        private readonly IInputContext _inputContext;
        private readonly ItemDisplayCatalog _catalog;
        private readonly GradeSpriteCatalog _gradeCatalog;
        private readonly CancellationTokenSource _cts = new();

        // 판매가 캐시(itemId→sell_price). 서버 GetShop 1회 결과를 보관 — 확인 팝업에 표시. null=미조회.
        private Dictionary<int, long> _sellPrices;

        private readonly ReactiveProperty<InventoryState> _state = new(InventoryState.Initial);
        public ReadOnlyReactiveProperty<InventoryState> State => _state.ToReadOnlyReactiveProperty();

        // ── Side Effect 채널 (State 아님 — 일회성 외부 효과) ──
        // 차감 성공 후에만 발행. LobbyModel.NavigateToRoom 과 동일 패턴(Subject→Observable, 구독자가 처리).
        private readonly Subject<int> _onConsumableUsed = new(); // itemId → 회복 핸들러가 GAS 적용
        private readonly Subject<InventoryToast> _onToast = new();   // 토스트(성공/실패) → View 표시(실패=팝업)

        /// <summary>소모품 사용 확정(차감 성공) — 구독자가 회복 효과를 ASC 에 적용(클라 권위).</summary>
        public Observable<int> OnConsumableUsed => _onConsumableUsed;

        /// <summary>일회성 토스트(성공/실패) — View 가 표시(실패는 팝업), 사이클 종료.</summary>
        public Observable<InventoryToast> OnToast => _onToast;

        public InventoryModel(IInventoryService service, IEquipmentService equipment = null,
            IInputContext inputContext = null, ItemDisplayCatalog catalog = null, IWalletService wallet = null,
            GradeSpriteCatalog gradeCatalog = null, IShopService shop = null)
        {
            _service = service;
            _equipment = equipment;
            _wallet = wallet;
            _shop = shop;
            _inputContext = inputContext;
            _catalog = catalog;
            _gradeCatalog = gradeCatalog;

            // 장착/해제 성공 시 인벤토리도 갱신 → 착용분 숨김/복원이 즉시 반영(EquipmentModel 과 동시).
            if (_equipment != null)
                _equipment.OnChanged += OnEquipmentChanged;
        }

        private void OnEquipmentChanged() => RefreshAsync().Forget();

        // 창이 열린 동안 게임플레이(Player) 입력 점유 — View의 UiInputCaptureBehaviour가 활성/비활성 시 호출.
        // 실제 토글은 IInputContext(refcount)가 담당. GUI는 System을 직접 모르고 이 Model 메서드만 넘긴다.
        public void BeginUiCapture() => _inputContext?.EnterUi();
        public void EndUiCapture() => _inputContext?.ExitUi();

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
                case InventoryIntent.UseItem use:
                    UseItemAsync(use.ItemId).Forget();
                    break;
                case InventoryIntent.EquipItem equip:
                    EquipItemAsync(equip.ItemId).Forget();
                    break;
                case InventoryIntent.SellItem sell:
                    SellItemAsync(sell.ItemId).Forget();
                    break;
            }
        }

        /// <summary>판매가(서버 sell_price) 조회 — 확인 팝업 표시용. GetShop 1회 캐시 후 룩업. 미주입/미조회면 0.</summary>
        public async UniTask<long> GetSellPriceAsync(int itemId)
        {
            if (_shop == null) return 0;
            if (_sellPrices == null)
            {
                var (result, items) = await _shop.GetShopAsync(_cts.Token);
                _sellPrices = new Dictionary<int, long>();
                if (result == ShopResult.Success)
                    foreach (var it in items)
                        _sellPrices[it.ItemId] = it.SellPrice;
            }
            return _sellPrices != null && _sellPrices.TryGetValue(itemId, out var price) ? price : 0;
        }

        /// <summary>판매(서버 권위, 1개): Sell → 성공 시 인벤/골드 갱신 + 토스트. 가격 확인은 View 가 먼저 처리.</summary>
        private async UniTaskVoid SellItemAsync(int itemId)
        {
            if (_shop == null)
            {
                _onToast.OnNext(new InventoryToast("판매할 수 없습니다", false));
                return;
            }

            try
            {
                var (result, _, _) = await _shop.SellAsync(itemId, 1, _cts.Token);
                if (result != ShopResult.Success)
                {
                    _onToast.OnNext(new InventoryToast("판매에 실패했습니다", false));
                    return;
                }

                _onToast.OnNext(new InventoryToast($"{DisplayName(itemId)} 판매", true));
                RefreshAsync().Forget(); // 남은 수량 + 골드 갱신
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        /// <summary>
        /// 장비 장착: IEquipmentService.EquipAsync(서버 권위, 슬롯은 서버 결정) → 성공 시 토스트.
        /// 장비창 갱신은 IEquipmentService.OnChanged 가 EquipmentModel 에 통지(여기서 직접 안 건드림).
        /// </summary>
        private async UniTaskVoid EquipItemAsync(int itemId)
        {
            if (_equipment == null)
            {
                _onToast.OnNext(new InventoryToast("장비 시스템이 없습니다", false));
                return;
            }

            try
            {
                var (result, _) = await _equipment.EquipAsync(itemId, _cts.Token);
                _onToast.OnNext(result == EquipmentResult.Success
                    ? new InventoryToast($"{DisplayName(itemId)} 장착", true)
                    : new InventoryToast("장착할 수 없습니다", false));
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        /// <summary>
        /// 소모품 사용: ① consume(서버 권위 차감) → ② 성공 시에만 Side Effect 발행(회복 신호 + 토스트) → ③ 수량 갱신.
        /// 회복 자체는 OnConsumableUsed 구독자(ConsumableEffectHandler)가 GAS 로 적용한다(Model 은 신호만).
        /// </summary>
        private async UniTaskVoid UseItemAsync(int itemId)
        {
            try
            {
                var (result, _) = await _service.ConsumeItemAsync(itemId, 1, _cts.Token);
                if (result != InventoryResult.Success)
                {
                    _onToast.OnNext(new InventoryToast("사용할 수 없습니다", false));   // 실패 — 회복 안 함
                    return;
                }

                _onConsumableUsed.OnNext(itemId);            // Side Effect A: 회복 적용 트리거(구독자→GAS)
                _onToast.OnNext(new InventoryToast($"{DisplayName(itemId)} 사용", true)); // Side Effect B: 토스트
                RefreshAsync().Forget();                     // State: 남은 수량 갱신(비동기)
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        private string DisplayName(int itemId)
            => (_catalog != null ? _catalog.Get(itemId)?.displayName : null) ?? itemId.ToString();

        /// <summary>현재 골드 잔액(지갑 미주입이면 기존 상태값 유지 — null-safe).</summary>
        private async UniTask<long> GetGoldAsync()
        {
            if (_wallet == null)
                return _state.Value.Gold;

            var (result, gold) = await _wallet.GetWalletAsync(_cts.Token);
            return result == WalletResult.Success ? gold : _state.Value.Gold;
        }

        /// <summary>현재 착용 중인 itemId 집합(없거나 장비 시스템 미주입이면 빈 집합).</summary>
        private async UniTask<HashSet<int>> GetEquippedIdsAsync()
        {
            var ids = new HashSet<int>();
            if (_equipment == null)
                return ids;

            var (result, equipped) = await _equipment.GetEquippedAsync(_cts.Token);
            if (result == EquipmentResult.Success)
                foreach (var e in equipped)
                    ids.Add(e.ItemId);
            return ids;
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

                // 착용 중인 아이템은 인벤토리 표시에서 제외(장비창에 나타남). 장비=스택1이라 itemId 매칭으로 충분.
                var equippedIds = await GetEquippedIdsAsync();

                var models = new List<InventoryItemModel>(items.Count);
                foreach (var data in items)
                {
                    if (equippedIds.Contains(data.ItemId))
                        continue;

                    var entry = _catalog != null ? _catalog.Get(data.ItemId) : null;
                    var grade = entry?.grade ?? ItemGrade.Common;
                    models.Add(new InventoryItemModel(
                        data.ItemId,
                        data.Quantity,
                        entry?.displayName ?? data.ItemId.ToString(),
                        entry?.icon,
                        entry?.category ?? ItemCategory.Etc,
                        grade,
                        _gradeCatalog != null ? _gradeCatalog.Get(grade) : null));
                }

                // 골드 잔액(지갑)도 함께 로드 — 인벤토리 화면에 표시. 지갑 미주입 시 기존값 유지(null-safe).
                var gold = await GetGoldAsync();

                _state.Value = _state.Value.WithItems(models).WithGold(gold);
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        public void Dispose()
        {
            if (_equipment != null)
                _equipment.OnChanged -= OnEquipmentChanged;
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
            _onConsumableUsed.Dispose();
            _onToast.Dispose();
        }
    }
}

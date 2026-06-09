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

        // ── Side Effect 채널 (State 아님 — 일회성 외부 효과) ──
        // 차감 성공 후에만 발행. LobbyModel.NavigateToRoom 과 동일 패턴(Subject→Observable, 구독자가 처리).
        private readonly Subject<string> _onConsumableUsed = new(); // itemId → 회복 핸들러가 GAS 적용
        private readonly Subject<string> _onToast = new();          // 토스트 메시지 → View 표시 후 종료

        /// <summary>소모품 사용 확정(차감 성공) — 구독자가 회복 효과를 ASC 에 적용(클라 권위).</summary>
        public Observable<string> OnConsumableUsed => _onConsumableUsed;

        /// <summary>일회성 토스트 메시지(사용/실패) — View 가 표시, 사이클 종료.</summary>
        public Observable<string> OnToast => _onToast;

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
                case InventoryIntent.UseItem use:
                    UseItemAsync(use.ItemId).Forget();
                    break;
            }
        }

        /// <summary>
        /// 소모품 사용: ① consume(서버 권위 차감) → ② 성공 시에만 Side Effect 발행(회복 신호 + 토스트) → ③ 수량 갱신.
        /// 회복 자체는 OnConsumableUsed 구독자(ConsumableEffectHandler)가 GAS 로 적용한다(Model 은 신호만).
        /// </summary>
        private async UniTaskVoid UseItemAsync(string itemId)
        {
            try
            {
                var (result, _) = await _service.ConsumeItemAsync(itemId, 1, _cts.Token);
                if (result != InventoryResult.Success)
                {
                    _onToast.OnNext("사용할 수 없습니다");   // Side Effect (실패 토스트) — 회복 안 함
                    return;
                }

                _onConsumableUsed.OnNext(itemId);            // Side Effect A: 회복 적용 트리거(구독자→GAS)
                _onToast.OnNext($"{DisplayName(itemId)} 사용"); // Side Effect B: 토스트
                RefreshAsync().Forget();                     // State: 남은 수량 갱신(비동기)
            }
            catch (OperationCanceledException)
            {
                // 창 닫힘/씬 전환 — 정상 취소
            }
        }

        private string DisplayName(string itemId)
            => (_catalog != null ? _catalog.Get(itemId)?.displayName : null) ?? itemId;

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
            _onConsumableUsed.Dispose();
            _onToast.Dispose();
        }
    }
}

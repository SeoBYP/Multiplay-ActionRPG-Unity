using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Inventory;
using Game.System.Equipment;
using Game.System.Input;
using R3;

namespace Game.Presentation.Equipment
{
    /// <summary>
    /// 장비창 MVI Model. View는 Accept(Intent)만, Model은 State만 발행한다.
    /// 표시(아이콘·이름)는 ItemDisplayCatalog 로 합성(인벤토리와 동일 카탈로그 공유).
    /// IEquipmentService.OnChanged 구독 → 인벤토리에서 장착해도 즉시 재조회.
    /// </summary>
    public sealed class EquipmentModel : IDisposable
    {
        private readonly IEquipmentService _service;
        private readonly IInputContext _inputContext;
        private readonly ItemDisplayCatalog _catalog;
        private readonly GradeSpriteCatalog _gradeCatalog;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<EquipmentState> _state = new(EquipmentState.Initial);
        public ReadOnlyReactiveProperty<EquipmentState> State => _state.ToReadOnlyReactiveProperty();

        public EquipmentModel(IEquipmentService service, IInputContext inputContext = null, ItemDisplayCatalog catalog = null,
            GradeSpriteCatalog gradeCatalog = null)
        {
            _service = service;
            _inputContext = inputContext;
            _catalog = catalog;
            _gradeCatalog = gradeCatalog;
            _service.OnChanged += OnServiceChanged;
        }

        // 창이 열린 동안 게임플레이(Player) 입력 점유 — View의 UiInputCaptureBehaviour가 호출(InventoryModel과 동일).
        public void BeginUiCapture() => _inputContext?.EnterUi();
        public void EndUiCapture() => _inputContext?.ExitUi();

        public void Accept(EquipmentIntent intent)
        {
            switch (intent)
            {
                case EquipmentIntent.Refresh:
                    RefreshAsync().Forget();
                    break;
                case EquipmentIntent.Unequip unequip:
                    UnequipAsync(unequip.Slot).Forget();
                    break;
            }
        }

        // 장착/해제 성공 시(인벤토리 경유 포함) 서비스가 알려준다 → 재조회.
        private void OnServiceChanged() => RefreshAsync().Forget();

        private async UniTaskVoid UnequipAsync(Shared.Gameplay.Equipment.EquipmentType slot)
        {
            try
            {
                await _service.UnequipAsync(slot, _cts.Token); // 성공 시 OnChanged → Refresh
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid RefreshAsync()
        {
            _state.Value = _state.Value.WithLoading();
            try
            {
                var (result, items) = await _service.GetEquippedAsync(_cts.Token);
                if (result != EquipmentResult.Success)
                {
                    _state.Value = _state.Value.WithError(result.ToString());
                    return;
                }

                var models = new List<EquipmentSlotModel>(items.Count);
                foreach (var data in items)
                {
                    var entry = _catalog != null ? _catalog.Get(data.ItemId) : null;
                    var gradeBg = _gradeCatalog != null && entry != null ? _gradeCatalog.Get(entry.grade) : null;
                    models.Add(new EquipmentSlotModel(
                        data.Slot,
                        data.ItemId,
                        entry?.displayName ?? data.ItemId.ToString(),
                        entry?.icon,
                        gradeBg));
                }

                _state.Value = _state.Value.WithEquipped(models);
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _service.OnChanged -= OnServiceChanged;
            _cts.Cancel();
            _cts.Dispose();
            _state.Dispose();
        }
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI;
using Game.Presentation.InGame;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 인벤토리 창 생명주기 컨트롤러 (POCO). InGameModel.OnToggleInventory(I키·HUD 버튼 공용 신호) 수신 →
    /// Inventory.prefab 로드(최초 1회)·Inject 후 토글.
    /// </summary>
    public sealed class InventoryViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;
        private readonly EquipmentViewController _equipment;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public InventoryViewController(InGameModel inGameModel, IObjectResolver resolver, EquipmentViewController equipment)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
            _equipment = equipment;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _inGameModel.OnToggleInventory
                .Subscribe(_ => ToggleAsync().Forget())
                .AddTo(_cts.Token);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _inst?.Dispose();
            _inst = null;
        }

        /// <summary>
        /// 인벤토리+장비 "쌍 토글". 인벤토리 상태 기준으로 열려 있으면 둘 다 닫고, 닫혀 있으면 둘 다 연다.
        /// X 로 한쪽만 닫힌 뒤에도 같은 기준이라 인벤토리만 다시 열린다(이미 열린 장비는 그대로 유지).
        /// </summary>
        private async UniTaskVoid ToggleAsync()
        {
            bool inventoryOpen = _inst != null && _inst.GameObject != null && _inst.GameObject.activeSelf;

            if (inventoryOpen)
            {
                _inst.GameObject.SetActive(false);
                _equipment?.Hide();
                return;
            }

            await ShowInventoryAsync();
            if (_equipment != null)
                await _equipment.ShowAsync();
        }

        private async UniTask ShowInventoryAsync()
        {
            // 켜질 때 Inventory.OnEnable 이 Refresh.
            if (_inst != null)
            {
                _inst.GameObject.SetActive(true);
                return;
            }

            if (_loading) return;
            _loading = true;
            try
            {
                // GUIRoot 이 없을 수도 있다(예: 던전 씬 직접 Play) — 없으면 부모 없이 생성.
                var parent = GUIRoot.Instance != null ? GUIRoot.Instance.transform : null;
                _inst = await AddressableLoader.LoadAndInstantiateAsync(
                    AddressKeys.UI.Inventory, parent, _cts.Token);

                if (_inst != null)
                {
                    _resolver.InjectGameObject(_inst.GameObject); // Inventory.Start → 첫 Refresh
                    Debug.Log("[InventoryViewController] Inventory 창 로드·생성 완료");
                }
                else
                {
                    Debug.LogError("[InventoryViewController] Inventory.prefab 로드 실패 (Addressable 확인 필요)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[InventoryViewController] 인벤토리 토글 실패: {e}");
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

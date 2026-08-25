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
    /// 인벤토리 창 생명주기 컨트롤러 (POCO).
    ///
    /// 역할:
    ///   InGameModel.OnToggleInventory(HUD 버튼·I키 공용 신호) 수신 → Inventory.prefab 로드(최초 1회)·Inject 후 토글.
    ///
    /// I키 = InputRouter→GameInputAction.ToggleInventory→InGameModel.Accept(ToggleInventory) 경로.
    /// HUD 버튼도 같은 신호를 발행한다(단일 funnel). 2026-08-25 배선 완료(F13).
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
        /// I키/HUD 버튼 = 인벤토리+장비 "쌍 토글". 인벤토리 상태 기준:
        ///   열려 있으면 → 둘 다 닫기, 닫혀 있으면 → 둘 다 열기.
        /// 각 창의 X(Close)로 따로 닫는 건 View가 독립 처리 → 한쪽만 닫힌 뒤 I키를 누르면 인벤토리는 다시 열린다
        /// (장비가 이미 열려 있으면 ShowAsync가 그대로 유지).
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
            // 이미 로드된 경우 활성화(켜질 때 Inventory.OnEnable이 Refresh).
            if (_inst != null)
            {
                _inst.GameObject.SetActive(true);
                return;
            }

            if (_loading) return;
            _loading = true;
            try
            {
                // GUIRoot이 없을 수도 있다(예: 던전 씬 직접 Play). GameHudController처럼 null 가드 — 없으면 부모 없이 생성.
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

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
    /// I키는 InputRouter→GameInputAction.ToggleInventory→InGameModel.Accept(ToggleInventory) 경로로 합류 예정
    /// (현재 던전 씬에 InputRouter 미등록 + .inputactions Inventory 액션 필요 — 후속 Unity 작업).
    /// 그때까지는 HUD 버튼만 이 신호를 발행한다.
    /// </summary>
    public sealed class InventoryViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public InventoryViewController(InGameModel inGameModel, IObjectResolver resolver)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
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

        private async UniTaskVoid ToggleAsync()
        {
            // 이미 로드된 경우 활성 토글(켜질 때 Inventory.OnEnable이 Refresh).
            if (_inst != null)
            {
                var go = _inst.GameObject;
                go.SetActive(!go.activeSelf);
                return;
            }

            if (_loading) return;
            _loading = true;
            try
            {
                _inst = await AddressableLoader.LoadAndInstantiateAsync(
                    AddressKeys.UI.Inventory, GUIRoot.Instance.transform, _cts.Token);

                if (_inst != null)
                    _resolver.InjectGameObject(_inst.GameObject); // Inventory.Start → 첫 Refresh
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

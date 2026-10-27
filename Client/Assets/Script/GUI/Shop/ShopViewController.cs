using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.GUI;
using Game.Presentation.InGame;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.Shop
{
    /// <summary>
    /// 상점 창 생명주기 컨트롤러 (POCO). InGameModel.OnToggleShop(HUD 상점버튼) 수신 → 단독 토글(최초 로드 후 SetActive 반전).
    /// X 닫기와 입력 점유(이동 차단)는 View(Shop) 쪽 책임.
    /// </summary>
    public sealed class ShopViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public ShopViewController(InGameModel inGameModel, IObjectResolver resolver)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _inGameModel.OnToggleShop
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
            if (_inst != null)
            {
                var go = _inst.GameObject;
                go.SetActive(!go.activeSelf);
                return;
            }
            await LoadAsync(active: true);
        }

        private async UniTask LoadAsync(bool active)
        {
            if (_loading) return;
            _loading = true;
            try
            {
                var parent = GUIRoot.Instance != null ? GUIRoot.Instance.transform : null;
                _inst = await AddressableLoader.LoadAndInstantiateAsync(
                    AddressKeys.UI.Shop, parent, _cts.Token);

                if (_inst != null)
                {
                    _resolver.InjectGameObject(_inst.GameObject); // Shop.Start → 첫 Refresh
                    _inst.GameObject.SetActive(active);
                    Debug.Log("[ShopViewController] Shop 창 로드·생성 완료");
                }
                else
                {
                    Debug.LogError("[ShopViewController] Shop.prefab 로드 실패 (Addressable 확인 필요)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShopViewController] 상점창 로드 실패: {e}");
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

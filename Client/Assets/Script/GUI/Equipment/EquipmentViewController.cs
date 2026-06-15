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
    /// 장비 창 생명주기 컨트롤러 (POCO).
    ///
    /// 역할:
    ///   ① InGameModel.OnToggleEquipment(K키) 수신 → 장비창 단독 토글.
    ///   ② InventoryViewController 가 "쌍 토글"(I키)에서 Show/Hide 로 함께 여닫는다.
    ///
    /// 창 자체의 X(Close) 버튼은 View(Equipment) 가 자기 SetActive(false) 로 독립 처리 →
    /// 인벤토리와 따로 닫힐 수 있다(요구사항: 하나만 X로 닫으면 독립).
    /// </summary>
    public sealed class EquipmentViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public EquipmentViewController(InGameModel inGameModel, IObjectResolver resolver)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _inGameModel.OnToggleEquipment
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

        /// <summary>현재 창이 떠 있고 활성인가(쌍 토글 판단용).</summary>
        public bool IsOpen => _inst != null && _inst.GameObject != null && _inst.GameObject.activeSelf;

        /// <summary>장비창을 띄운다(최초 로드 후 SetActive(true)). 인벤토리 쌍 토글에서 호출.</summary>
        public async UniTask ShowAsync()
        {
            if (_inst != null)
            {
                _inst.GameObject.SetActive(true);
                return;
            }
            await LoadAsync(active: true);
        }

        /// <summary>장비창을 숨긴다(로드돼 있을 때만). 인벤토리 쌍 토글에서 호출.</summary>
        public void Hide()
        {
            if (_inst != null && _inst.GameObject != null)
                _inst.GameObject.SetActive(false);
        }

        // K키 단독 토글: 로드돼 있으면 활성 반전, 아니면 로드(활성).
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
                    AddressKeys.UI.Equipment, parent, _cts.Token);

                if (_inst != null)
                {
                    _resolver.InjectGameObject(_inst.GameObject); // Equipment.Start → 첫 Refresh
                    _inst.GameObject.SetActive(active);
                    Debug.Log("[EquipmentViewController] Equipment 창 로드·생성 완료");
                }
                else
                {
                    Debug.LogError("[EquipmentViewController] Equipment.prefab 로드 실패 (Addressable 확인 필요)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EquipmentViewController] 장비창 로드 실패: {e}");
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

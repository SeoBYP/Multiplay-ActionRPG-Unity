using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.InGame;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.Stat
{
    /// <summary>
    /// 캐릭터 정보/스탯창(7.3) 생명주기 컨트롤러 (POCO). QuestViewController 동형 — 독립 토글.
    /// InGameModel.OnToggleAbility(HUD Ability버튼·G키) 수신 → 스탯창 단독 토글(최초 로드 후 SetActive 반전).
    /// </summary>
    public sealed class StatViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public StatViewController(InGameModel inGameModel, IObjectResolver resolver)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _inGameModel.OnToggleAbility
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
                _inst = await AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.StatWindow, parent, _cts.Token);

                if (_inst != null)
                {
                    _resolver.InjectGameObject(_inst.GameObject); // StatWindow.Start → 첫 Refresh
                    _inst.GameObject.SetActive(active);
                    Debug.Log("[StatViewController] 스탯창 로드·생성 완료");
                }
                else
                {
                    Debug.LogError("[StatViewController] StatWindow.prefab 로드 실패 (Addressable 확인 필요)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatViewController] 스탯창 로드 실패: {e}");
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.InGame;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 퀘스트 창 생명주기 컨트롤러 (POCO). ShopViewController 동형 — 독립 토글.
    /// InGameModel.OnToggleQuest(HUD 퀘스트버튼) 수신 → 퀘스트창 단독 토글(최초 로드 후 SetActive 반전).
    /// </summary>
    public sealed class QuestViewController : IInitializable, IDisposable
    {
        private readonly InGameModel _inGameModel;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private bool _loading;

        public QuestViewController(InGameModel inGameModel, IObjectResolver resolver)
        {
            _inGameModel = inGameModel;
            _resolver = resolver;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _inGameModel.OnToggleQuest
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
                _inst = await AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.Quest, parent, _cts.Token);

                if (_inst != null)
                {
                    _resolver.InjectGameObject(_inst.GameObject); // Quest.Start → 첫 Refresh
                    _inst.GameObject.SetActive(active);
                    Debug.Log("[QuestViewController] Quest 창 로드·생성 완료");
                }
                else
                {
                    Debug.LogError("[QuestViewController] Quest.prefab 로드 실패 (Addressable 확인 필요)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestViewController] 퀘스트창 로드 실패: {e}");
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

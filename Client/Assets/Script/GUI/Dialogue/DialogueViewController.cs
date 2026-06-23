using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Presentation.Dialogue;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.Dialogue
{
    /// <summary>
    /// 대화 창 생명주기 컨트롤러(상태 구동). DialogueModel.State.IsOpen 을 구독해 창을 열고 닫는다.
    /// 대화 시작 자체는 NPC(Gameplay)→IDialogueLauncher(=DialogueModel) 경로 — 여기선 창 로드/표시만 담당.
    /// (GUI는 System 을 직접 참조하지 못하므로 다리 구현은 DialogueModel(Presentation)에 있다.)
    /// ShopViewController 동형(Addressable 1회 로드 후 SetActive 토글).
    /// </summary>
    public sealed class DialogueViewController : IInitializable, IDisposable
    {
        private readonly DialogueModel _model;
        private readonly IObjectResolver _resolver;

        private AddressableInstance _inst;
        private CancellationTokenSource _cts;
        private IDisposable _stateSub;
        private bool _loading;

        // 대화 중 비활성화한 오브젝트(복원용). 열림 엣지에서 채우고 닫힘 엣지에서 되살린다.
        private readonly List<GameObject> _hidden = new();
        private bool _wasOpen;

        public DialogueViewController(DialogueModel model, IObjectResolver resolver)
        {
            _model = model;
            _resolver = resolver;
        }

        public void Initialize()
        {
            _cts = new CancellationTokenSource();
            _stateSub = _model.State.Subscribe(OnStateChanged);
        }

        public void Dispose()
        {
            RestoreHide();                  // 대화 중 파괴되어도 숨긴 오브젝트는 되살린다
            _cts?.Cancel();
            _cts?.Dispose();
            _stateSub?.Dispose();
            _inst?.Dispose();
            _inst = null;
        }

        private void OnStateChanged(DialogueState state)
        {
            if (state.IsOpen)
            {
                if (!_wasOpen) ApplyHide(state.HideObjects); // 열림 엣지에서 1회(노드마다 반복 X)
                ShowAsync().Forget();      // 최초면 로드 후 활성(DialogueView가 같은 State 를 즉시 렌더)
            }
            else
            {
                if (_wasOpen) RestoreHide();
                if (_inst != null) _inst.GameObject.SetActive(false);
            }
            _wasOpen = state.IsOpen;
        }

        /// <summary>대화 동안 지정 GameObject 비활성화(이름/경로 find). 못 찾으면 경고만.</summary>
        private void ApplyHide(IReadOnlyList<string> names)
        {
            if (names == null) return;
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var go = GameObject.Find(name);
                if (go == null)
                {
                    Debug.LogWarning($"[DialogueViewController] 숨길 오브젝트 '{name}' 못 찾음(대화 시작 시 활성·고유 이름 확인)");
                    continue;
                }
                go.SetActive(false);
                _hidden.Add(go);
            }
        }

        private void RestoreHide()
        {
            foreach (var go in _hidden)
                if (go != null) go.SetActive(true);
            _hidden.Clear();
        }

        private async UniTaskVoid ShowAsync()
        {
            if (_inst == null)
            {
                if (!await LoadAsync()) return;
            }
            _inst.GameObject.SetActive(true);
        }

        private async UniTask<bool> LoadAsync()
        {
            if (_loading) return false;
            _loading = true;
            try
            {
                var parent = GUIRoot.Instance != null ? GUIRoot.Instance.transform : null;
                _inst = await AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.Dialogue, parent, _cts.Token);
                if (_inst == null)
                {
                    Debug.LogError("[DialogueViewController] Dialogue.prefab 로드 실패 (Addressable 확인 필요)");
                    return false;
                }
                _resolver.InjectGameObject(_inst.GameObject); // DialogueView.Start → State 구독(현재값 즉시 렌더)
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueViewController] 대화창 로드 실패: {e}");
                return false;
            }
            finally
            {
                _loading = false;
            }
        }
    }
}

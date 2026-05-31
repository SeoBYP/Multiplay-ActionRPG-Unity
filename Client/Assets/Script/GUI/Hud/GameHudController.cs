using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GUI.OutGame
{
    public sealed class GameHudController : IAsyncStartable, IDisposable
    {
        private readonly IObjectResolver _resolver;
        private AddressableInstance? _inst;

        public GameHudController(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            var parent = GUIRoot.Instance != null ? GUIRoot.Instance.transform : null;
            _inst = await AddressableLoader.LoadAndInstantiateAsync(AddressKeys.UI.GameHud, parent, ct);
            if (_inst != null)
            {
                _resolver.InjectGameObject(_inst.GameObject);
                Debug.Log("[GameHudController] GameHud 로드·생성 완료");
            }
        }

        public void Dispose()
        {
            _inst?.Dispose();
            _inst = null;
        }
    }
}

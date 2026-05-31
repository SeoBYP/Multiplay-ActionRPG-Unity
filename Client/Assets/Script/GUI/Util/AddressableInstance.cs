using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.GUI
{
    /// <summary>
    /// Addressable 로드 결과(핸들 + 인스턴스)를 하나로 소유.
    /// Dispose 시 GameObject 파괴 + 핸들 Release를 함께 처리.
    /// </summary>
    public sealed class AddressableInstance : IDisposable
    {
        public GameObject GameObject { get; }

        private readonly AsyncOperationHandle<GameObject> _handle;
        private bool _disposed;

        internal AddressableInstance(GameObject go, AsyncOperationHandle<GameObject> handle)
        {
            GameObject = go;
            _handle    = handle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (GameObject != null)
                UnityEngine.Object.Destroy(GameObject);

            if (_handle.IsValid())
                Addressables.Release(_handle);
        }
    }
}

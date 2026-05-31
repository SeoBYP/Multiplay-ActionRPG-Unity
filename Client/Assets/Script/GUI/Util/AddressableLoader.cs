using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.GUI
{
    /// <summary>
    /// Addressable 프리팹 로드 + 인스턴스화 유틸.
    /// 취소/실패 시 핸들을 내부에서 정리하고 null 반환.
    /// </summary>
    public static class AddressableLoader
    {
        /// <summary>
        /// 프리팹을 로드해 parent 아래에 인스턴스화한다.
        /// 취소(ct) 또는 로드 실패 시 null 반환 — 핸들은 내부에서 해제됨.
        /// </summary>
        public static async UniTask<AddressableInstance?> LoadAndInstantiateAsync(
            string key, Transform parent, CancellationToken ct)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(key);

            try
            {
                await handle.Task.AsUniTask().AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AddressableLoader] 로드 실패 key={key}: {e.Message}");
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            if (ct.IsCancellationRequested)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            var go = UnityEngine.Object.Instantiate(handle.Result, parent);
            return new AddressableInstance(go, handle);
        }
    }
}

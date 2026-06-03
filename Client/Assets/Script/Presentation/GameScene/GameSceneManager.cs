using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.System.GameScene;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Game.Presentation.GameScene
{
    /// <summary>
    /// 씬 전환을 책임지는 서비스.
    ///
    /// 흐름:
    ///   1. Fader 생성 → FadeIn (현재 씬을 검게 덮음)
    ///   2. LoadingCanvas를 Fader 위에 생성 → 로딩바 재생
    ///   3. 씬 비동기 로드 + 진행률 표시
    ///   4. 로딩 제거 전에 Fader를 로딩 위에 다시 생성 → FadeIn (로딩을 검게 덮음)
    ///   5. LoadingCanvas와 첫 Fader 제거 (검은 막 뒤에 가려진 채)
    ///   6. FadeOut → 새 씬(던전)을 부드럽게 드러냄
    ///
    /// Fader와 Loading은 서로 다른 프리팹이며, 둘 다 Addressable로 동적 생성 후 파괴한다.
    /// </summary>
    public class GameSceneManager : IGameSceneManager
    {
        private const string FaderKey   = "Assets/Prefabs/GUI/Common/FaderCanvas.prefab";
        private const string LoadingKey = "Assets/Prefabs/GUI/Common/Loading/LoadingCanvas.prefab";

        // 씬이 즉시 로드돼도 로딩 화면이 깜빡이지 않도록 최소 노출 시간을 보장한다.
        private const float MinLoadingSeconds = 2f;

        public async UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default, Func<UniTask> holdUntil = null)
        {
            var enterFader  = default(Overlay); // 진입용 Fader (현재 씬 → 검게)
            var loading     = default(Overlay); // 로딩 캔버스
            var exitFader   = default(Overlay); // 진출용 Fader (검게 → 던전)

            try
            {
                // ── 1. 진입 Fader 생성 + FadeIn — 현재 씬을 검게 덮는다 ─────────
                enterFader = await LoadOverlayAsync(FaderKey, ct);
                if (enterFader.GameObject.GetComponent<IFader>() is { } fadeIn)
                    await fadeIn.FadeInAsync(ct);

                // ── 2. Loading 캔버스를 Fader 위에 생성 — 로딩바 표시 ──────────
                loading = await LoadOverlayAsync(LoadingKey, ct);
                var loadingView = loading.GameObject.GetComponent<ILoadingView>();

                // 로딩 화면이 보이기 시작한 시점. 최소 노출 시간 기준으로 사용한다.
                var loadingStartTime = Time.realtimeSinceStartup;

                // ── 3. 씬 비동기 로드 (allowSceneActivation=false → 0.9에서 멈춤) ──
                var loadOp = SceneManager.LoadSceneAsync(sceneName);
                loadOp.allowSceneActivation = false;

                // 표시 진행률 = min(로드 진행률, 시간 진행률).
                // 로드가 먼저 끝나도 최소 시간이 차야 100%가 되므로,
                // 바가 0→100까지 차오르는 시점과 씬 전환 시점이 정확히 일치한다.
                while (true)
                {
                    var loadProgress = Mathf.Clamp01(loadOp.progress / 0.9f);
                    var timeProgress = Mathf.Clamp01(
                        (Time.realtimeSinceStartup - loadingStartTime) / MinLoadingSeconds);
                    var displayed = Mathf.Min(loadProgress, timeProgress);

                    loadingView?.SetProgress(displayed * 100f);

                    if (loadProgress >= 1f && timeProgress >= 1f)
                        break;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                loadOp.allowSceneActivation = true;
                await UniTask.WaitUntil(() => loadOp.isDone, cancellationToken: ct);

                // ── 3.5. holdUntil 대기 — 새 씬은 활성화됐지만 Loading 으로 덮은 채 ────
                // (예: 던전 "전원 입장"까지 로딩 유지. 그동안 씬은 Loading 뒤에서 스폰 등 준비.)
                if (holdUntil != null)
                {
                    loadingView?.SetProgress(100f);
                    loadingView?.SetMessage("다른 플레이어를 기다리는 중…");
                    Debug.Log($"[GameSceneManager] '{sceneName}' 활성화 완료 — holdUntil 대기 시작(Loading 유지)");
                    await holdUntil();
                    Debug.Log($"[GameSceneManager] '{sceneName}' holdUntil 완료 — Fader로 화면 전환 시작");
                }

                // ── 4. 진출 Fader를 로딩 위에 생성 + FadeIn — 로딩을 검게 덮는다 ──
                exitFader = await LoadOverlayAsync(FaderKey, ct);
                var fadeOut = exitFader.GameObject.GetComponent<IFader>();
                if (fadeOut != null)
                    await fadeOut.FadeInAsync(ct);

                // ── 5. 검은 막 뒤에서 로딩과 진입 Fader 제거 ───────────────────
                loading.Dispose();    loading    = default;
                enterFader.Dispose(); enterFader = default;

                // ── 6. FadeOut — 새 씬(던전)을 부드럽게 드러낸다 ───────────────
                if (fadeOut != null)
                    await fadeOut.FadeOutAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[GameSceneManager] 씬 로드 취소됨: {sceneName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSceneManager] 씬 로드 실패 ({sceneName}): {e}");
            }
            finally
            {
                exitFader.Dispose();
                loading.Dispose();
                enterFader.Dispose();
            }
        }

        /// <summary>프리팹을 로드해 인스턴스화하고, 씬 전환에도 살아남도록 DontDestroyOnLoad 처리한다.</summary>
        private static async UniTask<Overlay> LoadOverlayAsync(string key, CancellationToken ct)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            await handle.Task.AsUniTask().AttachExternalCancellation(ct);

            var go = UnityEngine.Object.Instantiate(handle.Result);
            UnityEngine.Object.DontDestroyOnLoad(go);
            return new Overlay(go, handle);
        }

        /// <summary>
        /// 동적 생성한 오버레이(프리팹 인스턴스 + Addressable 핸들)를 함께 소유.
        /// Dispose는 멱등 — 중복 호출에 안전하다(struct 기본값 Dispose 포함).
        /// </summary>
        private readonly struct Overlay
        {
            public readonly GameObject GameObject;
            private readonly AsyncOperationHandle<GameObject> _handle;

            public Overlay(GameObject go, AsyncOperationHandle<GameObject> handle)
            {
                GameObject = go;
                _handle    = handle;
            }

            public void Dispose()
            {
                if (GameObject != null)
                    UnityEngine.Object.Destroy(GameObject);

                if (_handle.IsValid())
                    Addressables.Release(_handle);
            }
        }
    }
}

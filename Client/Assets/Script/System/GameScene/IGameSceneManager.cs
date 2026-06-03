using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.GameScene
{
    public interface IGameSceneManager
    {
        /// <summary>
        /// FadeIn → 씬 비동기 로드(진행률 표시) → [holdUntil 대기] → FadeOut 순서로 씬을 전환한다.
        ///
        /// holdUntil 가 주어지면, 씬 활성화 후에도 **Loading 을 띄운 채** 그 작업이 끝날 때까지 대기한 뒤
        /// Fader 로 새 씬을 드러낸다. (예: 던전 입장 시 "전원 입장(S_GameStatus InProgress)"까지 로딩 유지)
        /// </summary>
        UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default, Func<UniTask> holdUntil = null);
    }
}

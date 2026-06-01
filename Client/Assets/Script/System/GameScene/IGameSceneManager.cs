using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.System.GameScene
{
    public interface IGameSceneManager
    {
        /// <summary>
        /// FadeIn → 씬 비동기 로드(진행률 표시) → FadeOut 순서로 씬을 전환한다.
        /// </summary>
        UniTask LoadSceneAsync(string sceneName, CancellationToken ct = default);
    }
}

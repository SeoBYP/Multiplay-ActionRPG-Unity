using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Presentation.GameScene
{
    /// <summary>
    /// 화면 페이드 인/아웃 계약.
    /// Game.Presentation 레이어에 정의하고, FaderCanvas(Game.GUI)가 구현한다.
    /// </summary>
    public interface IFader
    {
        /// <summary>화면을 검게 덮는다. 페이드 완료까지 await.</summary>
        UniTask FadeInAsync(CancellationToken ct);

        /// <summary>화면을 드러낸다. 페이드 완료까지 await.</summary>
        UniTask FadeOutAsync(CancellationToken ct);
    }
}

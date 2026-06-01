namespace Game.Presentation.GameScene
{
    /// <summary>
    /// 로딩 진행률 표시 계약.
    /// Game.Presentation 레이어에 정의하고, Loading(Game.GUI)이 구현한다.
    /// </summary>
    public interface ILoadingView
    {
        /// <summary>로딩 진행률 갱신. progress = 0~100.</summary>
        void SetProgress(float progress);
    }
}

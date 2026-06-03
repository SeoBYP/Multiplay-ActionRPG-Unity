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

        /// <summary>로딩 텍스트를 임의 메시지로 교체(예: "다른 플레이어를 기다리는 중…"). 이후 SetProgress 호출 시 다시 덮인다.</summary>
        void SetMessage(string message);
    }
}

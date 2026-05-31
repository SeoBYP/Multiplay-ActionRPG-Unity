namespace Game.Gameplay.Input
{
    /// <summary>
    /// 특정 컨텍스트에서 InputAction을 소비할 수 있는 핸들러.
    ///
    /// Priority 높을수록 먼저 처리. TryHandle이 true를 반환하면
    /// 같은 프레임 나머지 핸들러는 해당 액션을 받지 못한다.
    ///
    /// 예: UI(100) > 인터랙션(50) > 전투(10)
    /// UI가 열려있으면 F키를 UI가 소비하고 월드 인터랙션은 무시됨.
    /// </summary>
    public interface IInputHandler
    {
        int Priority { get; }

        /// <summary>
        /// 이 핸들러가 해당 액션을 처리하면 true 반환 (이후 핸들러 차단).
        /// 처리하지 않으면 false 반환 (다음 핸들러로 전달).
        /// </summary>
        bool TryHandle(GameInputAction action);
    }
}

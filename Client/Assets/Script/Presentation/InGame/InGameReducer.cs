namespace Game.Presentation.InGame
{
    /// <summary>
    /// 순수 함수 Reducer — (OldState, Result) → NewState.
    /// 비동기 처리, 네트워크 호출, 외부 상태 참조 금지.
    /// </summary>
    public static class InGameReducer
    {
        public static InGameState Reduce(InGameState state, InGameResult result)
        {
            if (result is InGameResult.Returning)
                return state.WithReturning();

            if (result is InGameResult.Failed failed)
                return state.WithError(failed.Message);

            return state;
        }
    }
}

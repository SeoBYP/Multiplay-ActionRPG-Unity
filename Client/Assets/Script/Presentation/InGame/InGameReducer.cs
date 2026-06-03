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

            if (result is InGameResult.HpChanged hp)
                return state.WithHp(hp.Current, hp.Max);

            if (result is InGameResult.MpChanged mp)
                return state.WithMp(mp.Current, mp.Max);

            if (result is InGameResult.BuffsChanged buffs)
                return state.WithBuffs(buffs.Buffs);

            if (result is InGameResult.DungeonReady)
                return state.WithDungeonReady();

            return state;
        }
    }
}

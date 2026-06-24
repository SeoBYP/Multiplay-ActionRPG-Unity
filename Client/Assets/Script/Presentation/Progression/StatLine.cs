namespace Game.Presentation.Progression
{
    /// <summary>
    /// 스탯창 한 줄(라벨:값)의 View-facing 표현. GUI 는 이 문자열만 렌더 —
    /// System 타입(ProgressionData/ProgressionStats)을 GUI 에 노출하지 않기 위한 Presentation 변환물.
    /// </summary>
    public readonly struct StatLine
    {
        public readonly string Label;
        public readonly string Value;

        public StatLine(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }
}

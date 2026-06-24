using System;
using System.Collections.Generic;
using Game.System.Progression;

namespace Game.Presentation.Progression
{
    /// <summary>
    /// 스탯창 View 가 그릴 불변 상태. (로딩/에러/로드완료 + 진행 데이터 + GUI용 라인 목록)
    /// </summary>
    public readonly struct ProgressionViewState
    {
        public readonly bool IsLoading;
        public readonly string Error;       // null = 에러 없음
        public readonly bool HasData;
        public readonly ProgressionData Data;
        /// <summary>GUI 가 그대로 렌더하는 (라벨:값) 라인. System 타입 비노출용 변환물.</summary>
        public readonly IReadOnlyList<StatLine> Lines;

        private static readonly IReadOnlyList<StatLine> EmptyLines = Array.Empty<StatLine>();

        private ProgressionViewState(bool isLoading, string error, bool hasData, ProgressionData data,
            IReadOnlyList<StatLine> lines)
        {
            IsLoading = isLoading;
            Error = error;
            HasData = hasData;
            Data = data;
            Lines = lines ?? EmptyLines;
        }

        public static ProgressionViewState Initial => new(false, null, false, default, EmptyLines);

        public ProgressionViewState WithLoading() => new(true, null, HasData, Data, Lines);

        public ProgressionViewState WithError(string error) => new(false, error, HasData, Data, Lines);

        public static ProgressionViewState Loaded(ProgressionData data) => new(false, null, true, data, BuildLines(data));

        private static IReadOnlyList<StatLine> BuildLines(ProgressionData d)
        {
            var s = d.Stats;
            return new List<StatLine>
            {
                new StatLine("레벨", d.Level.ToString()),
                new StatLine("경험치", d.IsMaxLevel ? "MAX" : $"{d.Exp} / {d.ExpToNext}"),
                new StatLine("체력", s.MaxHealth.ToString()),
                new StatLine("마나", s.MaxMana.ToString()),
                new StatLine("공격력", s.AttackPower.ToString()),
                new StatLine("방어력", s.Defense.ToString()),
                new StatLine("힘", s.Strength.ToString()),
                new StatLine("민첩", s.Dexterity.ToString()),
                new StatLine("지능", s.Intelligence.ToString()),
            };
        }
    }
}

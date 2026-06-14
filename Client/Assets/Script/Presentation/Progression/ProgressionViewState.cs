using Game.System.Progression;

namespace Game.Presentation.Progression
{
    /// <summary>
    /// 스탯창 View 가 그릴 불변 상태. (로딩/에러/로드완료 + 진행 데이터)
    /// </summary>
    public readonly struct ProgressionViewState
    {
        public readonly bool IsLoading;
        public readonly string Error;       // null = 에러 없음
        public readonly bool HasData;
        public readonly ProgressionData Data;

        private ProgressionViewState(bool isLoading, string error, bool hasData, ProgressionData data)
        {
            IsLoading = isLoading;
            Error = error;
            HasData = hasData;
            Data = data;
        }

        public static ProgressionViewState Initial => new(false, null, false, default);

        public ProgressionViewState WithLoading() => new(true, null, HasData, Data);

        public ProgressionViewState WithError(string error) => new(false, error, HasData, Data);

        public static ProgressionViewState Loaded(ProgressionData data) => new(false, null, true, data);
    }
}

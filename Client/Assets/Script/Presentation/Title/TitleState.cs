namespace Game.Presentation.Title
{
    /// <summary>
    /// 타이틀 화면의 불변 상태 스냅샷.
    /// WithXxx 메서드로만 새 State를 생성한다.
    /// </summary>
    public sealed class TitleState
    {
        public readonly bool   IsAuthenticated;
        public readonly bool   IsLoading;
        public readonly string ErrorMessage; // null = 에러 없음

        public static readonly TitleState Initial = new TitleState(false, false, null);

        public TitleState(bool isAuthenticated, bool isLoading, string errorMessage)
        {
            IsAuthenticated = isAuthenticated;
            IsLoading       = isLoading;
            ErrorMessage    = errorMessage;
        }

        public TitleState WithLoading()              => new TitleState(IsAuthenticated, true, null);
        public TitleState WithAuthenticated()        => new TitleState(true,  false, null);
        public TitleState WithError(string message)  => new TitleState(false, false, message);
    }
}

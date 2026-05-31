namespace Game.Presentation.InGame
{
    /// <summary>
    /// 인게임 화면의 불변 상태 스냅샷.
    /// WithXxx 메서드로만 새 State를 생성한다 — 직접 필드 수정 불가.
    /// </summary>
    public sealed class InGameState
    {
        /// <summary>복귀 처리 중 (소켓 해제 + LeaveRoom 진행 중).</summary>
        public readonly bool IsReturning;

        /// <summary>복귀 실패 메시지. null = 에러 없음.</summary>
        public readonly string ErrorMessage;

        public static readonly InGameState Initial = new InGameState(false, null);

        private InGameState(bool isReturning, string errorMessage)
        {
            IsReturning  = isReturning;
            ErrorMessage = errorMessage;
        }

        public InGameState WithReturning() =>
            new InGameState(true, null);

        public InGameState WithError(string message) =>
            new InGameState(false, message);
    }
}

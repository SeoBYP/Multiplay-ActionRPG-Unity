namespace Game.Presentation.InGame
{
    /// <summary>
    /// Effect 실행 결과의 닫힌 집합.
    /// Reducer만 받는다 — View에 직접 노출되지 않는다.
    /// </summary>
    public abstract class InGameResult
    {
        private InGameResult() { }

        /// <summary>복귀 처리 시작 — 버튼 비활성화 등 로딩 표시에 사용.</summary>
        public sealed class Returning : InGameResult
        {
            public static readonly Returning Instance = new Returning();
            private Returning() { }
        }

        /// <summary>복귀 도중 예외 발생.</summary>
        public sealed class Failed : InGameResult
        {
            public readonly string Message;
            public Failed(string message) { Message = message; }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Input;

namespace Game.Tests.EditMode.Input.Fakes
{
    /// <summary>
    /// IInputHandler 의 테스트용 추적 핸들러.
    /// 수신한 액션 목록을 기록하고, consumed 여부를 생성자에서 지정한다.
    /// </summary>
    internal class TrackingHandler : IInputHandler
    {
        private readonly bool                   _consumes;
        private readonly List<GameInputAction>  _received = new List<GameInputAction>();

        public int Priority { get; }

        /// <summary>
        /// <param name="priority">IInputHandler.Priority 값</param>
        /// <param name="consumes">TryHandle 이 true 를 반환할지 여부 (true = 하위 핸들러 차단)</param>
        /// </summary>
        public TrackingHandler(int priority, bool consumes)
        {
            Priority  = priority;
            _consumes = consumes;
        }

        public bool TryHandle(GameInputAction action)
        {
            _received.Add(action);
            return _consumes;
        }

        /// <summary>해당 액션이 최소 1회 수신됐는지.</summary>
        public bool WasCalled(GameInputAction action) => _received.Contains(action);

        /// <summary>해당 액션 수신 횟수.</summary>
        public int CallCount(GameInputAction action) => _received.Count(a => a == action);

        /// <summary>수신 여부 초기화.</summary>
        public void Reset() => _received.Clear();
    }
}

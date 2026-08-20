using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 → 지상. 상단(올라섬) 또는 하단(발이 땅)에 닿으면 이탈한다.
    /// 상단 이탈 시 위로 올려놓는 텔레포트는 <see cref="ClimbState"/>.Exit 가 수행한다(판정은 센서 소유).
    /// </summary>
    public sealed class ClimbToGroundTransition : ITransitionRule
    {
        private readonly ClimbSensor _sensor;
        private readonly Transform _player;

        public ClimbToGroundTransition(ClimbSensor sensor, Transform player)
        {
            _sensor = sensor;
            _player = player;
        }

        public StateKind NextState => StateKind.Ground;

        public bool ShouldTransition(float deltaTime)
        {
            if (_sensor == null || _player == null) return true; // 배선이 없으면 사다리에 갇히지 않게 즉시 복귀
            return _sensor.ShouldDetach(_player.position, out _);
        }
    }
}

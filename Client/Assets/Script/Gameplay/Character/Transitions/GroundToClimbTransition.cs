namespace Game.Gameplay.Character
{
    /// <summary>
    /// 지상 → 사다리. 사다리(<see cref="Ladder"/>)와 상호작용해 부착 요청이 들어왔을 때만 전이한다.
    /// 요청은 one-shot(<see cref="ClimbSensor.ConsumeAttach"/>) — 입력 소비 규약과 동일.
    /// </summary>
    public sealed class GroundToClimbTransition : ITransitionRule
    {
        private readonly ClimbSensor _sensor;

        public GroundToClimbTransition(ClimbSensor sensor) => _sensor = sensor;

        public StateKind NextState => StateKind.Climb;

        public bool ShouldTransition(float deltaTime) => _sensor != null && _sensor.ConsumeAttach();
    }
}

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 → 낙하. 점프(Space) 이탈 전용 — 사다리 중간에서도 언제든 빠져나올 수 있어야 한다.
    /// 밀어내기(반대쪽으로 튕겨나감)는 <see cref="ClimbState"/>.Exit 가 하고, 여기선 "언제 빠질지"만 답한다.
    /// 순서 주의: 이 규칙을 상/하단 이탈(ClimbToGround)보다 <b>먼저</b> 등록해야 점프가 우선한다.
    /// </summary>
    public sealed class ClimbToFallTransition : ITransitionRule
    {
        private readonly ClimbSensor _sensor;

        public ClimbToFallTransition(ClimbSensor sensor) => _sensor = sensor;

        public StateKind NextState => StateKind.Fall;

        public bool ShouldTransition(float deltaTime) => _sensor != null && _sensor.JumpOffRequested;
    }
}

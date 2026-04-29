using Game.Main.Character.Input;

namespace Game.Main.Character
{
    public class GroundToAttackTransition : ITransitionRule
    {
        private readonly ICharacterInputSource _inputSource;

        public StateKind NextState => StateKind.Attack;

        public GroundToAttackTransition(ICharacterInputSource inputSource)
        {
            _inputSource = inputSource;
        }

        public bool ShouldTransition(float deltaTime)
        {
            return _inputSource.ConsumeAttackPressed();
        }
    }
}

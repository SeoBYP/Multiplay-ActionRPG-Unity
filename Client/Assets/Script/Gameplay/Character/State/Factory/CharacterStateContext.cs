using Game.Gameplay.Character.Input;
using Game.Gameplay;
using Script.System.GamePlayAbilitySystem;

namespace Game.Gameplay.Character
{
    public sealed class CharacterStateContext
    {
        public CharacterMotor Motor;
        public GroundedDetector GroundDetector;
        public CharacterAgentAnimations Animations;
        public ICharacterInputSource InputSource;
        public AbilitySystemComponent AbilitySystem;
        public LocomotionSettings LocomotionSettings;

        // Motion Matching 드라이버. null이면 기존 Animator 파라미터 방식으로 동작한다.
        public IMotionMatchingDriver MotionMatching;
    }
}

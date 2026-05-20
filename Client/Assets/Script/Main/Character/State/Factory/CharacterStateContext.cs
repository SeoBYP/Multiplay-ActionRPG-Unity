using Game.Main.Character.Input;
using Script.Main;
using Script.System.GamePlayAbilitySystem;

namespace Game.Main.Character
{
    public sealed class CharacterStateContext
    {
        public CharacterMotor Motor;
        public GroundedDetector GroundDetector;
        public CharacterAgentAnimations Animations;
        public ICharacterInputSource InputSource;
        public InteractionDetector InteractionDetector;
        public CharacterHitEventReceiver HitEventReceiver;
        public AbilitySystemComponent AbilitySystem;
        public LocomotionSettings LocomotionSettings;

        // Motion Matching 드라이버. null이면 기존 Animator 파라미터 방식으로 동작한다.
        public IMotionMatchingDriver MotionMatching;
    }
}

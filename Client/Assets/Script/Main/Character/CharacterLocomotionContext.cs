using Game.Main.Character.Input;
using Script.Main;

namespace Game.Main.Character
{
    public readonly struct CharacterLocomotionContext
    {
        public CharacterMotor Motor { get; }
        public GroundedDetector GroundDetector { get; }
        public CharacterAgentAnimations Animations { get; }
        public ICharacterInputSource InputSource { get; }
        public InteractionDetector InteractionDetector { get; }

        public CharacterLocomotionContext(CharacterMotor motor, GroundedDetector groundDetector,
            CharacterAgentAnimations animations, ICharacterInputSource inputSource, InteractionDetector interactionDetector)
        {
            Motor = motor;
            GroundDetector = groundDetector;
            Animations = animations;
            InputSource = inputSource;
            InteractionDetector = interactionDetector;
        }
    }
}

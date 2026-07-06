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
    }
}

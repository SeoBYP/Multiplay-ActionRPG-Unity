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
        public GasComponent AbilitySystem;
        public LocomotionSettings LocomotionSettings;
        public ClimbSensor ClimbSensor; // P6: 사다리 부착 신호(없으면 Climb 전이 자체가 안 생긴다)
    }
}

namespace Game.Main.Character
{
    public class NpcCharacterAgent : CharacterAgent
    {
        protected override void Awake()
        {
            base.Awake();
                        
            Context = new CharacterStateContext
            {
                Motor = Motor,
                GroundDetector = GroundDetector,
                Animations = AgentAnimations,
                InputSource = InputSource,
                AbilitySystem = AbilitySystem,
                HitEventReceiver = GetComponent<CharacterHitEventReceiver>(),
                LocomotionSettings = settings,
            };
        }
    }
}

namespace Game.Gameplay.Character
{
    public class LandState : State
    {
        private CharacterAgentAnimations _agentAnimations;

        public LandState(CharacterAgentAnimations agentAnimations)
        {
            _agentAnimations = agentAnimations;
        }

        public override void Enter()
        {
            _agentAnimations.SetFloat(AnimationFloatType.Speed, 0);
            _agentAnimations.SetTrigger(AnimationTriggerType.Land);
        }

        public override void Exit()
        {
            return;
        }


        protected override void StateUpdate(float deltaTime)
        {
            return;
        }
    }
}
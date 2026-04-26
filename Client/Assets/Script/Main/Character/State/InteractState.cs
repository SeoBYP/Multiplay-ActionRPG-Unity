namespace Game.Main.Character
{
    public class InteractState : State
    {
        private readonly CharacterAgentAnimations _animations;
        private readonly InteractionDetector _interactionDetector;
        private readonly float _invokeDelay;

        private float _remainingInvokeDelay;
        private bool _interactionInvoked;

        public InteractState(
            CharacterAgentAnimations animations,
            InteractionDetector interactionDetector,
            float invokeDelay)
        {
            _animations = animations;
            _interactionDetector = interactionDetector;
            _invokeDelay = invokeDelay;
        }

        public override void Enter()
        {
            _remainingInvokeDelay = _invokeDelay;
            _interactionInvoked = false;
            _animations.SetFloat(AnimationFloatType.Speed, 0f);
            _animations.SetTrigger(AnimationTriggerType.Interact);
        }

        protected override void StateUpdate(float deltaTime)
        {
            if (_interactionInvoked)
                return;

            _remainingInvokeDelay -= deltaTime;
            if (_remainingInvokeDelay > 0f)
                return;

            _interactionDetector.CurrentInteractable?.Interact(_interactionDetector.gameObject);
            _interactionInvoked = true;
        }

        public override void Exit()
        {
            _animations.ResetTrigger(AnimationTriggerType.Interact);
        }
    }
}

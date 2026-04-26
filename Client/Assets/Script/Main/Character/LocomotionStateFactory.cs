using System;

namespace Game.Main.Character
{
    public class LocomotionStateFactory : ILocomotionStateFactory
    {
        private readonly LocomotionSettings _settings;

        public LocomotionStateFactory(LocomotionSettings settings)
        {
            _settings = settings;
        }

        public State Create(Type stateType, CharacterLocomotionContext context)
        {
            State newState = null;

            if (stateType == typeof(GroundState))
            {
                newState = new GroundState(
                    context.Motor,
                    context.GroundDetector,
                    context.Animations,
                    context.InputSource,
                    _settings);

                newState.AddTransition(new GroundedToFallTransition(context.GroundDetector));
                if (context.InputSource != null)
                {
                    newState.AddTransition(new GroundedToJumpTransition(
                        context.InputSource,
                        0.2f));
                }
                if (context.InputSource != null && context.InteractionDetector != null)
                {
                    newState.AddTransition(new GroundToInteractTransition(
                        context.InputSource,
                        context.InteractionDetector));
                }
            }
            else if (stateType == typeof(JumpState))
            {
                newState = new JumpState(
                    context.Motor, 
                    context.Animations, 
                    context.InputSource, 
                    _settings);

                newState.AddTransition(new JumpToFallTransition(context.Motor, _settings.JumpToFallDelay));
            }
            else if (stateType == typeof(FallState))
            {
                newState = new FallState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    _settings);

                newState.AddTransition(new FallToLandTransition(context.GroundDetector));
            }
            else if (stateType == typeof(LandState))
            {
                newState = new LandState(context.Animations);
                newState.AddTransition(new LandToMovementTransition(_settings.LandDuration));
            }
            else if (stateType == typeof(InteractState))
            {
                newState = new InteractState(
                    context.Animations,
                    context.InteractionDetector,
                    _settings.InteractInvokeDelay);
                newState.AddTransition(new InteractToGroundTransition(_settings.InteractReturnDelay));
            }
            else
            {
                throw new Exception($"Type not handled {stateType}");
            }

            return newState;
        }
    }
}

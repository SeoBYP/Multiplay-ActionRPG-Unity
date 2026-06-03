using System;

namespace Game.Gameplay.Character
{
    public class StateFactory : IStateFactory
    {
        public State Create(StateDefinition definition, CharacterStateContext context)
        {
            return definition.Kind switch
            {
                StateKind.Ground => new GroundState(
                    context.Motor,
                    context.GroundDetector,
                    context.Animations,
                    context.InputSource,
                    context.LocomotionSettings,
                    context.MotionMatching),

                StateKind.Jump => new JumpState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    context.LocomotionSettings),

                StateKind.Fall => new FallState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    context.LocomotionSettings),

                StateKind.Land => new LandState(context.Animations),

                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

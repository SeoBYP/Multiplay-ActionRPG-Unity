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
                    context.AbilitySystem),

                StateKind.Jump => new JumpState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    context.LocomotionSettings,
                    context.AbilitySystem),

                StateKind.Fall => new FallState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    context.LocomotionSettings,
                    context.AbilitySystem),

                StateKind.Land => new LandState(context.Animations),

                StateKind.Climb => new ClimbState(
                    context.Motor,
                    context.Animations,
                    context.InputSource,
                    context.ClimbSensor,
                    context.LocomotionSettings),

                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

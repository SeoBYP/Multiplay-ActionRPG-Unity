using System;
using System.Collections.Generic;

namespace Game.Gameplay.Character
{
    public class StateMachineBuilder : IStateMachineBuilder
    {
        private readonly IStateFactory _stateFactory;

        public StateMachineBuilder(IStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        public StateKind GetInitialState(CharacterStateConfig config)
        {
            return config != null ? config.InitialState : StateKind.Ground;
        }

        public bool TryCreateState(
            StateKind stateKind,
            CharacterStateConfig config,
            CharacterStateContext context,
            out State state)
        {
            state = null;

            if (config == null || context == null)
                return false;

            Dictionary<StateKind, StateDefinition> states = BuildStateLookup(config);
            if (!states.TryGetValue(stateKind, out StateDefinition definition))
                return false;

            state = _stateFactory.Create(definition, context);
            AddTransitions(state, definition, context, states);
            return true;
        }

        private static Dictionary<StateKind, StateDefinition> BuildStateLookup(CharacterStateConfig config)
        {
            Dictionary<StateKind, StateDefinition> states = new();

            if (config.States == null)
                return states;

            foreach (StateDefinition stateDefinition in config.States)
            {
                states[stateDefinition.Kind] = stateDefinition;
            }

            return states;
        }

        private static void AddTransitions(
            State state,
            StateDefinition definition,
            CharacterStateContext context,
            IReadOnlyDictionary<StateKind, StateDefinition> availableStates)
        {
            switch (definition.Kind)
            {
                case StateKind.Ground:
                    if (availableStates.ContainsKey(StateKind.Fall))
                    {
                        state.AddTransition(new GroundedToFallTransition(context.GroundDetector));
                    }
                    if (context.InputSource != null && availableStates.ContainsKey(StateKind.Jump))
                    {
                        state.AddTransition(new GroundedToJumpTransition(
                            context.InputSource,
                            context.LocomotionSettings.JumpToFallDelay));
                    }
                    if (context.InputSource != null && availableStates.ContainsKey(StateKind.Attack))
                    {
                        state.AddTransition(new GroundToAttackTransition(context.InputSource));
                    }
                    if (context.InputSource != null &&
                        context.InteractionDetector != null &&
                        availableStates.ContainsKey(StateKind.Interact))
                    {
                        state.AddTransition(new GroundToInteractTransition(
                            context.InputSource,
                            context.InteractionDetector));
                    }
                    break;

                case StateKind.Jump:
                    if (availableStates.ContainsKey(StateKind.Fall))
                    {
                        state.AddTransition(new JumpToFallTransition(
                            context.Motor,
                            context.LocomotionSettings.JumpToFallDelay));
                    }
                    break;

                case StateKind.Fall:
                    if (availableStates.ContainsKey(StateKind.Land))
                    {
                        state.AddTransition(new FallToLandTransition(context.GroundDetector));
                    }
                    break;

                case StateKind.Land:
                    if (availableStates.ContainsKey(StateKind.Ground))
                    {
                        state.AddTransition(new LandToGroundTransition(
                            GetDuration(definition, context.LocomotionSettings.LandDuration)));
                    }
                    break;

                case StateKind.Interact:
                    if (availableStates.ContainsKey(StateKind.Ground))
                    {
                        state.AddTransition(new InteractToGroundTransition(
                            GetDuration(definition, context.LocomotionSettings.InteractReturnDelay)));
                    }
                    break;

                case StateKind.Attack:
                    if (availableStates.ContainsKey(StateKind.Ground))
                    {
                        state.AddTransition(new AttackToGroundTransition(
                            GetDuration(definition, 0.5f)));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(definition.Kind), definition.Kind, null);
            }
        }

        private static float GetDuration(StateDefinition definition, float fallback)
        {
            return definition.Duration > 0f ? definition.Duration : fallback;
        }
    }
}

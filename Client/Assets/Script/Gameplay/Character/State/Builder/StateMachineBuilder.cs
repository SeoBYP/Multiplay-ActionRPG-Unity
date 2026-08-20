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
                    // P6: 사다리 상호작용 요청이 있으면 Climb 으로. 센서가 없는 캐릭터(NPC 등)는 전이 자체를 안 만든다.
                    if (context.ClimbSensor != null && availableStates.ContainsKey(StateKind.Climb))
                    {
                        state.AddTransition(new GroundToClimbTransition(context.ClimbSensor));
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

                case StateKind.Climb:
                    // 점프 이탈이 상/하단 이탈보다 우선 — 사다리 중간에서도 Space 로 빠져나올 수 있어야 한다.
                    if (context.ClimbSensor != null && availableStates.ContainsKey(StateKind.Fall))
                    {
                        state.AddTransition(new ClimbToFallTransition(context.ClimbSensor));
                    }
                    if (availableStates.ContainsKey(StateKind.Ground))
                    {
                        state.AddTransition(new ClimbToGroundTransition(
                            context.ClimbSensor,
                            context.Motor != null ? context.Motor.transform : null));
                    }
                    break;

                case StateKind.Land:
                    if (availableStates.ContainsKey(StateKind.Ground))
                    {
                        state.AddTransition(new LandToGroundTransition(
                            GetDuration(definition, context.LocomotionSettings.LandDuration)));
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

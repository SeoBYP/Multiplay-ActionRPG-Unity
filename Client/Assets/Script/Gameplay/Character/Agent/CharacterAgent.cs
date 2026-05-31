using System;
using Game.Gameplay.Character.Input;
using Game.Gameplay;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    [RequireComponent(typeof(CharacterAgentAnimations))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(GroundedDetector))]
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(AbilitySystemComponent))]
    public abstract class CharacterAgent : MonoBehaviour
    {
        [SerializeField] protected CharacterStateConfig stateConfig;
        [SerializeField] protected LocomotionSettings settings;

        protected IStateMachineBuilder StateMachineBuilder;

        protected ICharacterInputSource InputSource;
        protected GroundedDetector GroundDetector;
        protected CharacterAgentAnimations AgentAnimations;
        protected CharacterMotor Motor;
        protected AbilitySystemComponent AbilitySystem;

        protected State CurrentState;
        protected CharacterStateContext Context;

        [field: SerializeField]
        public StateKind CurrentStateKind { get; private set; }

        [Inject]
        public void Construct(IStateMachineBuilder stateMachineBuilder)
        {
            StateMachineBuilder = stateMachineBuilder;
        }

        protected virtual void Awake()
        {
            InputSource = GetComponent<ICharacterInputSource>();
            GroundDetector = GetComponent<GroundedDetector>();
            AgentAnimations = GetComponent<CharacterAgentAnimations>();
            Motor = GetComponent<CharacterMotor>();
            AbilitySystem = GetComponent<AbilitySystemComponent>();
        }

        protected virtual void Start()
        {
            TransitionToState(StateMachineBuilder.GetInitialState(stateConfig));
        }

        protected virtual void TransitionToState(StateKind stateType)
        {
            if (!StateMachineBuilder.TryCreateState(stateType, stateConfig, Context, out State newState))
                return;

            if (CurrentState != null)
            {
                CurrentState.Exit();
                CurrentState.OnTransition -= TransitionToState;
            }
            
            CurrentState = newState;
            CurrentStateKind = stateType;
            CurrentState.OnTransition += TransitionToState;
            CurrentState.Enter();
        }


        protected virtual void Update()
        {
            CurrentState?.Update(Time.deltaTime);
        }

        protected virtual void FixedUpdate()
        {
            GroundDetector.GroundedCheck();
            AgentAnimations.SetBool(AnimationBoolType.Grounded, GroundDetector.Grounded);
        }
    }
}

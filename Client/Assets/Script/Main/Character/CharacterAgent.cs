using System;
using Game.Main.Character.Input;
using Script.Main;
using UnityEngine;
using VContainer;

namespace Game.Main.Character
{
    [RequireComponent(typeof(CharacterAgentAnimations))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(GroundedDetector))]
    [RequireComponent(typeof(CharacterMotor))]
    public class CharacterAgent : MonoBehaviour
    {
        private ILocomotionStateFactory _stateFactory;

        private ICharacterInputSource _inputSource;
        private GroundedDetector _groundDetector;
        private CharacterAgentAnimations _agentAnimations;
        private CharacterMotor _motor;
        private InteractionDetector _interactionDetector;

        private State _currentState;
        private CharacterLocomotionContext _context;
        
        [Inject]
        public void Construct(ILocomotionStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        private void Awake()
        {
            _inputSource = GetComponent<ICharacterInputSource>();
            _groundDetector = GetComponent<GroundedDetector>();
            _agentAnimations = GetComponent<CharacterAgentAnimations>();
            _motor = GetComponent<CharacterMotor>();
            _interactionDetector = GetComponent<InteractionDetector>();

            _context = new CharacterLocomotionContext(
                _motor,
                _groundDetector,
                _agentAnimations,
                _inputSource,
                _interactionDetector);
        }
        
        private void Start()
        {
            TransitionToState(typeof(GroundState));
        }

        private void TransitionToState(Type stateType)
        {
            State newState = _stateFactory.Create(stateType, _context);

            if (_currentState != null)
            {
                _currentState.Exit();
                _currentState.OnTransition -= TransitionToState;
            }

            _currentState = newState;
            _currentState.OnTransition += TransitionToState;
            _currentState.Enter();
        }


        private void Update()
        {
            _interactionDetector?.DetectInteractable();
            _currentState?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _groundDetector.GroundedCheck();
            _agentAnimations.SetBool(AnimationBoolType.Grounded, _groundDetector.Grounded);
        }
    }
}

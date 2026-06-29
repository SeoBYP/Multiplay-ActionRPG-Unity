using Game.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Gameplay.Character.Input
{
    public class PlayerInputComponent : MonoBehaviour
    {
        private ICharacterInputWriter _inputWriter;
        private PlayerInputActions _inputActions;

        private bool _subscribed;

        private void Awake()
        {
            _inputWriter = GetComponent<ICharacterInputWriter>();
        }
        
        [Inject]
        private void Construct(PlayerInputActions inputActions)
        {
            _inputActions = inputActions;
        }

        private void Start()
        {
            SubscribeInput();
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void SubscribeInput()
        {
            if (_subscribed || _inputActions == null)
                return;

            _inputActions.Player.Move.performed += OnMove;
            _inputActions.Player.Move.canceled += OnMove;
            _inputActions.Player.Look.performed += OnLook;
            _inputActions.Player.Look.canceled += OnLook;
            _inputActions.Player.Sprint.performed += OnSprint;
            _inputActions.Player.Sprint.canceled += OnSprint;
            _inputActions.Player.Jump.performed += OnJump;
            _inputActions.Player.Jump.canceled += OnJump;
            _inputActions.Player.Attack.performed += OnAttack;
            _inputActions.Player.Attack.canceled += OnAttack;
            _inputActions.Player.HeavyAttack.performed += OnHeavyAttack;
            _inputActions.Player.HeavyAttack.canceled += OnHeavyAttack;
            _inputActions.Player.Interact.performed += OnInteract;
            _inputActions.Player.Interact.canceled += OnInteract;
            _inputActions.Player.Dodge.performed += OnDodge;
            _inputActions.Player.Dodge.canceled += OnDodge;
            _inputActions.Player.LockOn.performed += OnLockOn;
            _inputActions.Player.LockOn.canceled += OnLockOn;


            _subscribed = true;
        }



        private void OnDisable()
        {
            UnsubscribeInput();
        }

        private void UnsubscribeInput()
        {
            if (!_subscribed || _inputActions == null)
                return;

            _inputActions.Player.Move.performed -= OnMove;
            _inputActions.Player.Move.canceled -= OnMove;
            _inputActions.Player.Look.performed -= OnLook;
            _inputActions.Player.Look.canceled -= OnLook;
            _inputActions.Player.Sprint.performed -= OnSprint;
            _inputActions.Player.Sprint.canceled -= OnSprint;
            _inputActions.Player.Jump.performed -= OnJump;
            _inputActions.Player.Jump.canceled -= OnJump;
            _inputActions.Player.Attack.performed -= OnAttack;
            _inputActions.Player.Attack.canceled -= OnAttack;
            _inputActions.Player.HeavyAttack.performed -= OnHeavyAttack;
            _inputActions.Player.HeavyAttack.canceled -= OnHeavyAttack;
            _inputActions.Player.Interact.performed -= OnInteract;
            _inputActions.Player.Interact.canceled -= OnInteract;
            _inputActions.Player.Dodge.performed -= OnDodge;
            _inputActions.Player.Dodge.canceled -= OnDodge;
            _inputActions.Player.LockOn.performed -= OnLockOn;
            _inputActions.Player.LockOn.canceled -= OnLockOn;
            _subscribed = false;
        }


        private void OnMove(InputAction.CallbackContext context)
        {
            _inputWriter.SetMove(context.ReadValue<Vector2>());
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            _inputWriter.SetLook(context.ReadValue<Vector2>());
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            _inputWriter.SetSprintHeld(context.ReadValueAsButton());
        }
        
        private void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressInteract();
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressAttack();
        }

        private void OnHeavyAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressHeavyAttack();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressJump();
        }
        
        private void OnDodge(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressDodge();
        }

        private void OnLockOn(InputAction.CallbackContext context)
        {
            if (context.performed)
                _inputWriter.PressLockOn();
        }
    }
}

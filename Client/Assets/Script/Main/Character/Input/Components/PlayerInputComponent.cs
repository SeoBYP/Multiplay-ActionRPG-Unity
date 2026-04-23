using Game.Main.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Main.Character.Input
{
    public class PlayerInputComponent : MonoBehaviour
    {
        private ICharacterInputWriter _inputWriter;
        private PlayerInputActions _inputActions;

        private bool _subscribed;

        [Inject]
        private void Construct(PlayerInputActions inputActions, ICharacterInputWriter inputWriter)
        {
            _inputActions = inputActions;
            _inputWriter = inputWriter;
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
            _inputActions.Player.Interact.performed += OnInteract;
            _inputActions.Player.Interact.canceled += OnInteract;
            _inputActions.Player.Dodge.performed += OnDodge;
            _inputActions.Player.Dodge.canceled += OnDodge;


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
            _inputActions.Player.Interact.performed -= OnInteract;
            _inputActions.Player.Interact.canceled -= OnInteract;
            _inputActions.Player.Dodge.performed -= OnDodge;
            _inputActions.Player.Dodge.canceled -= OnDodge;
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
    }
}
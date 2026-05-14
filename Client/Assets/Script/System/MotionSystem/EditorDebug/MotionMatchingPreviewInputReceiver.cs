using System;
using System.Reflection;
using UnityEngine;

namespace Game.System.MotionSystem
{
    public sealed class MotionMatchingPreviewInputReceiver : MonoBehaviour
    {
        [SerializeField] private Vector2 moveInput;
        [SerializeField] private Vector3 worldMoveDirection;
        [SerializeField] private bool sprintHeld;
        [SerializeField] private bool jumpPressed;
        [SerializeField] private bool dodgePressed;
        [SerializeField] private bool interactPressed;
        [SerializeField] private bool attackPressed;
        [SerializeField] private string lastEvent;

        private Component _inputBuffer;
        private MethodInfo _setMove;
        private MethodInfo _setSprintHeld;
        private MethodInfo _pressJump;
        private MethodInfo _pressDodge;
        private MethodInfo _pressInteract;
        private MethodInfo _pressAttack;

        public Vector2 MoveInput => moveInput;
        public Vector3 WorldMoveDirection => worldMoveDirection;
        public bool SprintHeld => sprintHeld;
        public string LastEvent => lastEvent;

        public void ReceiveEditorInput(
            Vector2 move,
            Vector3 worldDirection,
            bool sprint,
            bool jump,
            bool dodge,
            bool interact,
            bool attack)
        {
            moveInput = Vector2.ClampMagnitude(move, 1f);
            worldMoveDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.zero;
            sprintHeld = sprint;
            jumpPressed = jump;
            dodgePressed = dodge;
            interactPressed = interact;
            attackPressed = attack;

            EnsureInputBuffer();
            _setMove?.Invoke(_inputBuffer, new object[] { moveInput });
            _setSprintHeld?.Invoke(_inputBuffer, new object[] { sprintHeld });

            if (jumpPressed)
                DispatchEvent(_pressJump, "Jump");
            if (dodgePressed)
                DispatchEvent(_pressDodge, "Dodge");
            if (interactPressed)
                DispatchEvent(_pressInteract, "Interact");
            if (attackPressed)
                DispatchEvent(_pressAttack, "Attack");
        }

        private void DispatchEvent(MethodInfo method, string eventName)
        {
            method?.Invoke(_inputBuffer, Array.Empty<object>());
            lastEvent = eventName;
        }

        private void EnsureInputBuffer()
        {
            if (_inputBuffer != null)
                return;

            foreach (Component component in GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "CharacterInputBuffer")
                    continue;

                _inputBuffer = component;
                Type type = component.GetType();
                _setMove = type.GetMethod("SetMove", new[] { typeof(Vector2) });
                _setSprintHeld = type.GetMethod("SetSprintHeld", new[] { typeof(bool) });
                _pressJump = type.GetMethod("PressJump", Type.EmptyTypes);
                _pressDodge = type.GetMethod("PressDodge", Type.EmptyTypes);
                _pressInteract = type.GetMethod("PressInteract", Type.EmptyTypes);
                _pressAttack = type.GetMethod("PressAttack", Type.EmptyTypes);
                return;
            }
        }
    }
}

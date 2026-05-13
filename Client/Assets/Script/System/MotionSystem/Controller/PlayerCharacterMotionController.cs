using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 프로젝트의 PlayerCharacter 입력/이동 컴포넌트를 QuanticBrains식 Motion Matching 런타임에 연결하는 어댑터입니다.
    /// MotionMatching은 CharacterControllerBase만 알면 되므로, Player와 NPC는 각자 다른 어댑터를 붙여도
    /// 같은 Dataset, Avatar, QueryFlow 구조를 공유할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "MotionMatching/Character Controller/Project Player Character")]
    public class PlayerCharacterMotionController : CharacterControllerBase
    {
        [SerializeField]
        private bool applyRootMovement;

        private CharacterController _characterController;
        private Component _inputBuffer;
        private Component _motor;
        private PropertyInfo _currentInputProperty;
        private PropertyInfo _moveProperty;
        private PropertyInfo _sprintHeldProperty;
        private PropertyInfo _desiredFacingDirectionProperty;
        private Vector3 _lastRootPosition;
        private bool _wasMoving;
        private bool _wasSprinting;
        private bool _isStrafing;
        private string _lastSentQuery = string.Empty;
        private string _debugExpectedQuery = "Idle";
        private string _debugQueryReason = "Not initialized";

        public override string DebugExpectedQuery => _debugExpectedQuery;
        public override string DebugQueryReason => _debugQueryReason;

        public override void Initialize(MonoBehaviour mb)
        {
            base.Initialize(mb);

            _characterController = mb.GetComponent<CharacterController>();
            _inputBuffer = FindComponentByTypeName(mb, "CharacterInputBuffer");
            _motor = FindComponentByTypeName(mb, "CharacterMotor");
            CacheInputReflection();
            CacheMotorReflection();
            _lastRootPosition = mb.transform.position;

            if (_inputBuffer == null)
                Debug.LogWarning("[MotionMatching] PlayerCharacterMotionController requires CharacterInputBuffer.", mb);

            if (_characterController == null)
                Debug.LogWarning("[MotionMatching] PlayerCharacterMotionController requires CharacterController.", mb);
        }

        protected override Vector2 GetRawInput()
        {
            if (_inputBuffer == null || _currentInputProperty == null || _moveProperty == null)
                return Vector2.zero;

            object currentInput = _currentInputProperty.GetValue(_inputBuffer);
            return currentInput != null ? (Vector2)_moveProperty.GetValue(currentInput) : Vector2.zero;
        }

        protected override Vector3 GetForward(Vector3 input)
        {
            if (_motor != null && _desiredFacingDirectionProperty != null)
            {
                Vector3 desiredFacingDirection = (Vector3)_desiredFacingDirectionProperty.GetValue(_motor);
                if (desiredFacingDirection.sqrMagnitude > 0.0001f)
                    return desiredFacingDirection;
            }

            Vector3 horizontal = new Vector3(input.x, 0f, input.z);
            return horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : MotionMatching.transform.forward;
        }

        public override void UpdateMotion(float time)
        {
            base.UpdateMotion(time);

            bool isMoving = CurrentRawInput.sqrMagnitude > 0.0001f;
            bool isSprinting = IsSprintHeld();
            _isStrafing = isMoving;
            _debugExpectedQuery = GetExpectedQuery(isMoving, isSprinting, _isStrafing);
            _debugQueryReason = BuildQueryReason(isMoving, isSprinting, _isStrafing);

            SendQueryIfChanged(_debugExpectedQuery);

            _wasMoving = isMoving;
            _wasSprinting = isSprinting;
        }

        public override void Move(float3 position, quaternion rotation, float time)
        {
            // 현재 GroundState/CharacterMotor가 실제 이동을 담당하므로 기본 세팅에서는 root 이동을 적용하지 않습니다.
            // Dataset이 root motion까지 안정화되면 applyRootMovement를 켜서 Pose Player가 root delta를 직접 전달할 수 있습니다.
            if (!applyRootMovement || _characterController == null)
                return;

            Vector3 targetPosition = position;
            Vector3 delta = targetPosition - _lastRootPosition;
            _characterController.Move(delta);
            MotionMatching.transform.rotation = rotation;
            _lastRootPosition = MotionMatching.transform.position;
        }

        protected override void ToggleCollisions(bool isEnabled)
        {
            if (_characterController != null)
                _characterController.detectCollisions = isEnabled;
        }

        protected override void TogglePhysics(bool isEnabled)
        {
        }

        public override bool IsStrafing()
        {
            return _isStrafing;
        }

        private static Component FindComponentByTypeName(MonoBehaviour owner, string typeName)
        {
            foreach (Component component in owner.GetComponents<Component>())
            {
                Type componentType = component.GetType();
                if (componentType.Name == typeName)
                    return component;
            }

            return null;
        }

        private void CacheInputReflection()
        {
            if (_inputBuffer == null)
                return;

            _currentInputProperty = _inputBuffer.GetType().GetProperty("Current");
            Type inputFrameType = _currentInputProperty?.PropertyType;
            _moveProperty = inputFrameType?.GetProperty("Move");
            _sprintHeldProperty = inputFrameType?.GetProperty("SprintHeld");
        }

        private void CacheMotorReflection()
        {
            if (_motor == null)
                return;

            _desiredFacingDirectionProperty = _motor.GetType().GetProperty("DesiredFacingDirection");
        }

        private bool IsSprintHeld()
        {
            if (_inputBuffer == null || _currentInputProperty == null || _sprintHeldProperty == null)
                return false;

            object currentInput = _currentInputProperty.GetValue(_inputBuffer);
            return currentInput != null && (bool)_sprintHeldProperty.GetValue(currentInput);
        }

        private static string GetExpectedQuery(bool isMoving, bool isSprinting, bool isStrafing)
        {
            if (!isMoving)
                return "Idle";

            return isSprinting ? "Run" : "Walk";
        }

        private void SendQueryIfChanged(string query)
        {
            if (_lastSentQuery == query)
                return;

            if (query == "Idle")
            {
                if (MotionMatching.SendIdleQuery())
                    _lastSentQuery = query;
                return;
            }

            MotionMatching.SendQuery(query);

            _lastSentQuery = query;
        }

        private string BuildQueryReason(bool isMoving, bool isSprinting, bool isStrafing)
        {
            Vector3 movement = new Vector3(currentMoveInput.x, 0f, currentMoveInput.z);
            Vector3 facing = new Vector3(currentForward.x, 0f, currentForward.z);
            float dot = movement.sqrMagnitude > 0.0001f && facing.sqrMagnitude > 0.0001f
                ? Vector3.Dot(movement.normalized, facing.normalized)
                : 1f;

            return $"Moving={isMoving}, Sprint={isSprinting}, CameraFacing={isStrafing}, MoveFacingDot={dot:0.00}, LastQuery={_lastSentQuery}";
        }
    }
}

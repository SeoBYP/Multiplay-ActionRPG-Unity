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
        private Vector3 _lastMoveDirection;
        private float _queryLockTimer;
        private string _pendingFollowUpQuery = string.Empty;
        private string _lastSentQuery = string.Empty;
        private string _debugExpectedQuery = "Idle";
        private string _debugQueryReason = "Not initialized";

        private const float TransitionLockSeconds = 0.06f;
        private const float SpeedTransitionLockSeconds = 0.12f;
        private const float PivotLockSeconds = 0.10f;
        private const float ArcLockSeconds = 0.08f;
        private const float PivotDotThreshold = -0.35f;
        private const float DirectionChangeDotThreshold = 0.98f;

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
            string expectedLoopQuery = GetExpectedQuery(isMoving, isSprinting);
            _debugExpectedQuery = expectedLoopQuery;
            _debugQueryReason = BuildQueryReason(isMoving, isSprinting, _isStrafing);

            _queryLockTimer = Mathf.Max(0f, _queryLockTimer - time);

            if (TrySendImmediateTransitionQuery(isMoving, isSprinting, expectedLoopQuery))
            {
                RememberMovementState(isMoving, isSprinting);
                return;
            }

            if (_queryLockTimer > 0f)
            {
                RememberMovementState(isMoving, isSprinting);
                return;
            }

            if (MotionMatching.IsActionQueryPlaying())
            {
                if (IsSpeedTransitionQuery(_lastSentQuery) && _queryLockTimer <= 0f)
                {
                    SendQueryIfChanged(expectedLoopQuery, force: true);
                    _pendingFollowUpQuery = string.Empty;
                    RememberMovementState(isMoving, isSprinting);
                    return;
                }

                UpdatePendingFollowUpQuery(expectedLoopQuery);
                RememberMovementState(isMoving, isSprinting);
                return;
            }

            if (!string.IsNullOrEmpty(_pendingFollowUpQuery))
            {
                SendQueryIfChanged(_pendingFollowUpQuery);
                _pendingFollowUpQuery = string.Empty;
                RememberMovementState(isMoving, isSprinting);
                return;
            }

            if (!TrySendTransitionQuery(isMoving, isSprinting, expectedLoopQuery))
                SendQueryIfChanged(expectedLoopQuery);

            RememberMovementState(isMoving, isSprinting);
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

        private string GetExpectedQuery(bool isMoving, bool isSprinting)
        {
            if (!isMoving)
                return "Idle";

            string baseQuery = isSprinting ? "Run" : "Walk";
            string directionalQuery = baseQuery + GetDirectionCode(Flatten(currentMoveInput));
            return MotionMatching.HasQuery(directionalQuery) ? directionalQuery : baseQuery;
        }

        private bool TrySendTransitionQuery(bool isMoving, bool isSprinting, string expectedLoopQuery)
        {
            if (!_wasMoving && isMoving)
            {
                if (isSprinting &&
                    SendDirectionalActionQueryIfAvailable("IdleToRun", GetDirectionCode(Flatten(currentMoveInput)), expectedLoopQuery, TransitionLockSeconds))
                    return true;

                return SendDirectionalActionQueryIfAvailable("IdleToWalk", GetDirectionCode(Flatten(currentMoveInput)), expectedLoopQuery, TransitionLockSeconds);
            }

            if (_wasMoving && !isMoving)
            {
                string stopDirection = GetDirectionCode(_lastMoveDirection);
                return SendDirectionalActionQueryIfAvailable(_wasSprinting ? "RunToStop" : "WalkToStop", stopDirection, "Idle", TransitionLockSeconds);
            }

            if (_wasMoving && isMoving && !_wasSprinting && isSprinting)
                return SendActionQueryIfAvailable("WalkToRun", "Run", SpeedTransitionLockSeconds);

            if (_wasMoving && isMoving && _wasSprinting && !isSprinting)
                return SendActionQueryIfAvailable("RunToWalk", "Walk", SpeedTransitionLockSeconds);

            if (!_wasMoving || !isMoving || _lastMoveDirection.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 currentMoveDirection = Flatten(currentMoveInput).normalized;
            float directionDot = Vector3.Dot(_lastMoveDirection, currentMoveDirection);
            return TrySendDirectionChangeQuery(isSprinting, expectedLoopQuery, currentMoveDirection, directionDot, PivotLockSeconds, ArcLockSeconds);
        }

        private bool TrySendImmediateTransitionQuery(bool isMoving, bool isSprinting, string expectedLoopQuery)
        {
            if (_wasMoving && !isMoving)
            {
                string stopDirection = GetDirectionCode(_lastMoveDirection);
                return SendDirectionalActionQueryIfAvailable(
                    _wasSprinting ? "RunToStop" : "WalkToStop",
                    stopDirection,
                    "Idle",
                    0f);
            }

            if (!_wasMoving && isMoving)
            {
                string startDirection = GetDirectionCode(Flatten(currentMoveInput));
                if (isSprinting &&
                    SendDirectionalActionQueryIfAvailable("IdleToRun", startDirection, expectedLoopQuery, 0f))
                    return true;

                return SendDirectionalActionQueryIfAvailable("IdleToWalk", startDirection, expectedLoopQuery, 0f);
            }

            if (_wasMoving && isMoving && !_wasSprinting && isSprinting)
            {
                if (TrySendImmediateDirectionChange(isSprinting, expectedLoopQuery))
                    return true;

                return SendActionQueryIfAvailable("WalkToRun", expectedLoopQuery, SpeedTransitionLockSeconds);
            }

            if (_wasMoving && isMoving && _wasSprinting && !isSprinting)
            {
                if (TrySendImmediateDirectionChange(isSprinting, expectedLoopQuery))
                    return true;

                return SendActionQueryIfAvailable("RunToWalk", expectedLoopQuery, SpeedTransitionLockSeconds);
            }

            return TrySendImmediateDirectionChange(isSprinting, expectedLoopQuery);
        }

        private bool TrySendImmediateDirectionChange(bool isSprinting, string expectedLoopQuery)
        {
            Vector3 currentMoveDirection = Flatten(currentMoveInput);
            if (!_wasMoving || currentMoveDirection.sqrMagnitude <= 0.0001f || _lastMoveDirection.sqrMagnitude <= 0.0001f)
                return false;

            currentMoveDirection.Normalize();
            float directionDot = Vector3.Dot(_lastMoveDirection, currentMoveDirection);
            return TrySendDirectionChangeQuery(isSprinting, expectedLoopQuery, currentMoveDirection, directionDot, PivotLockSeconds, ArcLockSeconds);
        }

        private bool TrySendDirectionChangeQuery(
            bool isSprinting,
            string expectedLoopQuery,
            Vector3 currentMoveDirection,
            float directionDot,
            float pivotLockSeconds,
            float arcLockSeconds)
        {
            if (directionDot >= DirectionChangeDotThreshold)
                return false;

            float signedTurnAngle = Vector3.SignedAngle(_lastMoveDirection, currentMoveDirection, Vector3.up);
            float absTurnAngle = Mathf.Abs(signedTurnAngle);
            bool triedTurnBeforePivot = false;
            if (absTurnAngle < 145f &&
                SendTurnQueryIfAvailable(isSprinting, signedTurnAngle, expectedLoopQuery, arcLockSeconds))
            {
                triedTurnBeforePivot = true;
                return true;
            }

            if (directionDot < PivotDotThreshold &&
                SendPivotQueryIfAvailable(isSprinting, currentMoveDirection, expectedLoopQuery, pivotLockSeconds))
            {
                return true;
            }

            if (!triedTurnBeforePivot &&
                SendTurnQueryIfAvailable(isSprinting, signedTurnAngle, expectedLoopQuery, arcLockSeconds))
            {
                return true;
            }

            string turnSide = GetTurnSideSuffix(signedTurnAngle);
            string arcQuery = ShouldUseCircleTurn()
                ? isSprinting ? "RunCircle" : "WalkCircle"
                : isSprinting ? "RunArc" : "WalkArc";

            return SendTemporaryMotionQueryIfAvailable(arcQuery + turnSide, expectedLoopQuery, arcLockSeconds) ||
                   SendTemporaryMotionQueryIfAvailable(arcQuery, expectedLoopQuery, arcLockSeconds) ||
                   SendQueryIfChanged(expectedLoopQuery, force: true);
        }

        private string GetDirectionCode(Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= 0.0001f)
                return "F";

            Vector3 local = MotionMatching.transform.InverseTransformDirection(worldDirection.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (angle < 0f)
                angle += 360f;

            if (angle < 22.5f || angle >= 337.5f) return "F";
            if (angle < 67.5f) return "FR";
            if (angle < 112.5f) return "RR";
            if (angle < 157.5f) return "BR";
            if (angle < 202.5f) return "B";
            if (angle < 247.5f) return "BL";
            if (angle < 292.5f) return "LL";
            return "FL";
        }

        private bool SendActionQueryIfAvailable(string query, string followUpQuery, float lockSeconds)
        {
            if (!MotionMatching.HasQuery(query))
                return false;

            if (!MotionMatching.SendActionQuery(query, followUpQuery))
                return false;

            _lastSentQuery = query;
            _pendingFollowUpQuery = followUpQuery;
            _queryLockTimer = lockSeconds;
            return true;
        }

        private bool SendDirectionalActionQueryIfAvailable(string baseQuery, string direction, string followUpQuery, float lockSeconds)
        {
            foreach (string candidateDirection in GetDirectionFallbacks(direction))
            {
                if (SendActionQueryIfAvailable(baseQuery + candidateDirection, followUpQuery, lockSeconds))
                    return true;
            }

            return SendActionQueryIfAvailable(baseQuery, followUpQuery, lockSeconds);
        }

        private bool SendTurnQueryIfAvailable(
            bool isSprinting,
            float signedTurnAngle,
            string followUpQuery,
            float lockSeconds)
        {
            foreach (string turnQuery in GetTurnQueryCandidates(isSprinting, signedTurnAngle))
            {
                if (SendActionQueryIfAvailable(turnQuery, followUpQuery, lockSeconds))
                    return true;
            }

            return false;
        }

        private bool SendPivotQueryIfAvailable(
            bool isSprinting,
            Vector3 currentMoveDirection,
            string followUpQuery,
            float lockSeconds)
        {
            string baseQuery = isSprinting ? "RunPivot" : "WalkPivot";
            string from = GetDirectionCode(_lastMoveDirection);
            string to = GetDirectionCode(currentMoveDirection);

            foreach (string pivotQuery in GetPivotFallbackQueries(baseQuery, from, to))
            {
                if (SendActionQueryIfAvailable(pivotQuery, followUpQuery, lockSeconds))
                    return true;
            }

            return false;
        }

        private static string[] GetPivotFallbackQueries(string baseQuery, string from, string to)
        {
            string exact = baseQuery + from + to;
            string opposite = baseQuery + from + GetOppositeDirection(from);
            string reverseOpposite = baseQuery + GetOppositeDirection(to) + to;
            return new[] { exact, opposite, reverseOpposite, baseQuery };
        }

        private static string[] GetTurnQueryCandidates(bool isSprinting, float signedTurnAngle)
        {
            float absAngle = Mathf.Abs(signedTurnAngle);
            if (absAngle < 35f)
                return Array.Empty<string>();

            string side = GetTurnSideSuffix(signedTurnAngle);
            string oppositeSide = side == "L" ? "R" : "L";
            string angle = GetNearestTurnAngle(absAngle, isSprinting);
            if (string.IsNullOrEmpty(angle))
                return Array.Empty<string>();

            if (isSprinting)
            {
                return angle == "180"
                    ? new[] { "RunTurn" + side + angle, "RunTurn" + oppositeSide + angle, "RunReface" + side + angle, "RunReface" + oppositeSide + angle }
                    : new[] { "RunTurn" + side + angle, "RunReface" + side + angle };
            }

            return angle == "180"
                ? new[] { "WalkReface" + side + angle, "WalkReface" + oppositeSide + angle }
                : new[] { "WalkReface" + side + angle };
        }

        private static string GetNearestTurnAngle(float absAngle, bool includeRunAngles)
        {
            if (absAngle >= 157.5f)
                return "180";
            if (absAngle >= 112.5f)
                return includeRunAngles ? "135" : string.Empty;
            if (absAngle >= 67.5f)
                return "090";
            if (includeRunAngles && absAngle >= 35f)
                return "045";

            return string.Empty;
        }

        private static string GetTurnSideSuffix(float signedTurnAngle)
        {
            return signedTurnAngle < 0f ? "L" : "R";
        }

        private static string GetOppositeDirection(string direction)
        {
            return direction switch
            {
                "F" => "B",
                "FR" => "BL",
                "RR" => "LL",
                "BR" => "FL",
                "B" => "F",
                "BL" => "FR",
                "LL" => "RR",
                "FL" => "BR",
                "LR" => "RR",
                "RL" => "LL",
                _ => "B"
            };
        }

        private static string[] GetDirectionFallbacks(string direction)
        {
            return direction switch
            {
                "FL" => new[] { "FL", "F", "LL" },
                "FR" => new[] { "FR", "F", "RR" },
                "BL" => new[] { "BL", "B", "LL" },
                "BR" => new[] { "BR", "B", "RR" },
                "LL" => new[] { "LL", "FL", "BL" },
                "RR" => new[] { "RR", "FR", "BR" },
                "B" => new[] { "B", "BL", "BR" },
                _ => new[] { "F", "FL", "FR" }
            };
        }

        private void UpdatePendingFollowUpQuery(string expectedLoopQuery)
        {
            if (string.IsNullOrEmpty(_pendingFollowUpQuery))
                return;

            _pendingFollowUpQuery = expectedLoopQuery;
        }

        private static bool IsSpeedTransitionQuery(string query)
        {
            return query == "WalkToRun" || query == "RunToWalk";
        }

        private bool SendTemporaryMotionQueryIfAvailable(string query, string followUpQuery, float lockSeconds)
        {
            if (!MotionMatching.HasQuery(query))
                return false;

            MotionMatching.SendQuery(query);
            _lastSentQuery = query;
            _pendingFollowUpQuery = followUpQuery;
            _queryLockTimer = lockSeconds;
            return true;
        }

        private bool ShouldUseCircleTurn()
        {
            Vector3 movement = Flatten(currentMoveInput);
            Vector3 facing = Flatten(currentForward);
            if (movement.sqrMagnitude <= 0.0001f || facing.sqrMagnitude <= 0.0001f)
                return false;

            return Vector3.Dot(movement.normalized, facing.normalized) < 0.55f;
        }

        private void RememberMovementState(bool isMoving, bool isSprinting)
        {
            if (isMoving)
                _lastMoveDirection = Flatten(currentMoveInput).normalized;

            _wasMoving = isMoving;
            _wasSprinting = isSprinting;
        }

        private static Vector3 Flatten(float3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private bool SendQueryIfChanged(string query, bool force = false)
        {
            if (!force && _lastSentQuery == query)
                return false;

            if (query == "Idle")
            {
                if (MotionMatching.SendIdleQuery())
                {
                    _lastSentQuery = query;
                    return true;
                }

                return false;
            }

            MotionMatching.SendQuery(query);

            _lastSentQuery = query;
            return true;
        }

        private string BuildQueryReason(bool isMoving, bool isSprinting, bool isStrafing)
        {
            Vector3 movement = new Vector3(currentMoveInput.x, 0f, currentMoveInput.z);
            Vector3 facing = new Vector3(currentForward.x, 0f, currentForward.z);
            float dot = movement.sqrMagnitude > 0.0001f && facing.sqrMagnitude > 0.0001f
                ? Vector3.Dot(movement.normalized, facing.normalized)
                : 1f;

            return $"Moving={isMoving}, Sprint={isSprinting}, CameraFacing={isStrafing}, MoveFacingDot={dot:0.00}, LastQuery={_lastSentQuery}, Lock={_queryLockTimer:0.00}, FollowUp={_pendingFollowUpQuery}";
        }
    }
}

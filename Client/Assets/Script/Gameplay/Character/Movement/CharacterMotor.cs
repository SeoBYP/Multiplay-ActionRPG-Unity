using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    public class CharacterMotor : MonoBehaviour , IAgentMotor
    {
        private LocomotionSettings _settings;
        
        private CharacterController m_controller;
        private float m_speed;
        private float m_rotationVelocity;

        [SerializeField] private AgentRotationStrategy m_rotationStrategy;

        public Vector3 CurrentVelocity { get; private set; }
        public Vector3 DesiredMoveDirection { get; private set; }
        public Vector3 DesiredFacingDirection { get; private set; }

        /// <summary>
        /// 락온(2.6.3) 등 외부가 바라볼 방향을 강제할 때 세팅하는 facing 오버라이드(평면 월드 방향).
        /// 값이 있으면 회전 전략(카메라 기준) 대신 이 방향으로 바라본다. null 이면 기존 전략 사용.
        /// 이동(DesiredMoveDirection)은 영향받지 않는다 — 락온 중에도 이동은 카메라 기준(스트레이프).
        /// </summary>
        public Vector3? FacingOverride { get; set; }

        [Inject]
        public void Construct(LocomotionSettings settings)
        {
            _settings = settings;
        }
        
        private void Awake()
        {
            m_controller = GetComponent<CharacterController>();
            // get a reference to our main camera
            if (m_rotationStrategy == null)
            {
                m_rotationStrategy = GetComponent<AgentRotationStrategy>();
            }
        }


        public void Move(Vector3 input, float speed)
        {
            Vector2 horizontalInput = new Vector2(input.x, input.z);
            CharacterMovementCalculation(horizontalInput, speed);

            Vector3 targetDirection = m_rotationStrategy != null
                ? m_rotationStrategy.MovementDirectionCalculation(horizontalInput, transform)
                : transform.TransformDirection(new Vector3(input.x, 0.0f, input.z)).normalized;
            DesiredMoveDirection = targetDirection;
            // 락온 중이면 facing 을 타겟 방향으로 강제(이동은 위 targetDirection 그대로 = 스트레이프).
            if (FacingOverride.HasValue)
                DesiredFacingDirection = FacingOverride.Value;
            else
                DesiredFacingDirection = m_rotationStrategy != null
                    ? m_rotationStrategy.FacingDirectionCalculation(horizontalInput, transform)
                    : transform.forward;
            ApplyFacingDirection();

            CurrentVelocity = targetDirection * horizontalInput.normalized.magnitude * (m_speed * Time.deltaTime) +
                              new Vector3(0.0f, input.y, 0.0f) * Time.deltaTime;
            //move the character controller
            m_controller.Move(CurrentVelocity);
        }

        /// <summary>
        /// 회피(Dodge) 대시 — 고정 월드 방향으로 일정 속도 이동(입력/카메라 무관). 회피 시작 시 방향을 락한 채
        /// 매 프레임 호출한다. 즉시 그 방향을 바라보고(전환 스무딩 없음), 중력만 아래로 적용해 지면에 붙인다.
        /// 일반 이동(Move)과 달리 가속 램프·회전 스무딩을 거치지 않는다(짧고 빠른 임펄스).
        /// </summary>
        public void Dash(Vector3 worldDir, float speed, bool faceDirection = true)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 0.0001f)
            {
                worldDir.Normalize();
                DesiredMoveDirection = worldDir;
                // 회피=진행 방향을 바라봄. 넉백=밀려나는 거라 회전하지 않는다(faceDirection=false).
                if (faceDirection)
                    transform.rotation = Quaternion.LookRotation(worldDir);
            }

            float gravity = _settings != null ? _settings.Gravity : -15f;
            Vector3 displacement = worldDir * (speed * Time.deltaTime);
            displacement.y = gravity * Time.deltaTime;
            CurrentVelocity = displacement;
            m_controller.Move(displacement);
        }

        /// <summary>
        /// 중력·회전 없이 주어진 월드 변위를 그대로 적용한다(사다리 등 <b>특수 이동 모드</b> 전용).
        /// <see cref="Dash"/> 는 수평 전용(y 를 중력으로 덮어씀)이라 수직 이동에 쓸 수 없어 분리했다.
        /// </summary>
        public void MoveRaw(Vector3 worldDisplacement)
        {
            CurrentVelocity = worldDisplacement;
            m_controller.Move(worldDisplacement);
        }

        /// <summary>
        /// 위치 즉시 이동. <see cref="CharacterController"/> 는 켜진 상태에서 transform 을 옮기면 무시되므로
        /// 잠깐 껐다 켠다(사다리 부착/상단 이탈·리스폰 같은 순간이동에 필요).
        /// </summary>
        public void Teleport(Vector3 worldPosition)
        {
            bool wasEnabled = m_controller != null && m_controller.enabled;
            if (wasEnabled) m_controller.enabled = false;
            transform.position = worldPosition;
            if (wasEnabled) m_controller.enabled = true;
        }

        /// <summary>
        /// 입력 벡터를 현재 회전 전략(플레이어=카메라 기준)으로 변환한 <b>월드 이동 방향</b>. 입력 0이면 Vector3.zero.
        /// 회피 방향 결정에 쓴다(입력 있으면 그 방향, 없으면 호출부가 정면으로 폴백).
        /// </summary>
        public Vector3 ResolveWorldMoveDirection(Vector2 horizontalInput)
        {
            if (m_rotationStrategy != null)
                return m_rotationStrategy.MovementDirectionCalculation(horizontalInput, transform);
            return transform.TransformDirection(new Vector3(horizontalInput.x, 0f, horizontalInput.y)).normalized;
        }

        private void CharacterMovementCalculation(Vector2 horizontalInput, float targetSpeed)
        {
            if (horizontalInput == Vector2.zero)
                targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed =
                new Vector3(m_controller.velocity.x, 0.0f, m_controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = horizontalInput.magnitude;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // 램프 시작점을 '실제 controller.velocity'가 아니라 'm_speed(직전 의도 속도)'로 둔다.
                // 이유: velocity 기반이면 고fps에서 첫 프레임 변위가 CharacterController.minMoveDistance(0.001)보다
                //       작아 컨트롤러가 이동을 무시 → velocity가 0에 머물러 램프가 0에서 못 벗어나는 교착이 생긴다.
                //       m_speed 기반이면 실제 이동 여부와 무관하게 매 프레임 가속해 변위가 곧 임계를 넘어 정상 이동.
                m_speed = Mathf.Lerp(m_speed, targetSpeed * inputMagnitude,
                    Time.deltaTime * _settings.SpeedChangeRate);

                // round speed to 3 decimal places
                m_speed = Mathf.Round(m_speed * 1000f) / 1000f;
            }
            else
            {
                m_speed = targetSpeed;
            }
        }

        private void ApplyFacingDirection()
        {
            if (DesiredFacingDirection.sqrMagnitude < 0.0001f)
                return;

            float targetRotation = Mathf.Atan2(DesiredFacingDirection.x, DesiredFacingDirection.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetRotation,
                ref m_rotationVelocity,
                _settings.RotationSmoothTime);

            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }
    }
}

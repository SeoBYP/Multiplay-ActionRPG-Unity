using UnityEngine;
using VContainer;

namespace Game.Main.Character
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
            DesiredFacingDirection = m_rotationStrategy != null
                ? m_rotationStrategy.FacingDirectionCalculation(horizontalInput, transform)
                : transform.forward;
            ApplyFacingDirection();

            CurrentVelocity = targetDirection * horizontalInput.normalized.magnitude * (m_speed * Time.deltaTime) +
                              new Vector3(0.0f, input.y, 0.0f) * Time.deltaTime;
            //move the character controller
            m_controller.Move(CurrentVelocity);
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
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                m_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
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

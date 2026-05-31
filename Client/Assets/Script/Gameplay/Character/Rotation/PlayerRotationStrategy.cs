using UnityEngine;

namespace Game.Gameplay.Character.Rotation
{
    public class PlayerRotationStrategy : AgentRotationStrategy
    {
        [SerializeField]
        private GameObject m_mainCamera;
        private void Awake()
        {
            // get a reference to our main camera
            if (m_mainCamera == null)
            {
                m_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        protected override Vector3 MovementDirectionStrategy(Vector3 inputDirection, Transform agentTransform)
        {
            if (m_mainCamera == null)
                return agentTransform.TransformDirection(inputDirection).normalized;

            Vector3 cameraForward = m_mainCamera.transform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 cameraRight = m_mainCamera.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            return (cameraRight * inputDirection.x + cameraForward * inputDirection.z).normalized;
        }

        protected override Vector3 FacingDirectionStrategy(Vector3 inputDirection, Transform agentTransform)
        {
            if (m_mainCamera == null)
                return agentTransform.forward;

            Vector3 cameraForward = m_mainCamera.transform.forward;
            cameraForward.y = 0f;

            return cameraForward.sqrMagnitude < 0.0001f
                ? agentTransform.forward
                : cameraForward.normalized;
        }
    }
}

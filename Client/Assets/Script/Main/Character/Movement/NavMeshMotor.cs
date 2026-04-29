using UnityEngine;
using UnityEngine.AI;

namespace Game.Main.Character
{
    public class NavMeshMotor : MonoBehaviour, IAgentMotor
    {
        private NavMeshAgent m_navMeshAgent;

        public Vector3 CurrentVelocity => m_navMeshAgent.velocity;

        private void Awake()
        {
            m_navMeshAgent = GetComponent<NavMeshAgent>();
        }

        public void Move(Vector3 input, float speed)
        {
            m_navMeshAgent.speed = speed;

            Vector3 targetPosition =
                transform.position + new Vector3(input.x, 0, input.z);

            m_navMeshAgent.SetDestination(targetPosition);
        }
    }
}
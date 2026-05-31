using UnityEngine;

namespace Game.Gameplay.Character
{
    public abstract class AgentRotationStrategy : MonoBehaviour
    {
        public Vector3 MovementDirectionCalculation(Vector2 movementInput, Transform agentTransform)
        {
            Vector3 inputDirection = new Vector3(movementInput.x, 0.0f, movementInput.y).normalized;
            return movementInput == Vector2.zero
                ? Vector3.zero
                : MovementDirectionStrategy(inputDirection, agentTransform);
        }

        public Vector3 FacingDirectionCalculation(Vector2 movementInput, Transform agentTransform)
        {
            Vector3 inputDirection = new Vector3(movementInput.x, 0.0f, movementInput.y).normalized;
            Vector3 facingDirection = FacingDirectionStrategy(inputDirection, agentTransform);
            return facingDirection.sqrMagnitude < 0.0001f
                ? agentTransform.forward
                : facingDirection.normalized;
        }

        protected abstract Vector3 MovementDirectionStrategy(Vector3 inputDirection, Transform agentTransform);
        protected abstract Vector3 FacingDirectionStrategy(Vector3 inputDirection, Transform agentTransform);
    }
}

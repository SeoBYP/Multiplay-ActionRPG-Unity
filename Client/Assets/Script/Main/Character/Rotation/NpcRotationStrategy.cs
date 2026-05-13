using Game.Main.Character;
using UnityEngine;

namespace Script.Main.Character
{
    public class NpcRotationStrategy : AgentRotationStrategy
    {
        protected override Vector3 MovementDirectionStrategy(Vector3 inputDirection, Transform agentTransform)
            => inputDirection.normalized;

        protected override Vector3 FacingDirectionStrategy(Vector3 inputDirection, Transform agentTransform)
            => inputDirection.sqrMagnitude > 0.0001f
                ? inputDirection.normalized
                : agentTransform.forward;
    }
}

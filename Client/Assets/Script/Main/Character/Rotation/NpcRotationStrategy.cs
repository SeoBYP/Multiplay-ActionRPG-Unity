using Game.Main.Character;
using UnityEngine;

namespace Script.Main.Character
{
    public class NpcRotationStrategy : AgentRotationStrategy
    {
        protected override float RotationStrategy(Vector3 inputDirection)
            => Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
    }
}
using UnityEngine;

namespace Game.Gameplay.Character
{
    public interface IAgentMotor
    {
        void Move(Vector3 input, float speed);
        Vector3 CurrentVelocity { get; }
    }
}
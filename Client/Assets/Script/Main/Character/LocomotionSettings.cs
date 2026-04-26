using System;

namespace Game.Main.Character
{
    [Serializable]
    public class LocomotionSettings
    {
        public float MoveSpeed = 2f;
        public float SprintSpeed = 5.335f;
        public float Gravity = -15f;
        public float JumpHeight = 1.2f;
        public float FallTimeout = 0.15f;
        public float LandDuration = 0.533f;
        public float RotationSmoothTime = 0.12f;
        public float JumpToFallDelay = 0.2f;
        public float SpeedChangeRate = 10f;
        public float InteractInvokeDelay = 0.3f;
        public float InteractReturnDelay = 0.3f;
    }
}

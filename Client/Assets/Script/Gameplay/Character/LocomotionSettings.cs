using System;

namespace Game.Gameplay.Character
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

        // 회피(Dodge) — 대시 연출·이동 감각(클라 전용). 무적창/쿨다운은 Shared DodgeConfig(서버 권위).
        public float DodgeSpeed = 8f;
        public float DodgeDuration = 0.5f; // 대시(=입력 잠금) 지속(초). DodgeConfig.IframeMs(0.5s)와 맞춤.
    }
}

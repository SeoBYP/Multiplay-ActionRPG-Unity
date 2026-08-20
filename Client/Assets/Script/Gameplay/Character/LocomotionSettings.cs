using System;

namespace Game.Gameplay.Character
{
    [Serializable]
    public class LocomotionSettings
    {
        public float MoveSpeed = 2.3f;   // ARPGWarrior Walk 클립 실측 속도(2.26~2.32 m/s) — 발 슬라이딩 방지
        public float SprintSpeed = 5.335f;
        public float Gravity = -15f;
        public float JumpHeight = 1.2f;
        public float FallTimeout = 0.15f;
        public float LandDuration = 0.483f; // ARPGWarrior Landing 클립 길이(0.4833s) 정합 — SO(Data/CharacterStateConfig) 미지정 시 폴백
        public float RotationSmoothTime = 0.12f;
        public float JumpToFallDelay = 0.2f;
        public float SpeedChangeRate = 10f;

        // 이동 가감속 (m/s²). 즉시 최고속이 아니라 짧은 램프로 출발/정지를 부드럽게.
        // 가속 5 = 0→2m/s 0.4s, 감속 8 = 2→0m/s 0.25s.
        public float MoveAcceleration = 5f;
        public float MoveDeceleration = 8f;
        public float InteractInvokeDelay = 0.3f;
        public float InteractReturnDelay = 0.3f;

        // 회피(Dodge) — 대시 연출·이동 감각(클라 전용). 무적창/쿨다운은 Shared DodgeConfig(서버 권위).
        public float ClimbSpeed = 1.8f; // P6 사다리 오르내리기 속도(m/s). 클립 체감에 맞춘 값.
        public float DodgeSpeed = 8f;
        public float DodgeDuration = 0.5f; // 대시(=입력 잠금) 지속(초). DodgeConfig.IframeMs(0.5s)와 맞춤.
    }
}

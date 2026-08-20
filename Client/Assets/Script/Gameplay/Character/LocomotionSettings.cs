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

        /// <summary>8방향 블렌드(MoveX/MoveY) 감쇠 시간(초). 방향을 꺾을 때 클립이 툭 바뀌지 않게 한다.
        /// 너무 크면 발과 지면이 어긋나는 구간이 길어진다(0.1~0.15 권장).</summary>
        public float MoveBlendDamp = 0.12f;

        // 이동 가감속 (m/s²). 즉시 최고속이 아니라 짧은 램프로 출발/정지를 부드럽게.
        // 가속 5 = 0→2m/s 0.4s, 감속 8 = 2→0m/s 0.25s.
        public float MoveAcceleration = 5f;
        public float MoveDeceleration = 8f;
        public float InteractInvokeDelay = 0.3f;
        public float InteractReturnDelay = 0.3f;

        // 회피(Dodge) — 대시 연출·이동 감각(클라 전용). 무적창/쿨다운은 Shared DodgeConfig(서버 권위).
        public float ClimbSpeed = 1.2f; // 사다리 오르내리기 속도(m/s). 배속 보정이 있으니 원하는 값으로 바꿔도 손발은 안 미끄러진다.

        /// <summary>Climb_Up 클립이 상정한 상승 속도(m/s, 실측 1.00). 클립 배속 = ClimbSpeed / 이 값 → 손발이 발판을 정확히 따라간다.</summary>
        public float ClimbClipSpeed = 1.0f;

        /// <summary>사다리에서 점프(Space)로 이탈할 때 반대쪽으로 밀려나는 거리(m).</summary>
        public float ClimbJumpOffDistance = 0.7f;

        /// <summary>바닥에서 이 높이 안이면 아래 입력만으로 사다리에서 내려선다(m). 발판 간격(0.6)과 맞췄다.</summary>
        public float ClimbBottomReleaseHeight = 0.6f;
        public float DodgeSpeed = 8f;
        public float DodgeDuration = 0.5f; // 대시(=입력 잠금) 지속(초). DodgeConfig.IframeMs(0.5s)와 맞춤.
    }
}

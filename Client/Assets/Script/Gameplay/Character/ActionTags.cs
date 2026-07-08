namespace Game.Gameplay.Character
{
    /// <summary>
    /// 클라 전용 Action 상태 태그(서버 미사용). 공유 <c>Script.System.GamePlayAbilitySystem.GameplayTags</c>(Shared.Gameplay,
    /// Dead/Stun/Slow 등 서버·클라 공용)와 달리, 이 태그는 클라 로컬 이동/입력 연출에만 쓰인다.
    /// </summary>
    public static class ActionTags
    {
        /// <summary>
        /// Action(공격·상호작용 등) 발동 중 이동(수평 변위) 잠금. 발동 시 지속시간만큼 부여하고 자동 해제한다.
        /// <see cref="GroundState"/> 가 폴링해 수평 이동·이동 애니(Speed)를 0으로(중력·회전·락온 facing 은 유지).
        /// 서버는 이 태그를 모른다 — 이동은 클라 권위(C_Move)라, 잠긴 동안 C_Move 를 안 보내면 원격도 정지로 본다.
        /// </summary>
        public const string Rooted = "State.Rooted";
    }
}

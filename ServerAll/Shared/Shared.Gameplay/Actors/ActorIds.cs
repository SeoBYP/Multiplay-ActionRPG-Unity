namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// ActorId 규약의 <b>단일 정의</b>. 플레이어·몬스터를 하나의 long 식별자로 지칭하는 부호 규약.
    ///
    ///   > 0  : 플레이어 (= UserId 그대로. DB identity 라 항상 양수)
    ///   &lt; 0 : 몬스터   (= -InstanceId. 방 내 유일, InstanceId 는 1 이상)
    ///   = 0  : 환경/시스템 (S_ApplyEffect SourceId=0)
    ///
    /// 클라·서버가 각자 -x 를 손계산하지 않고 이 헬퍼만 쓴다 — 부호 실수 방지.
    /// </summary>
    public static class ActorIds
    {
        /// <summary>환경/시스템 출처(예: 낙사·장판). 시전자가 특정 액터가 아닐 때.</summary>
        public const long Environment = 0;

        /// <summary>플레이어 UserId → ActorId. 항상 양수이므로 값은 그대로지만 의도를 명시한다.</summary>
        public static long FromPlayer(long userId) => userId;

        /// <summary>몬스터 InstanceId(방 내 유일, ≥1) → 음수 ActorId. InstanceId 0 은 규약 위반(환경과 충돌).</summary>
        public static long FromMonster(int instanceId) => -(long)instanceId;

        public static bool IsPlayer(long actorId) => actorId > 0;
        public static bool IsMonster(long actorId) => actorId < 0;
        public static bool IsEnvironment(long actorId) => actorId == 0;

        /// <summary>몬스터 ActorId → InstanceId(양수)로 복원. 호출 전 <see cref="IsMonster"/> 로 판별한다.</summary>
        public static int ToMonsterInstanceId(long actorId) => (int)(-actorId);
    }
}

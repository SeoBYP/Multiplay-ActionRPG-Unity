namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// well-known GameplayTag 문자열 상수. 매직 스트링·오타 방지(클라·서버 공유).
    /// 새 상태 태그는 여기에 모은다(예: 스턴·무적).
    /// </summary>
    public static class GameplayTags
    {
        /// <summary>사망(다운). 입력 게이트가 폴링해 이동/공격/상호작용을 억제한다.</summary>
        public const string Dead = "State.Dead";
    }
}

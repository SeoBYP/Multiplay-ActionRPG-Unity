namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 마나 <b>리젠 권위 수치</b>(클라·서버 공유 단일 소스). 차감/거부는 서버가 진실원(S_PlayerMana 정정)이지만,
    /// 시간 비례 자연 회복은 클라(예측)·서버(권위)가 <b>같은 rate</b>로 계산해 수렴하므로 매 틱 동기화 패킷을 보내지 않는다.
    /// </summary>
    public static class ManaConfig
    {
        /// <summary>초당 마나 자연 회복량. 클라·서버 동일 적용(예측 수렴의 전제).</summary>
        public const float RegenPerSecond = 10f;
    }
}

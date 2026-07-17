using System;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 체력바가 읽는 몬스터 HP 계약. <b>표시 전용</b> — 판정에 관여하지 않는다.
    ///
    /// <para><b>왜 인터페이스인가</b>(unity-client.md 도입 기준): 구현체가 <b>실제로 둘</b>이다.
    /// <list type="bullet">
    /// <item><see cref="MonsterEntity"/> — 던전. HP 진실원 = 서버 권위(S_MonsterState).</item>
    /// <item><see cref="LocalMonster"/> — Main(B-lite 솔로). HP 진실원 = 클라 로컬.</item>
    /// </list>
    /// 둘의 권위 모델이 달라 하나로 합칠 수 없다. 체력바는 "누가 권위인가"를 알 필요가 없으므로
    /// 이 얇은 계약만 보게 한다 — 그러면 <see cref="MonsterHealthBar"/> 하나를 양쪽 프리팹에 그대로 쓴다.</para>
    /// </summary>
    public interface IMonsterHealth
    {
        int Hp { get; }
        int MaxHp { get; }

        /// <summary>HP 가 바뀌었다(사망 시 0 확정 포함). 체력바가 이걸 구독해 다시 그린다.</summary>
        event Action<IMonsterHealth> HpChanged;
    }
}

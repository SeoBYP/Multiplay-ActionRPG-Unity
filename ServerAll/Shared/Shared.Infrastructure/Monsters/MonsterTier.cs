namespace Shared.Infrastructure.Monsters;

/// <summary>
/// 몬스터 등급 <b>분류</b>. 진실원 = <c>monsters.json</c> 의 <c>tier</c> 문자열.
///
/// <para><b>배율이 아니다.</b> 변종은 각자 <b>ID 와 스탯을 직접 저작</b>한다 —
/// <c>leviathan</c> 과 <c>leviathan_boss</c> 는 별개 행이다.</para>
///
/// <para>용도: 표시·연출 분기(보스 체력바·등장 연출)와 저작 시 의도 표기.</para>
/// </summary>
public enum MonsterTier
{
    Normal = 0,
    Elite = 1,
    Boss = 2,
}

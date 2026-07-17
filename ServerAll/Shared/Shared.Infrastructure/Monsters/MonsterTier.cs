namespace Shared.Infrastructure.Monsters;

/// <summary>
/// 몬스터 등급 <b>분류</b>(AC-G). 진실원 = <c>monsters.json</c> 의 <c>tier</c> 문자열.
///
/// <para><b>배율이 아니다.</b> 변종은 각자 <b>ID 와 스탯을 직접 저작</b>한다 —
/// <c>leviathan</c>(maxHp 65) 과 <c>leviathan_boss</c>(maxHp 390) 는 별개 행이다.
/// 이전엔 <c>spawn.tier</c> + 배율 테이블(<c>monster-scaling.json</c>)로 곱했는데,
/// ① enum 을 서버·클라에 미러링해야 했고 ② 스폰에 필드가 둘(level+tier) 붙었고
/// ③ "이 몬스터가 왜 센지"를 두 곳에서 찾아야 했다. → <b>ID 하나만 처리</b>하도록 접었다.</para>
///
/// <para>남은 용도: 표시·연출 분기(보스 체력바·등장 연출)와 저작 시 의도 표기.</para>
/// </summary>
public enum MonsterTier
{
    Normal = 0,
    Elite = 1,
    Boss = 2,
}

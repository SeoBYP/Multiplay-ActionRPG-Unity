namespace Server.Monster;

/// <summary>몬스터 행동 페이즈. S_MonsterState.Phase(byte)와 1:1.</summary>
public enum MonsterPhase : byte
{
    Idle = 0,   // 타깃 없음·패트롤 없음 → 제자리
    Patrol = 1, // 패트롤 경로 순회
    Chase = 2,  // 최근접 타깃 추격
    Attack = 3, // 사거리 진입 → 정지·공격
}

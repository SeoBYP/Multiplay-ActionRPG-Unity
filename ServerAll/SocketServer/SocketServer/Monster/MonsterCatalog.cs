namespace Server.Monster;

/// <summary>몬스터 타입별 스탯(서버 시뮬레이션 전용). 추후 데이터(JSON/DB)로 외부화 가능.</summary>
public sealed record MonsterStats(
    int MaxHp,
    float MoveSpeed,        // units/sec
    float AggroRange,       // 추격 시작 거리
    float AttackRange,      // 정지·공격 거리
    float AttackCooldownMs, // 공격 간격
    int AttackDamage);

/// <summary>monsterId → MonsterStats. 미등록 타입은 Default 로 폴백(데이터 누락에도 동작).</summary>
public static class MonsterCatalog
{
    public static readonly MonsterStats Default =
        new(MaxHp: 30, MoveSpeed: 2.0f, AggroRange: 6f, AttackRange: 1.2f, AttackCooldownMs: 1500f, AttackDamage: 5);

    private static readonly Dictionary<string, MonsterStats> Table = new()
    {
        ["slime"] = new MonsterStats(MaxHp: 30, MoveSpeed: 2.0f, AggroRange: 6f, AttackRange: 1.2f, AttackCooldownMs: 1500f, AttackDamage: 5),
    };

    public static MonsterStats Get(string? monsterId)
        => monsterId != null && Table.TryGetValue(monsterId, out var s) ? s : Default;
}

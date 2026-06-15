namespace GameServer.Domain.Entities.Equipment;

/// <summary>
/// 장비 한 점이 더하는 **가산 스탯 모디파이어**(서버 권위). PlayerStats 의 합산 가능한 항목과 1:1.
/// 합산은 GetStatsAsync 단일 권위에서 base(레벨 룩업) 위에 Σ 한다(authority-model §4c).
/// 기본값 0 = 해당 스탯에 영향 없음.
/// </summary>
public readonly record struct EquipmentStatModifier(
    int MaxHealth = 0,
    int MaxMana = 0,
    int AttackPower = 0,
    int Defense = 0,
    int Strength = 0,
    int Dexterity = 0,
    int Intelligence = 0)
{
    /// <summary>두 모디파이어를 항목별로 더한다(착용 세트 합산용).</summary>
    public EquipmentStatModifier Add(in EquipmentStatModifier other) => new(
        MaxHealth + other.MaxHealth,
        MaxMana + other.MaxMana,
        AttackPower + other.AttackPower,
        Defense + other.Defense,
        Strength + other.Strength,
        Dexterity + other.Dexterity,
        Intelligence + other.Intelligence);
}

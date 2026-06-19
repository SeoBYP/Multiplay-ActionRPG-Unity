namespace GameServer.Domain.Entities.Shop;

/// <summary>상점 진열 분류(클라 탭과 1:1: Weapon/Armor/Accessory/Potion). 전투 슬롯(EquipmentType)과 별개의 표시용 그룹.</summary>
public enum ShopCategory
{
    Weapon,
    Armor,
    Accessory,
    Potion,
}

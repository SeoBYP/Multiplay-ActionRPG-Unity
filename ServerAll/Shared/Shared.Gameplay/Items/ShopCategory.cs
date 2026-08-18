namespace Shared.Gameplay.Items
{
    /// <summary>
    /// 상점 진열 분류(클라 탭과 1:1). 전투 슬롯(EquipmentType)과 별개의 표시용 그룹.
    /// 클라·서버 공통 — Shared.Gameplay 단일 소스.
    ///
    /// **proto enum(gameserver.shop.v1.ShopCategory)과 정수값 1:1 대응** — EquipmentType 과 동일 규약이다.
    /// 경계에서 캐스팅 매핑만 하면 되고, 값이 어긋날 여지를 없앤다.
    /// 이전 서버 도메인 enum 은 Unspecified 없이 Weapon=0 이라 proto 와 1 offset 이었다(수동 switch 로 흡수).
    /// None/0 = 미지정 가드(proto default).
    /// </summary>
    public enum ShopCategory
    {
        Unspecified = 0,
        Weapon = 1,
        Armor = 2,
        Accessory = 3,
        Potion = 4,
    }
}

namespace Shared.Gameplay.Items
{
    /// <summary>
    /// 상점 진열 분류(클라 탭과 1:1). 전투 슬롯(EquipmentType)과 별개의 표시용 그룹.
    /// 클라·서버 공통 — Shared.Gameplay 단일 소스.
    ///
    /// **proto enum(gameserver.shop.v1.ShopCategory)과 정수값 1:1 대응** — 경계에서 캐스팅 매핑만 한다.
    /// 0 = 미지정 가드(proto default).
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

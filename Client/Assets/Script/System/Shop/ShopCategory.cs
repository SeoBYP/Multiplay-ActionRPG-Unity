namespace Game.System.Shop
{
    /// <summary>상점 진열 분류(서버 proto enum 미러, proto 은닉). 클라 탭과 1:1.</summary>
    public enum ShopCategory
    {
        Unspecified,
        Weapon,
        Armor,
        Accessory,
        Potion,
    }
}

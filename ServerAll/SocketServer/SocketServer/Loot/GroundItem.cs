namespace Server.Loot;

// 정의(이름·아이콘)는 들지 않는다 — itemId 만(정의는 GameServer ItemCatalog 소유).
public sealed class GroundItem
{
    public int GroundId { get; init; }
    public int ItemId { get; init; }   // numericId(ItemCatalog). 대역 3000~3999 = 재화
    public int Qty { get; init; }
    public float PosX { get; init; }
    public float PosY { get; init; }
    public float PosZ { get; init; }
}

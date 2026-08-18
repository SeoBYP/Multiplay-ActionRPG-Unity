namespace Server.Loot;

/// <summary>
/// 월드 바닥에 떨어진 아이템(SocketServer 권위). 몬스터 사망 시 drop roll 결과로 스폰되고,
/// 줍기(TryPickup) 가 경쟁 중재 후 1회만 제거한다. GroundId 는 방 단위 순차 발급.
/// 정의(이름·아이콘) 는 들지 않는다 — itemId 문자열만(정의는 GameServer ItemCatalog 소유).
/// </summary>
public sealed class GroundItem
{
    public int GroundId { get; init; }
    public int ItemId { get; init; }   // numericId(ItemCatalog). 대역 3000~3999 = 재화
    public int Qty { get; init; }
    public float PosX { get; init; }
    public float PosY { get; init; }
    public float PosZ { get; init; }
}

namespace GameServer.Domain;

/// <summary>
/// 통화 식별자. 골드는 인벤토리 아이템(InventoryItem)이 아니라 지갑(UserWallet) 잔액으로 적립된다(3.4).
/// 드랍/킬 보상은 itemId 로 <see cref="Gold"/> 를 실어 오고, GameServer 지급 지점(LootGrantConsumer·
/// MainSpawnClaimService)이 이 값을 만나면 인벤토리 대신 IWalletService 로 라우팅한다.
/// </summary>
public static class Currencies
{
    public const string Gold = "gold";

    /// <summary>itemId 가 통화인지(인벤토리 아이템이 아니라 지갑으로 가야 하는지).</summary>
    public static bool IsCurrency(string itemId) => itemId == Gold;
}

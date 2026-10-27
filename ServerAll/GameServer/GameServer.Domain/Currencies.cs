namespace GameServer.Domain;

/// <summary>
/// 통화 식별자. 골드는 인벤토리 아이템이 아니라 지갑(UserWallet) 잔액으로 적립된다.
/// 드랍/킬 보상은 itemId 로 <see cref="Gold"/> 를 실어 오고, 지급 지점(LootGrantConsumer·
/// MainSpawnClaimService)이 이 값을 만나면 인벤토리 대신 IWalletService 로 라우팅한다.
///
/// 통화 여부는 numericId 대역(3000~3999)으로 판별한다. 대역이 곧 분류(1000 소모품 / 2100 무기 /
/// 2200 방어구 / 2300 장신구 / 3000 재화)라서, 새 통화도 대역 안에 넣으면 라우팅이 그대로 성립한다.
/// </summary>
public static class Currencies
{
    /// <summary>골드의 numericId. items.json 저작값과 일치해야 한다(ItemNumericIdTests 가 고정).</summary>
    public const int Gold = 3001;

    /// <summary>재화 대역 — 이 범위의 numericId 는 인벤토리가 아니라 지갑으로 간다.</summary>
    public const int CurrencyBandLow = 3000;

    public const int CurrencyBandHigh = 3999;

    /// <summary>itemId 가 통화인지(인벤토리 아이템이 아니라 지갑으로 가야 하는지).</summary>
    public static bool IsCurrency(int itemId) => itemId is >= CurrencyBandLow and <= CurrencyBandHigh;
}

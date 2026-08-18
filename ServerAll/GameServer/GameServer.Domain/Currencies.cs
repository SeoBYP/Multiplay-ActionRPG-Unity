namespace GameServer.Domain;

/// <summary>
/// 통화 식별자. 골드는 인벤토리 아이템(InventoryItem)이 아니라 지갑(UserWallet) 잔액으로 적립된다(3.4).
/// 드랍/킬 보상은 itemId 로 <see cref="Gold"/> 를 실어 오고, GameServer 지급 지점(LootGrantConsumer·
/// MainSpawnClaimService)이 이 값을 만나면 인벤토리 대신 IWalletService 로 라우팅한다.
///
/// <para><b>판별이 대역인 이유</b>: itemId 가 문자열이던 시절엔 <c>itemId == "gold"</c> 로 비교했고,
/// 카탈로그에 없는 의사 아이템("gold")을 드랍 테이블에 자유롭게 끼워 넣을 수 있었다. int 로 옮기면서
/// gold 를 <b>3001 로 카탈로그에 정식 등록</b>하고, 통화 여부는 <b>3000~3999 대역</b>으로 판별한다.
/// 대역이 곧 분류라는 규칙(1000 소모품 / 2100 무기 / 2200 방어구 / 2300 장신구 / 3000 재화)이
/// 여기서 실제 동작을 결정한다 — 새 통화가 생겨도 대역 안에 넣으면 라우팅이 그대로 성립한다.</para>
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

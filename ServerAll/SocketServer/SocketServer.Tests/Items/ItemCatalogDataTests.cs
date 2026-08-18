using System.IO;
using System.Linq;
using System.Text;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Shared.Infrastructure.Items;

namespace Server.Tests.Items;

/// <summary>
/// items.json(임베디드) 파싱 + 소모품 회복 수치 **단일소스** 검증.
///
/// 진실원 = 클라 아이템 SO → Tools/Items/Export bake → 이 임베디드 JSON.
/// 서버는 `GameplayEffectCatalog` 코드 시드가 아니라 **이 JSON 에서만** 소모품 회복을 읽는다.
/// → 코드 시드 제거 후에도 `CombatEffectCatalog.Resolve("potion_hp_small")` 가 동작해야 단일소스 배선이 옳다.
/// </summary>
public class ItemCatalogDataTests
{
    [Fact]
    public void 임베디드_potion_hp_small_이_Health_100_즉발로_로드된다()
    {
        var potion = ItemCatalogData.Current.Consumables.Single(d => d.Id == "potion_hp_small");

        Assert.Equal(EDurationPolicy.Instant, potion.Policy);
        var mod = Assert.Single(potion.Modifiers);
        Assert.Equal(EGameplayAttribute.Health, mod.AttributeType);
        Assert.Equal(100, mod.Amount);
    }

    [Fact]
    public void CombatEffectCatalog가_코드시드_제거후_소모품_회복을_JSON에서_흡수한다()
    {
        // potion_hp_small 은 GameplayEffectCatalog 코드 시드에서 제거됐다.
        // CombatEffectCatalog static ctor 가 bake JSON 을 Register 로 흡수해야 이 조회가 성립한다(단일소스 배선).
        var mods = CombatEffectCatalog.Resolve("potion_hp_small");

        var mod = Assert.Single(mods);
        Assert.Equal(EGameplayAttribute.Health, mod.AttributeType);
        Assert.Equal(100, mod.Amount);
    }

    [Fact]
    public void 전투_효과는_여전히_코드시드에서_해석된다()
    {
        // 전투 효과(서버 게임밸런스 권위)는 코드 시드 유지 — 소모품 흡수가 전투 조회를 깨면 안 된다(회귀 가드).
        // ※ 데미지 effect(*_dmg)는 AC-B B5 에서 폐기(수치=ability.baseDamage) → CC 효과로 검증한다.
        var mods = CombatEffectCatalog.Resolve("slow_3s");

        Assert.NotNull(new GameplayEffectCatalog().Get("slow_3s"));
        Assert.Empty(mods); // CC = modifier 없는 순수 상태태그(GrantedTags)
    }

    [Fact]
    public void 합성_JSON의_아이템_장비_상점_소모품이_모두_파싱된다()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleJson));

        var t = ItemCatalogData.Parse(stream);

        Assert.Equal(2, t.Items.Count);
        var item = t.ItemsById["potion_mp_small"];
        Assert.True(item.Stackable);
        Assert.Equal(99, item.MaxStack);

        var equip = Assert.Single(t.Equipment);
        Assert.Equal("sword_test", equip.ItemId);
        Assert.Equal(Shared.Gameplay.Equipment.EquipmentType.Weapon, equip.Slot);
        Assert.Equal(7, equip.Stats.AttackPower);

        Assert.Equal(2, t.Shop.Count);
        Assert.Equal(Shared.Gameplay.Items.ShopCategory.Potion, t.ShopById["potion_mp_small"].Category);
        Assert.Equal(50, t.ShopById["potion_mp_small"].BuyPrice);
    }

    [Fact]
    public void 소모품의_policy와_durationMs가_bake에서_보존된다()
    {
        // 회귀 가드: 구 ConsumableEffectExporter 는 policy/durationMs 를 내보내지 않았고 서버가 Instant/0 을
        // 하드코딩해, 지속형 버프 소모품을 저작해도 서버에선 즉발이 됐다. 그 유실이 되살아나면 여기서 깨진다.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleJson));

        var def = ItemCatalogData.Parse(stream).Consumables.Single();

        Assert.Equal("potion_mp_small", def.Id);
        Assert.Equal(EDurationPolicy.Duration, def.Policy);
        Assert.Equal(5000, def.DurationMs);
        var mod = Assert.Single(def.Modifiers);
        Assert.Equal(EGameplayAttribute.Mana, mod.AttributeType);
        Assert.Equal(50, mod.Amount);
    }

    /// <summary>파싱 전용 합성 데이터(실제 items.json 과 무관 — 스키마만 동일).</summary>
    private const string SampleJson = """
    {
      "items": [
        {
          "itemId": "potion_mp_small",
          "stackable": true,
          "maxStack": 99,
          "isEquipment": false,
          "equipSlot": "None",
          "equipStats": { "maxHealth": 0, "maxMana": 0, "attackPower": 0, "defense": 0, "strength": 0, "dexterity": 0, "intelligence": 0 },
          "isShopItem": true,
          "buyPrice": 50,
          "sellPrice": 10,
          "shopCategory": "Potion",
          "consumeEffects": [ { "stat": "Mana", "amount": 50, "policy": "Duration", "durationMs": 5000 } ]
        },
        {
          "itemId": "sword_test",
          "stackable": false,
          "maxStack": 1,
          "isEquipment": true,
          "equipSlot": "Weapon",
          "equipStats": { "maxHealth": 0, "maxMana": 0, "attackPower": 7, "defense": 0, "strength": 0, "dexterity": 0, "intelligence": 0 },
          "isShopItem": true,
          "buyPrice": 200,
          "sellPrice": 50,
          "shopCategory": "Weapon",
          "consumeEffects": []
        }
      ]
    }
    """;
}

using System.IO;
using System.Linq;
using System.Text;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Shared.Infrastructure.Consumables;

namespace Server.Tests.Consumables;

/// <summary>
/// consumable-effects.json(임베디드) 파싱 + 회복 수치 **단일소스** 검증.
///
/// 회복 수치 진실원 = 클라 `ConsumableCatalog` SO → Tools/Consumables/Export bake → 이 임베디드 JSON.
/// 서버는 `GameplayEffectCatalog` 코드 시드(예전 potion_hp_small)가 아니라 **이 JSON 에서만** 회복을 읽는다.
/// → 코드 시드 제거 후에도 `CombatEffectCatalog.Resolve("potion_hp_small")` 가 동작해야 단일소스 배선이 옳다.
/// 파싱·확률 무관(소모품은 즉발 회복) — 여기선 데이터/파싱/흡수만.
/// </summary>
public class ConsumableEffectCatalogTests
{
    [Fact]
    public void 임베디드_potion_hp_small_이_Health_100_즉발로_로드된다()
    {
        var potion = ConsumableEffectCatalog.LoadDefinitions().Single(d => d.Id == "potion_hp_small");

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
    public void 합성_JSON을_파싱한다()
    {
        const string json = """
        {
          "consumables": [
            { "itemId": "potion_mp_small", "modifiers": [ { "stat": "Mana", "amount": 50 } ] }
          ]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var def = Assert.Single(ConsumableEffectCatalog.Parse(stream));
        Assert.Equal("potion_mp_small", def.Id);
        Assert.Equal(EDurationPolicy.Instant, def.Policy);
        var mod = Assert.Single(def.Modifiers);
        Assert.Equal(EGameplayAttribute.Mana, mod.AttributeType);
        Assert.Equal(50, mod.Amount);
    }
}

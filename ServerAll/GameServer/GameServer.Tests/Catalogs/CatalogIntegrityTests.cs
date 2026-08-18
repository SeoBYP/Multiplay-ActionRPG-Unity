using GameServer.Domain;
using Shared.Infrastructure.Items;
using Shared.Infrastructure.Loot;
using Shared.Infrastructure.Quests;

namespace GameServer.Tests.Catalogs;

/// <summary>
/// 정적 기획데이터 **교차 정합성** 가드.
///
/// 왜 필요한가: 예전에는 같은 itemId 목록을 `ItemCatalog`/`EquipmentCatalog`/`ShopCatalog`/`QuestCatalog`
/// 4곳(+클라 SO)에 따로 하드코딩했고, 서버 코드를 한 번 고칠 때마다 조용히 갈라졌다.
/// 실제로 `gold_pouch` 고아·`potion_mp_small` 효과 누락이 발생했다.
/// 지금은 items.json 하나가 아이템/장비/상점/소모품을 모두 소유해 그 부류의 드리프트는 구조적으로 불가능하다.
///
/// 여기서 막는 것은 **다른 파일과의** 참조 드리프트(quests.json·drop-tables.json → items.json)와
/// 저작 실수(가격 0인 상품, 슬롯 없는 장비 등)다. 사람이 "export 했나?"를 기억하는 구조에 의존하지 않는다.
/// </summary>
public class CatalogIntegrityTests
{
    [Fact]
    public void 퀘스트_보상_아이템은_모두_아이템_카탈로그에_있다()
    {
        var missing = QuestCatalog.All
            .Where(q => !string.IsNullOrEmpty(q.Reward.ItemId))
            .Select(q => (q.QuestId, q.Reward.ItemId!))
            .Where(x => !ItemCatalog.Contains(x.Item2))
            .ToList();

        Assert.True(missing.Count == 0,
            $"quests.json 보상이 items.json 에 없는 itemId 를 가리킨다: {string.Join(", ", missing)}");
    }

    [Fact]
    public void CollectItem_퀘스트의_목표_아이템은_아이템_카탈로그에_있다()
    {
        var missing = QuestCatalog.All
            .Where(q => q.ObjectiveType == QuestObjectiveType.CollectItem)
            .Where(q => !ItemCatalog.Contains(q.TargetId))
            .Select(q => q.QuestId)
            .ToList();

        Assert.True(missing.Count == 0,
            $"CollectItem 목표가 items.json 에 없는 itemId 를 가리킨다: {string.Join(", ", missing)}");
    }

    [Fact]
    public void 드랍_테이블의_아이템은_통화를_빼면_모두_아이템_카탈로그에_있다()
    {
        // "gold" 는 인벤토리 아이템이 아니라 지갑으로 가는 통화(Currencies.Gold) — 카탈로그에 없는 게 정상이다.
        var missing = DropTableCatalog.All
            .SelectMany(kv => kv.Value.Select(e => (Monster: kv.Key, e.ItemId)))
            .Where(x => !Currencies.IsCurrency(x.ItemId) && !ItemCatalog.Contains(x.ItemId))
            .ToList();

        Assert.True(missing.Count == 0,
            $"drop-tables.json 이 items.json 에 없는 itemId 를 드랍한다: {string.Join(", ", missing)}");
    }

    [Fact]
    public void 상점_상품은_가격이_양수다()
    {
        var bad = ShopCatalog.All.Where(s => s.BuyPrice <= 0 || s.SellPrice <= 0).Select(s => s.ItemId).ToList();

        Assert.True(bad.Count == 0, $"가격이 0 이하인 상품: {string.Join(", ", bad)}");
    }

    [Fact]
    public void 상점_상품은_분류가_지정돼_있다()
    {
        var bad = ShopCatalog.All
            .Where(s => s.Category == Shared.Gameplay.Items.ShopCategory.Unspecified)
            .Select(s => s.ItemId)
            .ToList();

        Assert.True(bad.Count == 0, $"shopCategory 가 Unspecified 인 상품(클라 탭에서 사라진다): {string.Join(", ", bad)}");
    }

    [Fact]
    public void 장비는_착용_슬롯이_지정돼_있다()
    {
        var bad = EquipmentCatalog.All
            .Where(e => e.Slot == Shared.Gameplay.Equipment.EquipmentType.None)
            .Select(e => e.ItemId)
            .ToList();

        Assert.True(bad.Count == 0, $"equipSlot 이 None 인 장비(장착 불가): {string.Join(", ", bad)}");
    }

    [Fact]
    public void 장비는_가산_스탯이_하나라도_있다()
    {
        var bad = EquipmentCatalog.All
            .Where(e => e.Stats == default(EquipmentStatModifier))
            .Select(e => e.ItemId)
            .ToList();

        Assert.True(bad.Count == 0, $"모든 스탯이 0 인 장비(껴도 효과 없음): {string.Join(", ", bad)}");
    }

    [Fact]
    public void 장비는_스택되지_않는다()
    {
        // 장비는 개별 착용 대상이라 스택형이면 슬롯 의미가 깨진다(qty=1 소유가 전제).
        var bad = EquipmentCatalog.All
            .Select(e => ItemCatalog.Get(e.ItemId)!)
            .Where(i => i.Stackable || i.MaxStack != 1)
            .Select(i => i.ItemId)
            .ToList();

        Assert.True(bad.Count == 0, $"스택형으로 저작된 장비: {string.Join(", ", bad)}");
    }
}

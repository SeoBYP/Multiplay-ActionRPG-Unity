using System.Reflection;
using System.Text.Json;
using Script.System.GamePlayAbilitySystem;
using Shared.Gameplay.Equipment;
using Shared.Gameplay.Items;

namespace Shared.Infrastructure.Items;

/// <summary>
/// items.json(임베디드 리소스)을 1회 로드해 아이템/장비/상점/소모품 정의를 제공하는 **단일 저작 소스**.
/// MonsterCatalog·DropTableCatalog·LevelTable 과 동일 교리: 클라 SO 저작 → bake → 서버 임베디드 로드.
///
/// 이전에는 `GameServer.Domain` 안에 `ItemCatalog`/`EquipmentCatalog`/`ShopCatalog` 3개가 **하드코딩 Dictionary**
/// 로 흩어져 있어, 같은 itemId 목록을 4곳(+클라 SO)에서 따로 저작했다. 서버 코드를 고칠 때마다 클라와 갈라졌고
/// 실제로 `gold_pouch` 고아·`potion_mp_small` 효과 누락이 발생했다. 이 파일이 그 4곳을 하나로 합친다.
///
/// **순서 보존**: 파일 순서 = 저작 순서 = 상점 진열 순서(ShopCatalog.All). 알파벳 정렬하지 않는다.
/// </summary>
public static class ItemCatalogData
{
    private const string ResourceName = "Shared.Infrastructure.Items.items.json";

    private static readonly Lazy<CatalogTables> Tables = new(LoadEmbedded);

    /// <summary>로드된 파생 테이블(1회 로드). 파사드·SocketServer 가 공유한다.</summary>
    public static CatalogTables Current => Tables.Value;

    private static CatalogTables LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        return Parse(stream);
    }

    /// <summary>JSON 스트림 → 파생 테이블. 단위 테스트가 합성 JSON 으로 호출할 수 있도록 공개한다.</summary>
    public static CatalogTables Parse(Stream stream)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<ItemFile>(stream, options)
            ?? throw new InvalidOperationException("Failed to parse items.json");

        var items = new List<ItemDef>();
        var equipment = new List<EquipmentDef>();
        var shop = new List<ShopItemDef>();
        var consumables = new List<GameplayEffectDefinition>();

        foreach (var dto in file.Items)
        {
            if (string.IsNullOrWhiteSpace(dto.ItemId))
                throw new InvalidOperationException("items.json contains an entry with an empty itemId");

            items.Add(new ItemDef(dto.ItemId, dto.NumericId, dto.Stackable, dto.MaxStack));

            if (dto.IsEquipment)
            {
                var s = dto.EquipStats;
                equipment.Add(new EquipmentDef(
                    dto.ItemId,
                    dto.NumericId,
                    Enum.Parse<EquipmentType>(dto.EquipSlot, ignoreCase: true),
                    new EquipmentStatModifier(
                        s.MaxHealth, s.MaxMana, s.AttackPower, s.Defense,
                        s.Strength, s.Dexterity, s.Intelligence)));
            }

            if (dto.IsShopItem)
            {
                shop.Add(new ShopItemDef(
                    dto.ItemId,
                    dto.NumericId,
                    dto.BuyPrice,
                    dto.SellPrice,
                    Enum.Parse<ShopCategory>(dto.ShopCategory, ignoreCase: true)));
            }

            if (dto.ConsumeEffects.Count > 0)
            {
                // effectId == itemId 규칙: 소비 통지(PlayerConsumedMessage.EffectId)가 곧 itemId 라 별도 매핑 불필요.
                // ⚠ id 는 **numericId 의 문자열**이다 — ItemId 가 int 로 바뀐 뒤 gRPC 가 보내는 값이
                //   request.ItemId.ToString() 이므로 등록 키도 같아야 조회가 맞는다. 저작 문자열
                //   ("potion_hp_small")로 등록하면 소비해도 효과가 안 붙는다(E2E 가 이 불일치를 잡았다).
                // policy/durationMs 를 그대로 싣는다 — 구 ConsumableEffectExporter 는 이 둘을 bake 에서 누락시켰고
                // 서버가 Instant/0 으로 하드코딩해, 지속형 버프 물약을 저작해도 서버에선 즉발이 되는 버그가 있었다.
                var first = dto.ConsumeEffects[0];
                var mods = dto.ConsumeEffects
                    .Select(e => GameplayAttributeModifier.Create(
                        Enum.Parse<EGameplayAttribute>(e.Stat, ignoreCase: true), e.Amount, EModifierType.Additive))
                    .ToList();

                consumables.Add(new GameplayEffectDefinition(
                    id: dto.NumericId.ToString(),
                    category: EEffectCategory.AttackPower, // 소모품은 버프 아이콘 미사용(cosmetic)
                    policy: Enum.Parse<EDurationPolicy>(first.Policy, ignoreCase: true),
                    durationMs: first.DurationMs,
                    modifiers: mods));
            }
        }

        return new CatalogTables(items, equipment, shop, consumables);
    }

    /// <summary>파싱 산출물. 조회는 Dictionary, 열거는 원본(저작) 순서를 유지하는 List 로 각각 제공한다.</summary>
    public sealed class CatalogTables
    {
        internal CatalogTables(
            IReadOnlyList<ItemDef> items,
            IReadOnlyList<EquipmentDef> equipment,
            IReadOnlyList<ShopItemDef> shop,
            IReadOnlyList<GameplayEffectDefinition> consumables)
        {
            Items = items;
            Equipment = equipment;
            Shop = shop;
            Consumables = consumables;
            ItemsById = items.ToDictionary(i => i.ItemId, StringComparer.Ordinal);
            // numericId 는 전환 중인 키다. 미배정(0)·중복이 있어도 **서버를 죽이지 않는다** —
            // 로드 실패는 데이터 한 줄 때문에 서버 전체를 내리는 결과가 되고, 이 값은 아직
            // 아무 런타임 경로도 쓰지 않는다(1단계). 불변식은 저작(ItemCatalogExporter 가 bake 거부)과
            // 테스트(ItemNumericIdTests)가 지킨다.
            var byNumeric = new Dictionary<int, ItemDef>();
            foreach (var i in items)
                if (i.NumericId > 0)
                    byNumeric.TryAdd(i.NumericId, i);
            ItemsByNumericId = byNumeric;
            EquipmentById = equipment.ToDictionary(e => e.ItemId, StringComparer.Ordinal);
            ShopById = shop.ToDictionary(s => s.ItemId, StringComparer.Ordinal);

            // numericId 키 — ItemsByNumericId 와 같은 방어 규칙(미배정 0·중복은 건너뛴다).
            var eqByNum = new Dictionary<int, EquipmentDef>();
            foreach (var e in equipment) if (e.NumericId > 0) eqByNum.TryAdd(e.NumericId, e);
            EquipmentByNumericId = eqByNum;
            var shopByNum = new Dictionary<int, ShopItemDef>();
            foreach (var s2 in shop) if (s2.NumericId > 0) shopByNum.TryAdd(s2.NumericId, s2);
            ShopByNumericId = shopByNum;
        }

        public IReadOnlyList<ItemDef> Items { get; }
        public IReadOnlyList<EquipmentDef> Equipment { get; }
        public IReadOnlyList<ShopItemDef> Shop { get; }
        public IReadOnlyList<GameplayEffectDefinition> Consumables { get; }

        public IReadOnlyDictionary<string, ItemDef> ItemsById { get; }

        /// <summary>숫자 ID 조회. ItemId 를 int 로 옮기는 전환(2단계)의 진입점.</summary>
        public IReadOnlyDictionary<int, ItemDef> ItemsByNumericId { get; }
        public IReadOnlyDictionary<int, EquipmentDef> EquipmentByNumericId { get; }
        public IReadOnlyDictionary<int, ShopItemDef> ShopByNumericId { get; }
        public IReadOnlyDictionary<string, EquipmentDef> EquipmentById { get; }
        public IReadOnlyDictionary<string, ShopItemDef> ShopById { get; }
    }

    // ── JSON DTO (클라 ItemCatalogExporter 의 JsonUtility 출력 형식과 1:1) ──
    private sealed class ItemFile
    {
        public List<ItemDto> Items { get; set; } = new();
    }

    private sealed class ItemDto
    {
        public string ItemId { get; set; } = "";
        public int NumericId { get; set; }
        public bool Stackable { get; set; }
        public int MaxStack { get; set; } = 1;

        public bool IsEquipment { get; set; }
        public string EquipSlot { get; set; } = "None";
        public StatsDto EquipStats { get; set; } = new();

        public bool IsShopItem { get; set; }
        public long BuyPrice { get; set; }
        public long SellPrice { get; set; }
        public string ShopCategory { get; set; } = "Unspecified";

        public List<ConsumeEffectDto> ConsumeEffects { get; set; } = new();
    }

    private sealed class StatsDto
    {
        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }
        public int AttackPower { get; set; }
        public int Defense { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
    }

    private sealed class ConsumeEffectDto
    {
        public string Stat { get; set; } = "Health";
        public int Amount { get; set; }
        public string Policy { get; set; } = "Instant";
        public int DurationMs { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Items;
using Script.System.GamePlayAbilitySystem;
using Shared.Gameplay.Equipment;
using Shared.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// ItemCatalogDefinition(SO) ↔ items.json 툴 (MonsterCatalogExporter 와 동일 컨벤션).
    ///
    /// - Export: SO 의 items → JSON bake → 서버 임베디드 경로(서버 재빌드 시 반영).
    /// - Import(부트스트랩): 기존 items.json → ItemCatalogDefinition 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(<c>Shared.Infrastructure.Items.ItemCatalogData.Parse</c>)와 동일:
    ///   { "items": [ { "itemId":.., "stackable":.., "equipStats":{..}, "consumeEffects":[..] } ] }
    ///
    /// <para><b>⚠ 정렬하지 않는다</b> — MonsterCatalogExporter 는 monsterId 로 OrderBy 하지만 아이템은
    /// <c>ItemCatalogData</c> 가 "파일 순서 = 저작 순서 = 상점 진열 순서"를 계약으로 명시한다.
    /// 정렬하면 상점 진열이 조용히 바뀐다.</para>
    /// </summary>
    public static class ItemCatalogExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Items/items.json"; // repo 루트 기준
        private const string AssetDir = "Assets/GameData/Item"; // 저작 전용 SO(런타임 미로드, JSON bake만). Resources 밖.
        private const string AssetName = "ItemCatalogDefinition";

        [MenuItem("Tools/Item/Export Item Catalog (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            EditorToolReport.Later("Export Item Catalog", count == 0
                ? "ItemCatalogDefinition 에셋이 없습니다. 먼저 'Import' 로 부트스트랩하거나 SO를 생성하세요."
                : $"아이템 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.");
        }

        /// <summary>ItemCatalogDefinition → items.json bake. 반환: 아이템 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:ItemCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemCatalogDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0)
            {
                Debug.LogWarning("[ItemCatalogExporter] ItemCatalogDefinition 에셋이 없습니다.");
                return 0;
            }
            if (defs.Count > 1)
            {
                Debug.LogError($"[ItemCatalogExporter] ItemCatalogDefinition 이 {defs.Count}개입니다 — 1개만 허용.");
                return -1;
            }

            var items = defs[0].items ?? new List<ItemDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenNumeric = new HashSet<int>();
            foreach (var i in items)
            {
                if (string.IsNullOrWhiteSpace(i.itemId))
                {
                    Debug.LogError($"[ItemCatalogExporter] itemId 가 비어있는 항목: {AssetDatabase.GetAssetPath(defs[0])}");
                    return -1;
                }
                // itemId 는 DB 복합 PK(UserId, ItemId)·proto·드랍테이블이 함께 참조하는 키다. 중복되면 조용히 덮어써진다.
                if (!seen.Add(i.itemId))
                {
                    Debug.LogError($"[ItemCatalogExporter] itemId 중복: '{i.itemId}'");
                    return -1;
                }
                if (!seenNumeric.Add(i.numericId))
                {
                    Debug.LogError($"[ItemCatalogExporter] numericId 중복: {i.numericId} ('{i.itemId}') — DB·패킷 키가 겹치면 안 된다.");
                    return -1;
                }
                var band = ExpectedBand(i.shopCategory);
                if (i.numericId < band.lo || i.numericId > band.hi)
                {
                    Debug.LogError($"[ItemCatalogExporter] '{i.itemId}' numericId {i.numericId} 가 {i.shopCategory} 대역({band.lo}~{band.hi}) 밖이다.");
                    return -1;
                }
                if (i.isEquipment && i.equipSlot == EquipmentType.None)
                {
                    Debug.LogError($"[ItemCatalogExporter] '{i.itemId}' 는 isEquipment 인데 equipSlot 이 None 이다.");
                    return -1;
                }
                if (i.consumeEffects != null && i.consumeEffects.Any(e => e.policy == EDurationPolicy.Duration && e.durationMs <= 0))
                {
                    Debug.LogError($"[ItemCatalogExporter] '{i.itemId}' 의 Duration 효과에 durationMs 가 없다.");
                    return -1;
                }
            }

            var file = new FileDto
            {
                // ⚠ OrderBy 금지 — 저작 순서 = 상점 진열 순서(ItemCatalogData 계약).
                items = items.Select(ToDto).ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[ItemCatalogExporter] Export 완료 — 아이템 {file.items.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.items.Count;
        }

        [MenuItem("Tools/Item/Import Item Catalog from JSON (bootstrap)")]
        public static void Import()
        {
            var count = ImportAll();
            if (count < 0) return; // 실패 사유는 콘솔
            EditorToolReport.Later("Import Item Catalog", $"아이템 {count}종을 SO 로 가져왔습니다.\n{AssetDir}/{AssetName}.asset");
        }

        /// <summary>items.json → ItemCatalogDefinition 부트스트랩. 반환: 아이템 수 / -1(실패).
        /// <b>다이얼로그 없음</b> — 자동화(Unity CLI)는 반드시 이쪽을 호출한다(BakeAll 과 대칭).</summary>
        public static int ImportAll()
        {
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(serverPath))
            {
                Debug.LogError($"[ItemCatalogExporter] items.json 이 없습니다: {serverPath}");
                return -1;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(serverPath));
            if (file?.items == null)
            {
                Debug.LogError("[ItemCatalogExporter] items.json 파싱에 실패했습니다.");
                return -1;
            }

            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<ItemCatalogDefinition>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ItemCatalogDefinition>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            so.items = file.items.Select(FromDto).ToList();
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ItemCatalogExporter] Import 완료 — 아이템 {so.items.Count}종 → {assetPath}");
            return so.items.Count;
        }

        /// <summary>
        /// 분류별 numericId 대역. <b>대역이 곧 분류</b>라 로그·DB 만 보고도 무엇인지 안다.
        /// 문자열 id 를 걷어낸 뒤(2단계) 사람이 읽는 단서는 이 대역뿐이므로 규칙을 코드로 강제한다.
        /// </summary>
        private static (int lo, int hi) ExpectedBand(ShopCategory c) => c switch
        {
            ShopCategory.Potion => (1000, 1999),
            ShopCategory.Weapon => (2100, 2199),
            ShopCategory.Armor => (2200, 2299),
            ShopCategory.Accessory => (2300, 2399),
            _ => (3000, 3999), // Unspecified = 재화·기타
        };

        private static ItemDto ToDto(ItemDefinition i) => new()
        {
            itemId = i.itemId,
            numericId = i.numericId,
            stackable = i.stackable,
            maxStack = i.stackable ? i.maxStack : 1,
            isEquipment = i.isEquipment,
            equipSlot = i.equipSlot.ToString(),
            equipStats = new StatsDto
            {
                maxHealth = i.equipStats?.maxHealth ?? 0,
                maxMana = i.equipStats?.maxMana ?? 0,
                attackPower = i.equipStats?.attackPower ?? 0,
                defense = i.equipStats?.defense ?? 0,
                strength = i.equipStats?.strength ?? 0,
                dexterity = i.equipStats?.dexterity ?? 0,
                intelligence = i.equipStats?.intelligence ?? 0,
            },
            isShopItem = i.isShopItem,
            buyPrice = i.buyPrice,
            sellPrice = i.sellPrice,
            shopCategory = i.shopCategory.ToString(),
            consumeEffects = (i.consumeEffects ?? new List<ItemConsumeEffect>())
                .Select(e => new ConsumeEffectDto
                {
                    stat = e.stat.ToString(),
                    amount = e.amount,
                    policy = e.policy.ToString(),
                    durationMs = e.durationMs,
                })
                .ToList(),
        };

        private static ItemDefinition FromDto(ItemDto d) => new()
        {
            itemId = d.itemId,
            numericId = d.numericId,
            stackable = d.stackable,
            maxStack = d.maxStack,
            isEquipment = d.isEquipment,
            equipSlot = ParseEnum(d.equipSlot, EquipmentType.None),
            equipStats = new ItemEquipStats
            {
                maxHealth = d.equipStats?.maxHealth ?? 0,
                maxMana = d.equipStats?.maxMana ?? 0,
                attackPower = d.equipStats?.attackPower ?? 0,
                defense = d.equipStats?.defense ?? 0,
                strength = d.equipStats?.strength ?? 0,
                dexterity = d.equipStats?.dexterity ?? 0,
                intelligence = d.equipStats?.intelligence ?? 0,
            },
            isShopItem = d.isShopItem,
            buyPrice = d.buyPrice,
            sellPrice = d.sellPrice,
            shopCategory = ParseEnum(d.shopCategory, ShopCategory.Unspecified),
            consumeEffects = (d.consumeEffects ?? new List<ConsumeEffectDto>())
                .Select(e => new ItemConsumeEffect
                {
                    stat = ParseEnum(e.stat, EGameplayAttribute.Health),
                    amount = e.amount,
                    policy = ParseEnum(e.policy, EDurationPolicy.Instant),
                    durationMs = e.durationMs,
                })
                .ToList(),
        };

        private static T ParseEnum<T>(string s, T fallback) where T : struct
            => Enum.TryParse<T>(s, ignoreCase: true, out var v) ? v : fallback;

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        private static string RepoRoot() => Directory.GetParent(Application.dataPath)!.Parent!.FullName;

        // ── JSON DTO (서버 ItemCatalogData 의 ItemDto 와 1:1) ──
        [Serializable] private sealed class FileDto { public List<ItemDto> items = new(); }

        [Serializable]
        private sealed class ItemDto
        {
            public string itemId;
            public int numericId;
            public bool stackable;
            public int maxStack = 1;
            public bool isEquipment;
            public string equipSlot = "None";
            public StatsDto equipStats = new();
            public bool isShopItem;
            public long buyPrice;
            public long sellPrice;
            public string shopCategory = "Unspecified";
            public List<ConsumeEffectDto> consumeEffects = new();
        }

        [Serializable]
        private sealed class StatsDto
        {
            public int maxHealth;
            public int maxMana;
            public int attackPower;
            public int defense;
            public int strength;
            public int dexterity;
            public int intelligence;
        }

        [Serializable]
        private sealed class ConsumeEffectDto
        {
            public string stat = "Health";
            public int amount;
            public string policy = "Instant";
            public int durationMs;
        }
    }
}

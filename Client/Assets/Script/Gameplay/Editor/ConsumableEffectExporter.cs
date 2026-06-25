using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Inventory;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// ConsumableCatalog(SO) → consumable-effects.json bake (DropTableExporter 와 동일 컨벤션, 교리 gas-architecture §2.5).
    ///
    /// 데이터 진실원 = 클라 `ConsumableCatalog` SO(기획자 Inspector 편집). 서버는 UnityEngine(SO)을 못 읽으므로
    /// 이 툴이 SO 를 서버 임베디드 JSON 으로 bake → 서버 `Shared.Infrastructure.Consumables.ConsumableEffectCatalog` 가 읽는다.
    /// (클라는 SO 를 직접 읽으므로 클라 JSON 미러 불필요. effectId == itemId.)
    /// JSON 형식 = ConsumableEffectCatalog.Parse 와 동일: { "consumables":[ { "itemId":.., "modifiers":[ { "stat":.., "amount":.. } ] } ] }
    /// </summary>
    public static class ConsumableEffectExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Consumables/consumable-effects.json";
        private const string AssetDir = "Assets/GameData/Consumable"; // 런타임은 Addressables(address=asset path)로 로드. Resources 밖.
        private const string AssetName = "ConsumableCatalog";

        [MenuItem("Tools/Consumables/Export (SO -> JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Consumables",
                    "ConsumableCatalog 에셋이 없습니다. 먼저 'Import' 로 부트스트랩하거나 SO를 생성하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Consumables",
                $"소모품 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>모든 ConsumableCatalog → consumable-effects.json bake. 반환: 소모품 종 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var catalogs = LoadAllCatalogs();
            if (catalogs.Count == 0)
            {
                Debug.LogWarning("[ConsumableEffectExporter] ConsumableCatalog 에셋이 없습니다.");
                return 0;
            }

            var merged = new List<ConsumableDef>();
            var seen = new HashSet<string>();
            foreach (var cat in catalogs)
            foreach (var item in cat.items)
            {
                if (string.IsNullOrWhiteSpace(item.itemId))
                {
                    Debug.LogError($"[ConsumableEffectExporter] itemId 가 비어있는 항목: {AssetDatabase.GetAssetPath(cat)}");
                    return -1;
                }
                if (!seen.Add(item.itemId))
                {
                    Debug.LogError($"[ConsumableEffectExporter] itemId 중복: '{item.itemId}'");
                    return -1;
                }
                merged.Add(item);
            }

            var file = new FileDto
            {
                consumables = merged
                    .OrderBy(i => i.itemId, StringComparer.Ordinal)
                    .Select(i => new ConsumableDto
                    {
                        itemId = i.itemId,
                        modifiers = (i.effects ?? new List<ConsumableEffectDef>())
                            .Select(e => new ModDto { stat = e.stat.ToString(), amount = e.amount })
                            .ToList()
                    })
                    .ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[ConsumableEffectExporter] Export 완료 — 소모품 {file.consumables.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.consumables.Count;
        }

        private static List<ConsumableCatalog> LoadAllCatalogs()
        {
            return AssetDatabase.FindAssets("t:ConsumableCatalog")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ConsumableCatalog>)
                .Where(c => c != null)
                .ToList();
        }

        private static void WriteFile(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
        }

        /// <summary>Application.dataPath = repo/Client/Assets → repo 루트.</summary>
        private static string RepoRoot() => Directory.GetParent(Application.dataPath)!.Parent!.FullName;

        // ── JSON DTO (서버 ConsumableEffectCatalog.Parse 와 동일 형식) ──
        [Serializable] private sealed class FileDto { public List<ConsumableDto> consumables = new(); }
        [Serializable] private sealed class ConsumableDto { public string itemId; public List<ModDto> modifiers = new(); }
        [Serializable] private sealed class ModDto { public string stat = "Health"; public int amount; }
    }
}

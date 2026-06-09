using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Loot;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// DropTableDefinition(SO) ↔ drop-tables.json 양방향 툴 (MapDataExporter 와 동일 컨벤션).
    ///
    /// - Export: 모든 DropTableDefinition 의 tables 를 모아 JSON 으로 bake → 서버 임베디드 경로 기록.
    ///   (클라 Main 은 SO를 직접 읽으므로 클라 JSON 미러는 불필요 — SO가 클라의 단일 소스.)
    ///   ※ 서버는 임베디드 리소스라 export 후 서버 재빌드 시 반영된다.
    /// - Import(부트스트랩): 기존 drop-tables.json → DropTableDefinition 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(Shared.Infrastructure.DropTableCatalog.Parse)가 읽는 것과 동일:
    ///   { "tables": [ { "monsterId": "...", "drops": [ { "itemId":.., "chance":.., "minQty":.., "maxQty":.. } ] } ] }
    /// </summary>
    public static class DropTableExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Loot/drop-tables.json"; // repo 루트 기준
        private const string AssetDir = "Assets/GameData/Resources/Loot"; // 런타임 Resources.Load<DropTableDefinition>("Loot/DropTableDefinition")
        private const string AssetName = "DropTableDefinition";

        [MenuItem("Tools/Loot/Export Drop Tables (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Drop Tables",
                    "DropTableDefinition 에셋이 없습니다. 먼저 'Import Drop Tables from JSON' 으로 부트스트랩하거나 SO를 생성하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Drop Tables",
                $"몬스터 {count}종 드랍을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>모든 DropTableDefinition → drop-tables.json bake. 반환: 몬스터 종 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = LoadAllDefinitions();
            if (defs.Count == 0)
            {
                Debug.LogWarning("[DropTableExporter] DropTableDefinition 에셋이 없습니다.");
                return 0;
            }

            // 여러 SO의 tables 를 모으되 monsterId 중복/공란 검증.
            var merged = new List<MonsterDropTable>();
            var seen = new HashSet<string>();
            foreach (var def in defs)
            foreach (var t in def.tables)
            {
                if (string.IsNullOrWhiteSpace(t.monsterId))
                {
                    Debug.LogError($"[DropTableExporter] monsterId 가 비어있는 항목: {AssetDatabase.GetAssetPath(def)}");
                    return -1;
                }
                if (!seen.Add(t.monsterId))
                {
                    Debug.LogError($"[DropTableExporter] monsterId 중복: '{t.monsterId}'");
                    return -1;
                }
                merged.Add(t);
            }

            var file = new FileDto
            {
                tables = merged
                    .OrderBy(t => t.monsterId, StringComparer.Ordinal)
                    .Select(t => new TableDto
                    {
                        monsterId = t.monsterId,
                        drops = (t.drops ?? new List<DropEntryDef>())
                            .Select(d => new DropDto
                            {
                                itemId = d.itemId, chance = d.chance, minQty = d.minQty, maxQty = d.maxQty
                            })
                            .ToList()
                    })
                    .ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[DropTableExporter] Export 완료 — 몬스터 {file.tables.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.tables.Count;
        }

        [MenuItem("Tools/Loot/Import Drop Tables from JSON (bootstrap)")]
        public static void Import()
        {
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(serverPath))
            {
                EditorUtility.DisplayDialog("Import Drop Tables", "drop-tables.json 을 찾지 못했습니다.", "확인");
                return;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(serverPath));
            if (file?.tables == null || file.tables.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Drop Tables", "JSON 파싱 실패 또는 테이블이 없습니다.", "확인");
                return;
            }

            EnsureAssetFolder(AssetDir);

            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<DropTableDefinition>(assetPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<DropTableDefinition>();

            def.tables = file.tables
                .Select(t => new MonsterDropTable
                {
                    monsterId = t.monsterId,
                    drops = (t.drops ?? new List<DropDto>())
                        .Select(d => new DropEntryDef
                        {
                            itemId = d.itemId, chance = d.chance, minQty = d.minQty, maxQty = d.maxQty
                        })
                        .ToList()
                })
                .ToList();

            if (isNew) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DropTableExporter] Import 완료 — 몬스터 {def.tables.Count}종 ({assetPath})");
            EditorUtility.DisplayDialog("Import Drop Tables",
                $"부트스트랩 완료 — 몬스터 {def.tables.Count}종\n위치: {assetPath}", "확인");
        }

        private static List<DropTableDefinition> LoadAllDefinitions()
        {
            return AssetDatabase.FindAssets("t:DropTableDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DropTableDefinition>)
                .Where(d => d != null)
                .ToList();
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteFile(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
        }

        /// <summary>Application.dataPath = repo/Client/Assets → repo 루트.</summary>
        private static string RepoRoot() => Directory.GetParent(Application.dataPath)!.Parent!.FullName;

        // ── JSON DTO (서버 DropTableCatalog.Parse 와 동일 형식) ──
        [Serializable] private sealed class FileDto { public List<TableDto> tables = new(); }
        [Serializable] private sealed class TableDto { public string monsterId; public List<DropDto> drops = new(); }
        [Serializable] private sealed class DropDto { public string itemId; public float chance = 1f; public int minQty = 1; public int maxQty = 1; }
    }
}

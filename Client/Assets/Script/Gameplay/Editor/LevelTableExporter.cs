using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// LevelTableDefinition(SO) ↔ level-table.json 툴 (DropTableExporter 와 동일 컨벤션).
    ///
    /// - Generate Default Curve: 비어있는/덮어쓸 SO 를 거듭제곱 Exp(round(100·L^1.5)) + 선형 스탯으로 1~60 시드.
    /// - Export: SO 의 rows → JSON 으로 bake → 서버 임베디드 경로 기록(서버 재빌드 시 반영).
    /// - Import(부트스트랩): 기존 level-table.json → LevelTableDefinition 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(Shared.Infrastructure.Progression.LevelTable.Parse)가 읽는 것과 동일:
    ///   { "levels": [ { "level":.., "expToNext":.., "maxHealth":.., ... } ] }
    /// </summary>
    public static class LevelTableExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Progression/level-table.json"; // repo 루트 기준
        private const string AssetDir = "Assets/GameData/Progression"; // 저작 전용 SO(런타임 미로드, JSON bake만). Resources 밖.
        private const string AssetName = "LevelTableDefinition";
        private const int MaxLevel = 60;

        [MenuItem("Tools/Progression/Generate Default Curve (1-60)")]
        public static void GenerateDefaultCurve()
        {
            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<LevelTableDefinition>(assetPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<LevelTableDefinition>();

            def.rows = BuildDefaultRows();

            if (isNew) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LevelTableExporter] 기본 곡선 {def.rows.Count}행 시드 완료 ({assetPath}). 조정 후 Export 하세요.");
            EditorUtility.DisplayDialog("Generate Level Curve",
                $"1~{MaxLevel} 기본 곡선 시드 완료.\n위치: {assetPath}\nInspector 에서 조정 후 Export 하세요.", "확인");
        }

        /// <summary>거듭제곱 Exp + 선형 스탯 기본 곡선(서버 커밋 JSON 과 동일 산식). 단위 테스트/검증용으로 공개.</summary>
        public static List<LevelRowDef> BuildDefaultRows()
        {
            var rows = new List<LevelRowDef>(MaxLevel);
            for (int L = 1; L <= MaxLevel; L++)
            {
                rows.Add(new LevelRowDef
                {
                    level = L,
                    expToNext = L == MaxLevel ? 0 : (long)Math.Round(100.0 * Math.Pow(L, 1.5)),
                    maxHealth = 100 + (L - 1) * 20,
                    maxMana = 50 + (L - 1) * 10,
                    attackPower = 10 + (L - 1) * 3,
                    defense = 5 + (L - 1) * 2,
                    strength = 5 + (L - 1),
                    dexterity = 5 + (L - 1),
                    intelligence = 5 + (L - 1),
                });
            }
            return rows;
        }

        [MenuItem("Tools/Progression/Export Level Table (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Level Table",
                    "LevelTableDefinition 에셋이 없습니다. 먼저 'Generate Default Curve' 또는 'Import' 로 부트스트랩하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Level Table",
                $"레벨 {count}행을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>LevelTableDefinition → level-table.json bake. 반환: 행 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:LevelTableDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelTableDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0)
            {
                Debug.LogWarning("[LevelTableExporter] LevelTableDefinition 에셋이 없습니다.");
                return 0;
            }
            if (defs.Count > 1)
            {
                Debug.LogError($"[LevelTableExporter] LevelTableDefinition 이 {defs.Count}개입니다 — 1개만 허용(레벨 진행은 단일 테이블).");
                return -1;
            }

            var rows = defs[0].rows ?? new List<LevelRowDef>();
            if (rows.Count == 0)
            {
                Debug.LogError("[LevelTableExporter] rows 가 비어있습니다.");
                return -1;
            }

            // level 은 1부터 연속·중복 없음이어야 룩업이 안전하다.
            var sorted = rows.OrderBy(r => r.level).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (sorted[i].level != i + 1)
                {
                    Debug.LogError($"[LevelTableExporter] level 이 1부터 연속이 아닙니다 (index {i} → level {sorted[i].level}).");
                    return -1;
                }
            }

            var file = new FileDto
            {
                levels = sorted.Select(r => new LevelDto
                {
                    level = r.level,
                    expToNext = r.expToNext,
                    maxHealth = r.maxHealth,
                    maxMana = r.maxMana,
                    attackPower = r.attackPower,
                    defense = r.defense,
                    strength = r.strength,
                    dexterity = r.dexterity,
                    intelligence = r.intelligence,
                }).ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[LevelTableExporter] Export 완료 — 레벨 {file.levels.Count}행\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.levels.Count;
        }

        [MenuItem("Tools/Progression/Import Level Table from JSON (bootstrap)")]
        public static void Import()
        {
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(serverPath))
            {
                EditorUtility.DisplayDialog("Import Level Table", "level-table.json 을 찾지 못했습니다.", "확인");
                return;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(serverPath));
            if (file?.levels == null || file.levels.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Level Table", "JSON 파싱 실패 또는 레벨이 없습니다.", "확인");
                return;
            }

            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<LevelTableDefinition>(assetPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<LevelTableDefinition>();

            def.rows = file.levels
                .OrderBy(l => l.level)
                .Select(l => new LevelRowDef
                {
                    level = l.level,
                    expToNext = l.expToNext,
                    maxHealth = l.maxHealth,
                    maxMana = l.maxMana,
                    attackPower = l.attackPower,
                    defense = l.defense,
                    strength = l.strength,
                    dexterity = l.dexterity,
                    intelligence = l.intelligence,
                })
                .ToList();

            if (isNew) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LevelTableExporter] Import 완료 — 레벨 {def.rows.Count}행 ({assetPath})");
            EditorUtility.DisplayDialog("Import Level Table",
                $"부트스트랩 완료 — 레벨 {def.rows.Count}행\n위치: {assetPath}", "확인");
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

        // ── JSON DTO (서버 LevelTable.Parse 와 동일 형식) ──
        [Serializable] private sealed class FileDto { public List<LevelDto> levels = new(); }

        [Serializable]
        private sealed class LevelDto
        {
            public int level;
            public long expToNext;
            public int maxHealth;
            public int maxMana;
            public int attackPower;
            public int defense;
            public int strength;
            public int dexterity;
            public int intelligence;
        }
    }
}

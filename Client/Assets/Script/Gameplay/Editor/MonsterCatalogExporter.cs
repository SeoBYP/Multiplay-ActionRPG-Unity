using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Monster;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// MonsterCatalogDefinition(SO) ↔ monsters.json 툴 (DropTableExporter 와 동일 컨벤션).
    ///
    /// - Export: SO 의 monsters → JSON bake → 서버 임베디드 경로(서버 재빌드 시 반영).
    /// - Import(부트스트랩): 기존 monsters.json → MonsterCatalogDefinition 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(Shared.Infrastructure.Monsters.MonsterCatalog.Parse)와 동일:
    ///   { "monsters": [ { "monsterId":.., "maxHp":.., ..., "expReward":.. } ] }
    /// </summary>
    public static class MonsterCatalogExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Monsters/monsters.json"; // repo 루트 기준
        private const string AssetDir = "Assets/GameData/Monster"; // 저작 전용 SO(런타임 미로드, JSON bake만). Resources 밖.
        private const string AssetName = "MonsterCatalogDefinition";

        [MenuItem("Tools/Monster/Export Monster Catalog (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Monster Catalog",
                    "MonsterCatalogDefinition 에셋이 없습니다. 먼저 'Import' 로 부트스트랩하거나 SO를 생성하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Monster Catalog",
                $"몬스터 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>MonsterCatalogDefinition → monsters.json bake. 반환: 몬스터 종 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:MonsterCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterCatalogDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0)
            {
                Debug.LogWarning("[MonsterCatalogExporter] MonsterCatalogDefinition 에셋이 없습니다.");
                return 0;
            }
            if (defs.Count > 1)
            {
                Debug.LogError($"[MonsterCatalogExporter] MonsterCatalogDefinition 이 {defs.Count}개입니다 — 1개만 허용.");
                return -1;
            }

            var monsters = defs[0].monsters ?? new List<MonsterDefinition>();
            var seen = new HashSet<string>();
            foreach (var m in monsters)
            {
                if (string.IsNullOrWhiteSpace(m.monsterId))
                {
                    Debug.LogError($"[MonsterCatalogExporter] monsterId 가 비어있는 항목: {AssetDatabase.GetAssetPath(defs[0])}");
                    return -1;
                }
                if (!seen.Add(m.monsterId))
                {
                    Debug.LogError($"[MonsterCatalogExporter] monsterId 중복: '{m.monsterId}'");
                    return -1;
                }
            }

            var file = new FileDto
            {
                monsters = monsters
                    .OrderBy(m => m.monsterId, StringComparer.Ordinal)
                    .Select(m => new MonsterDto
                    {
                        monsterId = m.monsterId,
                        maxHp = m.maxHp,
                        moveSpeed = m.moveSpeed,
                        aggroRange = m.aggroRange,
                        abilityIds = new List<string>(m.abilityIds ?? new List<string>()),
                        expReward = m.expReward,
                    })
                    .ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[MonsterCatalogExporter] Export 완료 — 몬스터 {file.monsters.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.monsters.Count;
        }

        [MenuItem("Tools/Monster/Import Monster Catalog from JSON (bootstrap)")]
        public static void Import()
        {
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(serverPath))
            {
                EditorUtility.DisplayDialog("Import Monster Catalog", "monsters.json 을 찾지 못했습니다.", "확인");
                return;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(serverPath));
            if (file?.monsters == null || file.monsters.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Monster Catalog", "JSON 파싱 실패 또는 몬스터가 없습니다.", "확인");
                return;
            }

            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<MonsterCatalogDefinition>(assetPath);
            bool isNew = def == null;
            if (isNew) def = ScriptableObject.CreateInstance<MonsterCatalogDefinition>();

            def.monsters = file.monsters
                .Select(m => new MonsterDefinition
                {
                    monsterId = m.monsterId,
                    maxHp = m.maxHp,
                    moveSpeed = m.moveSpeed,
                    aggroRange = m.aggroRange,
                    abilityIds = new List<string>(m.abilityIds ?? new List<string>()),
                    expReward = m.expReward,
                })
                .ToList();

            if (isNew) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MonsterCatalogExporter] Import 완료 — 몬스터 {def.monsters.Count}종 ({assetPath})");
            EditorUtility.DisplayDialog("Import Monster Catalog",
                $"부트스트랩 완료 — 몬스터 {def.monsters.Count}종\n위치: {assetPath}", "확인");
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

        // ── JSON DTO (서버 MonsterCatalog.Parse 와 동일 형식) ──
        [Serializable] private sealed class FileDto { public List<MonsterDto> monsters = new(); }

        [Serializable]
        private sealed class MonsterDto
        {
            public string monsterId;
            public int maxHp = 30;
            public float moveSpeed = 2.0f;
            public float aggroRange = 6f;
            public List<string> abilityIds = new();
            public int expReward = 20;
        }
    }
}

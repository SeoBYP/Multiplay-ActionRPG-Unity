using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Items;
using Game.Gameplay.Quests;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// QuestCatalogDefinition(SO) ↔ quests.json 툴 (ItemCatalogExporter 와 동일 컨벤션).
    ///
    /// - Export: SO 의 quests → JSON bake → 서버 임베디드 경로(서버 재빌드 시 반영).
    /// - Import(부트스트랩): 기존 quests.json → QuestCatalogDefinition 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(<c>Shared.Infrastructure.Quests.QuestCatalog</c>)와 동일:
    ///   { "quests": [ { "questId","name","description","objectiveType","targetId","requiredCount",
    ///                   "reward": { "exp","gold","itemId","itemQty" } } ] }
    /// </summary>
    public static class QuestCatalogExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Quests/quests.json"; // repo 루트 기준
        private const string AssetDir = "Assets/GameData/Quest"; // 저작 전용 SO(런타임 미로드, JSON bake만).
        private const string AssetName = "QuestCatalogDefinition";

        [MenuItem("Tools/Quest/Export Quest Catalog (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            EditorToolReport.Later("Export Quest Catalog", count == 0
                ? "QuestCatalogDefinition 에셋이 없습니다. 먼저 'Import' 로 부트스트랩하거나 SO를 생성하세요."
                : $"퀘스트 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.");
        }

        /// <summary>QuestCatalogDefinition → quests.json bake. 반환: 퀘스트 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:QuestCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestCatalogDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0)
            {
                Debug.LogWarning("[QuestCatalogExporter] QuestCatalogDefinition 에셋이 없습니다.");
                return 0;
            }
            if (defs.Count > 1)
            {
                Debug.LogError($"[QuestCatalogExporter] QuestCatalogDefinition 이 {defs.Count}개입니다 — 1개만 허용.");
                return -1;
            }

            var quests = defs[0].quests ?? new List<QuestDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var itemCatalog = LoadItemCatalog();

            foreach (var q in quests)
            {
                if (string.IsNullOrWhiteSpace(q.questId))
                {
                    Debug.LogError($"[QuestCatalogExporter] questId 가 비어있는 항목: {AssetDatabase.GetAssetPath(defs[0])}");
                    return -1;
                }
                if (!seen.Add(q.questId))
                {
                    Debug.LogError($"[QuestCatalogExporter] questId 중복: '{q.questId}'");
                    return -1;
                }
                if (q.requiredCount <= 0)
                {
                    Debug.LogError($"[QuestCatalogExporter] '{q.questId}' requiredCount 는 1 이상이어야 한다.");
                    return -1;
                }
                // 보상 아이템 참조 무결성 — 서버 CatalogIntegrityTests 가 잡기 전에 저작 시점에 막는다.
                if (q.rewardItemId != 0 && itemCatalog != null && itemCatalog.Get(q.rewardItemId) == null)
                {
                    Debug.LogError($"[QuestCatalogExporter] '{q.questId}' 보상 아이템 {q.rewardItemId} 가 ItemCatalogDefinition 에 없다.");
                    return -1;
                }
                if (q.rewardItemId != 0 && q.rewardItemQty <= 0)
                {
                    Debug.LogError($"[QuestCatalogExporter] '{q.questId}' 보상 아이템이 있는데 수량이 0 이다.");
                    return -1;
                }
            }

            var file = new FileDto
            {
                quests = quests.Select(q => new QuestDto
                {
                    questId = q.questId,
                    name = q.displayName,
                    description = q.description,
                    objectiveType = q.objectiveType.ToString(), // 문자열 계약 — JSON 을 사람이 읽을 때 1 보다 "CollectItem" 이 낫다
                    targetId = q.targetId,
                    requiredCount = q.requiredCount,
                    reward = new RewardDto
                    {
                        exp = q.rewardExp,
                        gold = q.rewardGold,
                        itemId = q.rewardItemId,
                        itemQty = q.rewardItemQty,
                    },
                }).ToList()
            };

            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            WriteFile(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[QuestCatalogExporter] Export 완료 — 퀘스트 {file.quests.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.quests.Count;
        }

        [MenuItem("Tools/Quest/Import Quest Catalog from JSON (bootstrap)")]
        public static void Import()
        {
            var count = ImportAll();
            if (count < 0) return;
            EditorToolReport.Later("Import Quest Catalog", $"퀘스트 {count}종을 SO 로 가져왔습니다.\n{AssetDir}/{AssetName}.asset");
        }

        /// <summary>quests.json → QuestCatalogDefinition 부트스트랩. 반환: 퀘스트 수 / -1(실패).
        /// <b>다이얼로그 없음</b> — 자동화(Unity CLI)는 이쪽을 호출한다(BakeAll 과 대칭).</summary>
        public static int ImportAll()
        {
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(serverPath))
            {
                Debug.LogError($"[QuestCatalogExporter] quests.json 이 없습니다: {serverPath}");
                return -1;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(serverPath));
            if (file?.quests == null)
            {
                Debug.LogError("[QuestCatalogExporter] quests.json 파싱에 실패했습니다.");
                return -1;
            }

            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<QuestCatalogDefinition>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<QuestCatalogDefinition>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            so.quests = file.quests.Select(d => new QuestDefinition
            {
                questId = d.questId,
                displayName = d.name,
                description = d.description,
                objectiveType = Enum.TryParse<QuestObjectiveId>(d.objectiveType, true, out var o) ? o : QuestObjectiveId.KillMonster,
                targetId = d.targetId,
                requiredCount = d.requiredCount,
                rewardExp = d.reward?.exp ?? 0,
                rewardGold = d.reward?.gold ?? 0,
                rewardItemId = d.reward?.itemId ?? 0,
                rewardItemQty = d.reward?.itemQty ?? 0,
            }).ToList();

            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[QuestCatalogExporter] Import 완료 — 퀘스트 {so.quests.Count}종 → {assetPath}");
            return so.quests.Count;
        }

        private static ItemCatalogDefinition LoadItemCatalog()
            => AssetDatabase.FindAssets("t:ItemCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemCatalogDefinition>)
                .FirstOrDefault(d => d != null);

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

        // ── JSON DTO (서버 QuestCatalog 파서와 1:1) ──
        [Serializable] private sealed class FileDto { public List<QuestDto> quests = new(); }

        [Serializable]
        private sealed class QuestDto
        {
            public string questId;
            public string name;
            public string description;
            public string objectiveType = "KillMonster";
            public string targetId;
            public int requiredCount = 1;
            public RewardDto reward = new();
        }

        [Serializable]
        private sealed class RewardDto
        {
            public long exp;
            public long gold;
            public int itemId;   // numericId. 0 = 아이템 보상 없음(문자열 시절 빈 문자열의 자리).
            public int itemQty;
        }
    }
}

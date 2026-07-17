using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.Gameplay.Monster;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// MonsterScalingDefinition(SO) ↔ monster-scaling.json 툴 (MonsterCatalogExporter 와 동일 컨벤션).
    ///
    /// - Export: SO 의 tiers → JSON bake → 서버 임베디드 경로(서버 재빌드 시 반영).
    /// - Import(부트스트랩): 기존 monster-scaling.json → SO 에셋 1개 생성/갱신.
    ///
    /// JSON 형식은 서버 로더(Shared.Infrastructure.Monsters.MonsterScalingCatalog.Parse)와 동일:
    ///   { "tiers": [ { "tier":0, "hpMultiplier":1.0, ... } ] }
    /// </summary>
    public static class MonsterScalingExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Monsters/monster-scaling.json";
        private const string AssetDir = "Assets/GameData/Monster";
        private const string AssetName = "MonsterScalingDefinition";

        [MenuItem("Tools/Monster/Export Monster Scaling (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔에 사유
            if (count == 0)
            {
                EditorUtility.DisplayDialog("Export Monster Scaling",
                    "MonsterScalingDefinition 에셋이 없습니다. 먼저 'Import' 로 부트스트랩하세요.", "확인");
                return;
            }
            EditorUtility.DisplayDialog("Export Monster Scaling",
                $"등급 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>
        /// SO → JSON bake. 반환: 등급 수 / 0(에셋없음) / -1(검증실패).
        /// <b>팝업이 없다</b> — 자동화(MCP·CI)가 이걸 직접 부른다. Export() 의 DisplayDialog 는 메인 스레드를 막는다.
        /// </summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets($"t:{AssetName}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterScalingDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0) return 0;

            var rows = defs.SelectMany(d => d.tiers ?? new List<MonsterTierRow>()).ToList();

            // 검증: 등급 중복 = 어느 쪽이 이길지 데이터로 알 수 없다 → 조용한 밸런스 붕괴.
            var dup = rows.GroupBy(r => r.tier).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
            {
                Debug.LogError($"[MonsterScalingExporter] 등급 중복: {dup.Key}");
                return -1;
            }

            // 검증: 배율 0/음수는 스탯을 0 으로 만든다(HP 0 = 즉사 몬스터).
            foreach (var r in rows)
            {
                if (r.hpMultiplier <= 0f || r.damageMultiplier <= 0f || r.expMultiplier < 0f || r.dropChanceMultiplier < 0f)
                {
                    Debug.LogError($"[MonsterScalingExporter] {r.tier}: 배율이 0 이하다(HP/피해는 양수여야 한다).");
                    return -1;
                }
            }

            // 검증: Normal 이 없으면 기본 등급이 폴백(배율 1)으로 조용히 대체된다.
            if (rows.All(r => r.tier != MonsterTierId.Normal))
            {
                Debug.LogError("[MonsterScalingExporter] Normal 등급이 없다 — 기본 등급은 반드시 저작돼야 한다.");
                return -1;
            }

            WriteFile(Path.Combine(RepoRoot(), ServerJsonRelative), ToJson(rows));
            Debug.Log($"[MonsterScalingExporter] Export 완료 — 등급 {rows.Count}종. ※ 서버 반영은 서버 재빌드 필요.");
            return rows.Count;
        }

        [MenuItem("Tools/Monster/Import Monster Scaling from JSON (bootstrap)")]
        public static void Import()
        {
            var path = Path.Combine(RepoRoot(), ServerJsonRelative);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("Import Monster Scaling", "monster-scaling.json 을 찾지 못했습니다.", "확인");
                return;
            }

            var file = JsonUtility.FromJson<FileDto>(File.ReadAllText(path));
            if (file?.tiers == null || file.tiers.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Monster Scaling", "JSON 파싱 실패 또는 등급이 없습니다.", "확인");
                return;
            }

            EnsureAssetFolder(AssetDir);
            var assetPath = $"{AssetDir}/{AssetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<MonsterScalingDefinition>(assetPath);
            bool isNew = so == null;
            if (isNew) so = ScriptableObject.CreateInstance<MonsterScalingDefinition>();

            so.tiers = file.tiers.Select(t => new MonsterTierRow
            {
                tier = (MonsterTierId)t.tier,
                hpMultiplier = t.hpMultiplier,
                damageMultiplier = t.damageMultiplier,
                expMultiplier = t.expMultiplier,
                dropChanceMultiplier = t.dropChanceMultiplier,
            }).ToList();

            if (isNew) AssetDatabase.CreateAsset(so, assetPath);
            else EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Import Monster Scaling", $"등급 {so.tiers.Count}종을 SO 로 가져왔습니다.", "확인");
        }

        private static string ToJson(List<MonsterTierRow> rows)
        {
            var sb = new StringBuilder("{\n    \"tiers\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("        {\n");
                sb.Append("            \"tier\": ").Append((int)r.tier).Append(",\n");
                sb.Append("            \"hpMultiplier\": ").Append(F(r.hpMultiplier)).Append(",\n");
                sb.Append("            \"damageMultiplier\": ").Append(F(r.damageMultiplier)).Append(",\n");
                sb.Append("            \"expMultiplier\": ").Append(F(r.expMultiplier)).Append(",\n");
                sb.Append("            \"dropChanceMultiplier\": ").Append(F(r.dropChanceMultiplier)).Append('\n');
                sb.Append("        }").Append(i < rows.Count - 1 ? "," : "").Append('\n');
            }
            sb.Append("    ]\n}\n");
            return sb.ToString();
        }

        /// <summary>InvariantCulture 고정 — 한국어 로캘에서 소수점이 ',' 로 나가면 서버 파싱이 깨진다.</summary>
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        private static string RepoRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
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

        [Serializable] private sealed class FileDto { public List<TierDto> tiers = new(); }
        [Serializable] private sealed class TierDto
        {
            public int tier;
            public float hpMultiplier = 1f;
            public float damageMultiplier = 1f;
            public float expMultiplier = 1f;
            public float dropChanceMultiplier = 1f;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Abilities;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// SkillCatalogDefinition(SO) → skills.json bake 툴 (MonsterCatalogExporter 와 동일 컨벤션).
    /// SO 저작 진실원 → Export → 서버 임베디드 skills.json → 서버 재빌드 시 반영(gas-architecture §2.5).
    ///
    /// JSON 형식은 서버 로더(Shared.Infrastructure.Skills.SkillCatalog.Parse)와 동일:
    ///   { "skills": [ { "id", "startupMs",.., "hitboxShape", "offsetX/Y/Z", "halfX/Y/Z", "onHitEffectIds"[] } ] }
    /// hitboxShape 는 enum 이름 문자열로 직렬화(서버가 Enum.TryParse).
    /// </summary>
    public static class SkillCatalogExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Skills/skills.json"; // repo 루트 기준

        [MenuItem("Tools/Skill/Export Skill Catalog (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔 사유
            EditorUtility.DisplayDialog("Export Skill Catalog",
                count == 0
                    ? "SkillCatalogDefinition 에셋이 없습니다. 먼저 생성하고 SkillDefinition 을 등록하세요."
                    : $"스킬 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>SkillCatalogDefinition → skills.json bake. 반환: 스킬 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:SkillCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SkillCatalogDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0) { Debug.LogWarning("[SkillCatalogExporter] SkillCatalogDefinition 에셋이 없습니다."); return 0; }
            if (defs.Count > 1) { Debug.LogError($"[SkillCatalogExporter] SkillCatalogDefinition 이 {defs.Count}개입니다 — 1개만 허용."); return -1; }

            var skills = defs[0].skills ?? new List<SkillDefinition>();
            var seen = new HashSet<string>();
            var dtos = new List<SkillDto>();
            foreach (var s in skills)
            {
                if (s == null) continue;
                if (string.IsNullOrWhiteSpace(s.id)) { Debug.LogError($"[SkillCatalogExporter] id 가 비어있는 SkillDefinition: {AssetDatabase.GetAssetPath(s)}"); return -1; }
                if (!seen.Add(s.id)) { Debug.LogError($"[SkillCatalogExporter] skill id 중복: '{s.id}'"); return -1; }

                dtos.Add(new SkillDto
                {
                    id = s.id,
                    startupMs = s.startupMs, activeMs = s.activeMs, recoveryMs = s.recoveryMs, cooldownMs = s.cooldownMs,
                    manaCost = s.manaCost,
                    hitboxShape = s.hitboxShape.ToString(),
                    offsetX = s.hitboxOffset.x, offsetY = s.hitboxOffset.y, offsetZ = s.hitboxOffset.z,
                    halfX = s.hitboxHalfExtents.x, halfY = s.hitboxHalfExtents.y, halfZ = s.hitboxHalfExtents.z,
                    onHitEffectIds = new List<string>(s.onHitEffectIds ?? new List<string>()),
                    comboChainMs = s.comboChainMs, comboWindowMs = s.comboWindowMs,
                });

                // 불변식 검증(저작 실수 조기 차단): 체인 ≤ 창.
                if (s.comboChainMs > 0 && s.comboWindowMs > 0 && s.comboChainMs > s.comboWindowMs)
                {
                    Debug.LogError($"[SkillCatalogExporter] '{s.id}': comboChainMs({s.comboChainMs}) > comboWindowMs({s.comboWindowMs}) — 체인 지점이 창보다 늦으면 콤보가 절대 이어지지 않는다.");
                    return -1;
                }
            }

            var file = new FileDto { skills = dtos.OrderBy(d => d.id, StringComparer.Ordinal).ToList() };
            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            var dir = Path.GetDirectoryName(serverPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[SkillCatalogExporter] Export 완료 — 스킬 {file.skills.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.skills.Count;
        }

        /// <summary>Application.dataPath = repo/Client/Assets → repo 루트.</summary>
        private static string RepoRoot() => Directory.GetParent(Application.dataPath)!.Parent!.FullName;

        [Serializable] private sealed class FileDto { public List<SkillDto> skills = new(); }

        [Serializable]
        private sealed class SkillDto
        {
            public string id;
            public int startupMs, activeMs, recoveryMs, cooldownMs, manaCost;
            public string hitboxShape;
            public float offsetX, offsetY, offsetZ, halfX, halfY, halfZ;
            public List<string> onHitEffectIds = new();
            public int comboChainMs, comboWindowMs; // 콤보 타이밍(서버·클라 공유)
        }
    }
}

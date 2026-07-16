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
    /// AbilityCatalogDefinition(SO) → abilities.json bake 툴 (SkillCatalogExporter 와 동일 컨벤션, AC-B).
    /// SO 저작 진실원 → Export → 서버 임베디드 abilities.json → 서버 재빌드 시 반영(gas-architecture §2.5).
    ///
    /// <b>Cue(cueTrigger/cueComboStep)는 bake 하지 않는다</b> — 서버는 연출을 하나도 모른다(gas §2 원칙).
    /// 클라는 SO 를 직접 조회하므로 JSON 에 Cue 가 없어도 된다.
    ///
    /// JSON 형식(서버 Shared.Infrastructure.Abilities.AbilityCatalog.Parse 와 동일):
    ///   { "abilities": [ { "id","networkId","startupMs",..,"baseDamage","activationRange","onHitEffectIds"[] } ] }
    /// </summary>
    public static class AbilityCatalogExporter
    {
        private const string ServerJsonRelative = "ServerAll/Shared/Shared.Infrastructure/Abilities/abilities.json"; // repo 루트 기준

        [MenuItem("Tools/Ability/Export Ability Catalog (SO → JSON)")]
        public static void Export()
        {
            var count = BakeAll();
            if (count < 0) return; // 검증 실패 — 콘솔 사유
            EditorUtility.DisplayDialog("Export Ability Catalog",
                count == 0
                    ? "AbilityCatalogDefinition 에셋이 없습니다. 먼저 생성하고 AbilityDefinition 을 등록하세요."
                    : $"어빌리티 {count}종을 서버 JSON 에 기록했습니다.\n서버 반영은 서버 재빌드가 필요합니다.", "확인");
        }

        /// <summary>AbilityCatalogDefinition → abilities.json bake. 반환: 어빌리티 수 / 0(에셋없음) / -1(검증실패).</summary>
        public static int BakeAll()
        {
            var defs = AssetDatabase.FindAssets("t:AbilityCatalogDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AbilityCatalogDefinition>)
                .Where(d => d != null)
                .ToList();

            if (defs.Count == 0) { Debug.LogWarning("[AbilityCatalogExporter] AbilityCatalogDefinition 에셋이 없습니다."); return 0; }
            if (defs.Count > 1) { Debug.LogError($"[AbilityCatalogExporter] AbilityCatalogDefinition 이 {defs.Count}개입니다 — 1개만 허용."); return -1; }

            var abilities = defs[0].abilities ?? new List<AbilityDefinition>();
            var seenIds = new HashSet<string>();
            var seenNetworkIds = new HashSet<int>();
            var dtos = new List<AbilityDto>();

            foreach (var a in abilities)
            {
                if (a == null) continue;

                if (string.IsNullOrWhiteSpace(a.id))
                {
                    Debug.LogError($"[AbilityCatalogExporter] id 가 비어있는 AbilityDefinition: {AssetDatabase.GetAssetPath(a)}");
                    return -1;
                }
                if (!seenIds.Add(a.id))
                {
                    Debug.LogError($"[AbilityCatalogExporter] ability id 중복: '{a.id}'");
                    return -1;
                }
                // networkId 는 패킷 계약 키 — 중복되면 서버가 잘못된 어빌리티를 발동한다(조기 차단).
                if (!seenNetworkIds.Add(a.networkId))
                {
                    Debug.LogError($"[AbilityCatalogExporter] networkId 중복: {a.networkId} ('{a.id}') — 패킷 SkillId 가 겹치면 안 된다.");
                    return -1;
                }
                // 불변식 검증(저작 실수 조기 차단): 체인 ≤ 창.
                if (a.comboChainMs > 0 && a.comboWindowMs > 0 && a.comboChainMs > a.comboWindowMs)
                {
                    Debug.LogError($"[AbilityCatalogExporter] '{a.id}': comboChainMs({a.comboChainMs}) > comboWindowMs({a.comboWindowMs}) — 체인 지점이 창보다 늦으면 콤보가 절대 이어지지 않는다.");
                    return -1;
                }

                dtos.Add(new AbilityDto
                {
                    id = a.id,
                    networkId = a.networkId,
                    startupMs = a.startupMs, activeMs = a.activeMs, recoveryMs = a.recoveryMs, cooldownMs = a.cooldownMs,
                    manaCost = a.manaCost,
                    hitboxShape = a.hitboxShape.ToString(),
                    offsetX = a.hitboxOffset.x, offsetY = a.hitboxOffset.y, offsetZ = a.hitboxOffset.z,
                    halfX = a.hitboxHalfExtents.x, halfY = a.hitboxHalfExtents.y, halfZ = a.hitboxHalfExtents.z,
                    baseDamage = a.baseDamage,
                    activationRange = a.activationRange,
                    onHitEffectIds = new List<string>(a.onHitEffectIds ?? new List<string>()),
                    comboChainMs = a.comboChainMs, comboWindowMs = a.comboWindowMs,
                    // ※ cueTrigger/cueComboStep 은 의도적으로 제외 — 서버는 연출을 모른다.
                });
            }

            var file = new FileDto { abilities = dtos.OrderBy(d => d.id, StringComparer.Ordinal).ToList() };
            var json = JsonUtility.ToJson(file, true);
            var serverPath = Path.Combine(RepoRoot(), ServerJsonRelative);
            var dir = Path.GetDirectoryName(serverPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(serverPath, json);

            AssetDatabase.Refresh();
            Debug.Log($"[AbilityCatalogExporter] Export 완료 — 어빌리티 {file.abilities.Count}종\n  서버: {serverPath}\n  ※ 서버 반영은 서버 재빌드 필요.");
            return file.abilities.Count;
        }

        /// <summary>Application.dataPath = repo/Client/Assets → repo 루트.</summary>
        private static string RepoRoot() => Directory.GetParent(Application.dataPath)!.Parent!.FullName;

        [Serializable] private sealed class FileDto { public List<AbilityDto> abilities = new(); }

        [Serializable]
        private sealed class AbilityDto
        {
            public string id;
            public int networkId;
            public int startupMs, activeMs, recoveryMs, cooldownMs, manaCost;
            public string hitboxShape;
            public float offsetX, offsetY, offsetZ, halfX, halfY, halfZ;
            public int baseDamage;
            public float activationRange;
            public List<string> onHitEffectIds = new();
            public int comboChainMs, comboWindowMs;
        }
    }
}

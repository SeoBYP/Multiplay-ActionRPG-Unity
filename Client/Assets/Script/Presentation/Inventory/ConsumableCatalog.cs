using System;
using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 소모품 효과 데이터(클라 저작 SO). itemId → 적용할 스탯 효과들.
    ///
    /// 회복 효과 *수치/대상 스탯*은 데이터(여기), *적용*은 GAS(ConsumableEffectHandler가 GameplayEffectDefinition 빌드).
    /// 서버는 회복을 적용하지 않으므로(플레이어 HP=클라 권위) 이 데이터는 클라 전용 — 서버와 공유하지 않는다.
    /// 새 소모품은 코드 없이 이 SO 에 항목만 추가하면 된다(heal/mana/buff 모두 스탯 modifier).
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableCatalog", menuName = "Game/Consumable Catalog", order = 2)]
    public sealed class ConsumableCatalog : ScriptableObject
    {
        [Tooltip("소모품 itemId 별 효과. itemId 는 서버 ItemCatalog 와 문자열 정렬(예: potion_hp_small).")]
        public List<ConsumableDef> items = new();

        /// <summary>itemId 의 소모품 정의. 미등록(=소모품 아님)이면 null.</summary>
        public ConsumableDef Get(string itemId)
        {
            foreach (var d in items)
                if (d.itemId == itemId)
                    return d;
            return null;
        }
    }

    /// <summary>한 소모품의 효과 목록.</summary>
    [Serializable]
    public sealed class ConsumableDef
    {
        public string itemId;
        public List<ConsumableEffectDef> effects = new();
    }

    /// <summary>스탯 효과 1개 — GAS GameplayEffect 로 적용된다(Instant 회복/지속 버프).</summary>
    [Serializable]
    public sealed class ConsumableEffectDef
    {
        [Tooltip("대상 스탯(Health=체력 회복, Mana=마나 회복 등).")]
        public EGameplayAttribute stat = EGameplayAttribute.Health;
        [Tooltip("증감량(+회복).")]
        public int amount = 30;
        [Tooltip("Instant=즉발 / Duration=지속 버프(durationMs).")]
        public EDurationPolicy policy = EDurationPolicy.Instant;
        public int durationMs;
    }
}

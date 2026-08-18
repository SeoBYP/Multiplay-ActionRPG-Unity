using System;
using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using Shared.Gameplay.Equipment;
using Shared.Gameplay.Items;
using UnityEngine;

namespace Game.Gameplay.Items
{
    /// <summary>
    /// 아이템 정의 저작(authoring) 진실원. 디자이너가 이 SO 하나를 Inspector 에서 편집한다.
    /// (MonsterCatalogDefinition·DropTableDefinition 과 동일 컨벤션 — 단일 SO + List, SO 저작 → JSON bake → 서버 임베디드.)
    ///
    /// 서버(UnityEngine 의존 0)는 SO 를 못 읽으므로 Export 툴(Tools/Item/Export)이 items.json 으로 bake →
    /// <c>Shared.Infrastructure.Items.ItemCatalogData</c> 가 읽어 Item/Equipment/Shop/Consumable 4개 테이블로 분해한다.
    ///
    /// <para><b>왜 이 SO 가 뒤늦게 생겼나</b>: bake 산출물 7종 중 items.json 만 Exporter 가 없어
    /// 서버 JSON 과 클라 <c>ItemDisplayCatalog</c> 를 각각 손으로 저작했고, 실제로 갈라졌다
    /// (gold_pouch 가 클라에만 존재 · 진열 순서 어긋남, 2026-08-18 실측). 이 SO 가 그 이중 저작을 없앤다.</para>
    ///
    /// <para><b>표시 데이터는 여기 없다</b>: 이름·아이콘·설명·등급은 서버가 쓰지 않으므로 bake 대상이 아니고
    /// <c>ItemDisplayCatalog</c>(Presentation) 가 계속 소유한다. 이 SO = "무엇인가"(스택·장비스탯·상점가·소비효과).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalogDefinition", menuName = "Game/Item Catalog Definition", order = 5)]
    public sealed class ItemCatalogDefinition : ScriptableObject
    {
        [Tooltip("아이템 정의 목록. **이 순서가 곧 상점 진열 순서다**(ItemCatalogData: \"파일 순서 = 저작 순서 = 상점 진열 순서\").\n" +
                 "Export 는 이 순서를 그대로 보존한다 — 알파벳 정렬하지 않는다.")]
        public List<ItemDefinition> items = new();

        /// <summary>itemId 의 정의. 미등록이면 null. (클라 런타임 조회용)</summary>
        public ItemDefinition Get(string itemId)
        {
            foreach (var i in items)
                if (i.itemId == itemId)
                    return i;
            return null;
        }
    }

    /// <summary>아이템 1종 — 스택 규칙 + (장비면) 스탯 + (상점 취급이면) 가격 + (소비형이면) 효과.</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        [Tooltip("아이템 키(서버·클라 공용). ItemDisplayCatalog·proto·DB 가 모두 이 값을 참조한다.")]
        public string itemId;

        [Header("스택")]
        public bool stackable;
        [Tooltip("한 칸 최대 수량. stackable=false 면 서버가 1 로 취급한다.")]
        public int maxStack = 1;

        [Header("장비 (isEquipment=false 면 아래 무시)")]
        public bool isEquipment;
        public EquipmentType equipSlot = EquipmentType.None;
        [Tooltip("장비 한 점이 더하는 가산 스탯. 합산은 서버 GetStatsAsync 단일 권위에서 base(레벨) 위에 Σ 한다.")]
        public ItemEquipStats equipStats = new();

        [Header("상점 (isShopItem=false 면 아래 무시)")]
        public bool isShopItem;
        public long buyPrice;
        public long sellPrice;
        public ShopCategory shopCategory = ShopCategory.Unspecified;

        [Header("소비 효과 (비우면 사용 불가 아이템)")]
        [Tooltip("사용 시 적용할 효과. 서버는 effectId == itemId 규칙으로 매핑하므로 별도 id 가 없다.\n" +
                 "policy/durationMs 는 **첫 항목의 값**이 효과 전체에 적용된다(서버 ItemCatalogData 파서 규칙).")]
        public List<ItemConsumeEffect> consumeEffects = new();
    }

    /// <summary>장비 가산 스탯. 서버 <c>EquipmentStatModifier</c> 와 1:1. 기본 0 = 영향 없음.</summary>
    [Serializable]
    public sealed class ItemEquipStats
    {
        public int maxHealth;
        public int maxMana;
        public int attackPower;
        public int defense;
        public int strength;
        public int dexterity;
        public int intelligence;
    }

    /// <summary>소비 효과 1건. 서버가 <c>GameplayAttributeModifier</c>(Additive) 로 변환한다.</summary>
    [Serializable]
    public sealed class ItemConsumeEffect
    {
        public EGameplayAttribute stat = EGameplayAttribute.Health;
        public int amount;
        [Tooltip("Instant = 즉발(회복). Duration = 지속(버프) — durationMs 필요.")]
        public EDurationPolicy policy = EDurationPolicy.Instant;
        public int durationMs;
    }
}

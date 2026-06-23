using UnityEngine;

namespace Game.Presentation.Inventory
{
    /// <summary>
    /// 등급(레어도) → 슬롯 배경 스프라이트 매핑(ScriptableObject). 인벤/상점/장비 슬롯이 공유하는 단일 소스.
    /// fantasy_gui_4 의 fg4_slot{색} 스프라이트를 등급별로 인스펙터에서 할당한다(Common 회 / Rare 파 / Epic 보 / Legendary 주).
    /// 슬롯은 도메인을 모르고 Sprite 만 받으므로(decoupled), grade→Sprite 해석은 Model 레이어가 이 카탈로그로 수행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Inventory/Grade Sprite Catalog", fileName = "GradeSpriteCatalog")]
    public sealed class GradeSpriteCatalog : ScriptableObject
    {
        [SerializeField] private Sprite common;
        [SerializeField] private Sprite rare;
        [SerializeField] private Sprite epic;
        [SerializeField] private Sprite legendary;

        /// <summary>등급의 배경 스프라이트. 미할당이면 null(슬롯이 배경을 끔).</summary>
        public Sprite Get(ItemGrade grade) => grade switch
        {
            ItemGrade.Rare => rare,
            ItemGrade.Epic => epic,
            ItemGrade.Legendary => legendary,
            _ => common,
        };
    }
}

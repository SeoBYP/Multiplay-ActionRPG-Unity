using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.GUI.Common;
using Game.Presentation.Inventory;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Inventory
{
    /// <summary>
    /// Inventory 창 (MVI View).
    ///   InventoryModel만 주입받는다(proto·System 비참조).
    ///   탭/닫기 → Intent, State → 슬롯 렌더. 표시데이터는 Model이 카탈로그로 합성해 둔 것.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public enum ItemType
        {
            All,
            Equipment, // 장비
            Consumable, // 소비 아이템
            Material,   // 재료
            Quest,      // 퀘스트
            Etc         // 기타
        }

        // 닫기 버튼
        [SerializeField] private Button closeButton;

        [Serializable]
        // 인벤토리 탭
        private class ItemTab
        {
            public ItemType type;
            public Toggle toggle;
        }
        /// <summary>
        /// 인벤토리 탭들
        /// </summary>
        [SerializeField] private ItemTab[] itemTabs;

        /// <summary>
        /// 아이템 슬롯 ScrollRect
        /// </summary>
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contents;

        /// <summary>
        /// 현재 보유중인 아이템 슬롯
        /// </summary>
        private List<UniversalSlot> activeSlots = new List<UniversalSlot>();

        [Inject] private InventoryModel _model;

        private IDisposable _stateSubscription;

        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            itemTabs = new ItemTab[]
            {
                new ItemTab { type = ItemType.All, toggle = this.FindChildComponentByName<Toggle>("ItemTab_All") },
                new ItemTab { type = ItemType.Equipment, toggle = this.FindChildComponentByName<Toggle>("ItemTab_Equipment") },
                new ItemTab { type = ItemType.Consumable, toggle = this.FindChildComponentByName<Toggle>("ItemTab_Consumable") },
                new ItemTab { type = ItemType.Material, toggle = this.FindChildComponentByName<Toggle>("ItemTab_Material") },
                new ItemTab { type = ItemType.Quest, toggle = this.FindChildComponentByName<Toggle>("ItemTab_Quest") },
                new ItemTab { type = ItemType.Etc, toggle = this.FindChildComponentByName<Toggle>("ItemTab_Etc") },
            };

            closeButton = this.FindChildComponentByName<Button>("btn_close");

            scrollRect = this.FindChildComponentByName<ScrollRect>("ScrollView");
            contents = scrollRect.content;
        }

        private void Awake()
        {
            activeSlots = contents.GetComponentsInChildren<UniversalSlot>(true).ToList();
        }

        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            // 탭 토글 → SelectTab 인텐트 (All 은 SelectedCategory == null).
            if (itemTabs != null)
            {
                foreach (var tab in itemTabs)
                {
                    if (tab?.toggle == null) continue;
                    var captured = tab;
                    tab.toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (isOn) _model.Accept(new InventoryIntent.SelectTab(ToCategory(captured.type)));
                    });
                }
            }

            // R3 구독 — AddTo(CancellationToken)은 Game.GUI.Common import와 오버로드가 꼬여 CS1620이 나므로
            // IDisposable을 직접 보관해 OnDestroy에서 해제한다.
            _stateSubscription = _model.State.Subscribe(Render);

            // 창이 처음 열릴 때(Start 시점) 1회 갱신.
            _model.Accept(InventoryIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        private void OnEnable()
        {
            // 재오픈(SetActive false→true) 시 최신화. 최초 활성화 때는 _model 주입 전이라 null → Start가 담당.
            if (_model != null)
                _model.Accept(InventoryIntent.Refresh.Instance);
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }

        private void Render(InventoryState state)
        {
            var filtered = Filter(state.Items, state.SelectedCategory);

            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (i < filtered.Count)
                {
                    activeSlots[i].Show();
                    activeSlots[i].ItemContents?.Bind(filtered[i].Icon, filtered[i].Quantity);
                }
                else
                {
                    activeSlots[i].Hide();
                }
            }

            // 슬롯 풀보다 아이템이 많으면 잘림 — 침묵 금지(프리팹 슬롯 수 조정 필요).
            if (filtered.Count > activeSlots.Count)
                Debug.LogWarning($"[Inventory] 아이템 {filtered.Count}개 > 슬롯 {activeSlots.Count}개 — 일부 미표시. 슬롯 풀을 늘려라.");
        }

        private static List<InventoryItemModel> Filter(IReadOnlyList<InventoryItemModel> items, ItemCategory? category)
        {
            if (items == null) return new List<InventoryItemModel>();
            if (category == null) return items.ToList(); // All
            return items.Where(i => i.Category == category.Value).ToList();
        }

        /// <summary>탭 ItemType → 도메인 ItemCategory?. All 은 null(전체).</summary>
        private static ItemCategory? ToCategory(ItemType type) => type switch
        {
            ItemType.Equipment => ItemCategory.Equipment,
            ItemType.Consumable => ItemCategory.Consumable,
            ItemType.Material => ItemCategory.Material,
            ItemType.Quest => ItemCategory.Quest,
            ItemType.Etc => ItemCategory.Etc,
            _ => null, // All
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.GUI.Common;
using Game.Presentation.Inventory;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        [Header("Slot Generation")]
        [Tooltip("총 슬롯(컨테이너) 개수 — 고정.")]
        [SerializeField] private int slotCount = 30;

        // 슬롯 prefab은 Addressable로 로드(UniversalSlot / ItemContentsSlot) — Inspector 할당 불요.
        private UniversalSlot _universalSlotPrefab;
        private ItemContentsSlot _itemContentsPrefab;
        private AsyncOperationHandle<GameObject> _slotHandle;
        private AsyncOperationHandle<GameObject> _contentHandle;

        /// <summary>
        /// 생성된 컨테이너 슬롯들(고정 slotCount 개). 빈 칸은 Content 없이 컨테이너만 보인다.
        /// </summary>
        private readonly List<UniversalSlot> activeSlots = new List<UniversalSlot>();

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

        /// <summary>UniversalSlot / ItemContentsSlot prefab을 Addressable로 로드한다.</summary>
        private async UniTask LoadSlotPrefabsAsync()
        {
            _slotHandle    = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.UniversalSlot);
            _contentHandle = Addressables.LoadAssetAsync<GameObject>(AddressKeys.UI.ItemContentsSlot);
            try
            {
                await _slotHandle.Task.AsUniTask();
                await _contentHandle.Task.AsUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Inventory] 슬롯 prefab Addressable 로드 실패: {e.Message}");
                return;
            }

            _universalSlotPrefab = _slotHandle.Result != null ? _slotHandle.Result.GetComponent<UniversalSlot>() : null;
            _itemContentsPrefab  = _contentHandle.Result != null ? _contentHandle.Result.GetComponent<ItemContentsSlot>() : null;

            if (_universalSlotPrefab == null)
                Debug.LogError("[Inventory] UniversalSlot prefab 로드 실패/컴포넌트 없음 (Addressable 주소 확인)");
            if (_itemContentsPrefab == null)
                Debug.LogError("[Inventory] ItemContentsSlot prefab 로드 실패/컴포넌트 없음 (Addressable 주소 확인)");
        }

        /// <summary>컨테이너 슬롯을 slotCount 개 무조건 생성한다(프리팹에 미리 박힌 슬롯은 정리해 중복 방지).</summary>
        private void BuildSlots()
        {
            if (_universalSlotPrefab == null || contents == null)
            {
                Debug.LogError("[Inventory] UniversalSlot prefab/contents 없음 — 슬롯 생성 불가");
                return;
            }

            foreach (var existing in contents.GetComponentsInChildren<UniversalSlot>(true))
                Destroy(existing.gameObject);

            activeSlots.Clear();
            for (int i = 0; i < slotCount; i++)
                activeSlots.Add(Instantiate(_universalSlotPrefab, contents));
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

            // 슬롯 prefab을 Addressable로 로드한 뒤 슬롯 생성·구독·첫 갱신을 순서대로 한다.
            InitializeAsync().Forget();
        }

        /// <summary>슬롯 prefab 로드 → slotCount 개 컨테이너 생성 → State 구독 → 첫 Refresh.</summary>
        private async UniTaskVoid InitializeAsync()
        {
            await LoadSlotPrefabsAsync();
            BuildSlots();

            // R3 구독 — AddTo(CancellationToken)은 Game.GUI.Common import와 오버로드가 꼬여 CS1620이 나므로
            // IDisposable을 직접 보관해 OnDestroy에서 해제한다.
            _stateSubscription = _model.State.Subscribe(Render);

            // 슬롯 준비 후 1회 갱신.
            _model.Accept(InventoryIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            if (_slotHandle.IsValid()) Addressables.Release(_slotHandle);
            if (_contentHandle.IsValid()) Addressables.Release(_contentHandle);
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
                    // 아이템 있는 칸 — Content를 동적 생성/보장 후 바인딩.
                    var content = activeSlots[i].EnsureContent(_itemContentsPrefab);
                    content?.Bind(filtered[i].Icon, filtered[i].Quantity);
                }
                else
                {
                    // 빈 칸 — 컨테이너만 두고 Content 숨김.
                    activeSlots[i].ClearContent();
                }
            }

            // 슬롯 수보다 아이템이 많으면 잘림 — 침묵 금지(slotCount 조정 필요).
            if (filtered.Count > activeSlots.Count)
                Debug.LogWarning($"[Inventory] 아이템 {filtered.Count}개 > 슬롯 {activeSlots.Count}개 — 일부 미표시. slotCount를 늘려라.");
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

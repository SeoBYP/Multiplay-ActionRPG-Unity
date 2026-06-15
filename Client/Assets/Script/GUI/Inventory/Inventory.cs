using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.GUI.Common;
using Game.Presentation.Inventory;
using Script.GUI.Inventory;
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
        private IDisposable _toastSubscription;

        private Canvas _canvas;
        private readonly ItemActionPanelController _actionPanel = new ItemActionPanelController();

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

            // 창이 활성인 동안 게임플레이(Player) 입력 점유 → WASD 이동·단축키 차단(DungeonRoomLobbyView와 동일).
            // OnDisable(SetActive(false)/Destroy)에서 자동 해제. 토글로 다시 켜지면 OnEnable이 재점유.
            if (_model != null)
                gameObject.AddComponent<UiInputCaptureBehaviour>()
                          .Bind(_model.BeginUiCapture, _model.EndUiCapture);

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

            // Side Effect: 토스트(사용/실패). 정식 위젯은 7.x — 현재는 로그.
            _toastSubscription = _model.OnToast.Subscribe(msg => Debug.Log($"[Inventory] 토스트: {msg}"));

            // 슬롯 준비 후 1회 갱신.
            _model.Accept(InventoryIntent.Refresh.Instance);
        }

        private void OnDestroy()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _toastSubscription?.Dispose();
            _toastSubscription = null;
            _actionPanel.Close();
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
            _actionPanel.Close();
            gameObject.SetActive(false);
        }

        private void Render(InventoryState state)
        {
            var filtered = Filter(state.Items, state.SelectedCategory);

            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (i < filtered.Count)
                {
                    // 아이템 있는 칸 — Content를 동적 생성/보장 후 바인딩. 클릭 → 슬롯 오른쪽에 액션 패널.
                    var content = activeSlots[i].EnsureContent(_itemContentsPrefab);
                    var item = filtered[i];
                    var slotRect = (RectTransform)activeSlots[i].transform;
                    content?.Bind(item.ItemId, item.Icon, item.Quantity, _ => OpenActionPanel(item, slotRect).Forget());
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

        // ── 액션 패널 (슬롯 클릭 → 오른쪽 팝업, Canvas 직속) ──

        /// <summary>
        /// 클릭한 슬롯 오른쪽에 ItemActionPanel 을 Canvas 직속으로 생성. 뒤에 백드롭(클릭 시 닫힘)을 깐다.
        /// 팝업은 클릭 시에만 필요하므로 **온디맨드 Addressable 로드**(필드로 들고 있지 않음). 닫을 때 ReleaseInstance.
        /// </summary>
        private async UniTask OpenActionPanel(InventoryItemModel item, RectTransform slotRect)
        {
            // 분류에 따라 버튼 노출: 소모품=사용, 장비=장착. 그 외엔 둘 다 비활성. (해제는 장비창에서만)
            bool canUse = item.Category == ItemCategory.Consumable;
            bool canEquip = item.Category == ItemCategory.Equipment;
            await _actionPanel.OpenAsync(ResolveCanvas(), slotRect, panel => panel.Bind(
                item.ItemId,
                id => _model.Accept(new InventoryIntent.UseItem(id)),
                id => _model.Accept(new InventoryIntent.EquipItem(id)),
                null,
                canUse,
                canEquip,
                canUnequip: false));
        }

        private Canvas ResolveCanvas()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            return _canvas;
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

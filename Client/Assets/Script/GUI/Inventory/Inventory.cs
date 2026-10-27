using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.GUI.Common;
using Game.Presentation.Inventory;
using Script.GUI.Inventory;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.Inventory
{
    /// <summary>
    /// Inventory 창 (MVI View). InventoryModel만 주입받는다. 탭 → Intent, State → 슬롯 렌더(표시 데이터는 Model 이 합성).
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

        [SerializeField] private Button closeButton;

        [Serializable]
        private class ItemTab
        {
            public ItemType type;
            public Toggle toggle;
        }
        [SerializeField] private ItemTab[] itemTabs;

        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contents;

        [Header("Slot Generation")]
        [Tooltip("총 슬롯(컨테이너) 개수 — 고정.")]
        [SerializeField] private int slotCount = 30;

        [Header("Gold")]
        [SerializeField] private TextMeshProUGUI _goldText;
        
        // 슬롯 prefab은 Addressable로 로드(UniversalSlot / ItemContentsSlot) — Inspector 할당 불요.
        private UniversalSlot _universalSlotPrefab;
        private ItemContentsSlot _itemContentsPrefab;
        private AsyncOperationHandle<GameObject> _slotHandle;
        private AsyncOperationHandle<GameObject> _contentHandle;

        /// <summary>컨테이너 슬롯들(고정 slotCount 개). 빈 칸은 Content 없이 컨테이너만 보인다.</summary>
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

            // 창 활성 동안 게임플레이 입력 점유(WASD·단축키 차단). OnDisable 에서 자동 해제, 토글로 다시 켜지면 OnEnable 이 재점유.
            if (_model != null)
                gameObject.AddComponent<UiInputCaptureBehaviour>()
                          .Bind(_model.BeginUiCapture, _model.EndUiCapture);

            // All 은 SelectedCategory == null.
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

            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            await LoadSlotPrefabsAsync();
            BuildSlots();

            // R3 구독 — AddTo(CancellationToken)은 Game.GUI.Common import와 오버로드가 꼬여 CS1620이 나므로
            // IDisposable을 직접 보관해 OnDestroy에서 해제한다.
            _stateSubscription = _model.State.Subscribe(Render);

            // 토스트: 실패는 AlertPopup, 성공은 로그.
            _toastSubscription = _model.OnToast.Subscribe(ShowToast);

            _model.Accept(InventoryIntent.Refresh.Instance);
        }

        private void ShowToast(InventoryToast toast)
        {
            if (!toast.Success)
            {
                ShowFailPopupAsync(toast.Message).Forget();
                return;
            }
            Debug.Log($"[Inventory] {toast.Message}");
        }

        private async UniTaskVoid ShowFailPopupAsync(string message)
        {
            if (GUIRoot.Instance == null)
            {
                Debug.LogWarning($"[Inventory] 실패: {message}");
                return;
            }

            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.AlertPopup, GUIRoot.Instance.transform, destroyCancellationToken);
            if (inst == null) return;

            var popup = inst.GameObject.GetComponent<AlertPopup>();
            if (popup == null) { inst.Dispose(); return; }

            popup.SetAddressableOwner(inst);
            popup.Setup("인벤토리", message, glow: PopupGlowType.Warning);
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
            if (_goldText != null)
                _goldText.text = state.Gold.ToString("N0");

            var filtered = Filter(state.Items, state.SelectedCategory);

            for (int i = 0; i < activeSlots.Count; i++)
            {
                if (i < filtered.Count)
                {
                    var content = activeSlots[i].EnsureContent(_itemContentsPrefab);
                    var item = filtered[i];
                    var slotRect = (RectTransform)activeSlots[i].transform;
                    content?.Bind(item.ItemId, item.Icon, item.Quantity, _ => OpenActionPanel(item, slotRect).Forget(),
                        item.GradeBackground);
                }
                else
                {
                    activeSlots[i].ClearContent();
                }
            }

            // 슬롯 수보다 아이템이 많으면 잘림 — 침묵 금지(slotCount 조정 필요).
            if (filtered.Count > activeSlots.Count)
                Debug.LogWarning($"[Inventory] 아이템 {filtered.Count}개 > 슬롯 {activeSlots.Count}개 — 일부 미표시. slotCount를 늘려라.");
        }

        /// <summary>클릭한 슬롯 오른쪽에 ItemActionPanel 을 온디맨드로 띄운다(공용 ItemActionPanelController).</summary>
        private async UniTask OpenActionPanel(InventoryItemModel item, RectTransform slotRect)
        {
            // 소모품=사용, 장비=장착, 판매는 모든 항목(장착품은 표시에서 이미 제외). 해제는 장비창에서만.
            bool canUse = item.Category == ItemCategory.Consumable;
            bool canEquip = item.Category == ItemCategory.Equipment;
            await _actionPanel.OpenAsync(ResolveCanvas(), slotRect, panel => panel.Bind(
                item.ItemId,
                id => _model.Accept(new InventoryIntent.UseItem(id)),
                id => _model.Accept(new InventoryIntent.EquipItem(id)),
                null,
                _ => ShowSellConfirmAsync(item).Forget(),
                canUse,
                canEquip,
                canUnequip: false,
                canSell: true));
        }

        /// <summary>판매 버튼 → 서버 sell_price 확인 팝업. 확인=판매 인텐트.</summary>
        private async UniTaskVoid ShowSellConfirmAsync(InventoryItemModel item)
        {
            long price = await _model.GetSellPriceAsync(item.ItemId);

            var canvas = ResolveCanvas();
            if (canvas == null) return;

            var inst = await AddressableLoader.LoadAndInstantiateAsync(
                AddressKeys.UI.ConfirmPopup, canvas.transform, destroyCancellationToken);
            if (inst == null) return;

            var popup = inst.GameObject.GetComponent<ConfirmPopup>();
            if (popup == null) { inst.Dispose(); return; }

            popup.SetAddressableOwner(inst);
            popup.Setup("판매",
                $"{item.DisplayName}을(를) {price} 골드에 판매하시겠습니까?",
                onConfirm: () => _model.Accept(new InventoryIntent.SellItem(item.ItemId)),
                onCancel: null,
                glow: PopupGlowType.Warning);
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

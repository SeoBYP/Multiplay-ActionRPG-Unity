using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Presentation.InGame;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 인게임 HUD View.
    ///
    /// MVI 규칙:
    ///   - State를 받아 UI를 렌더링한다.
    ///   - 사용자 입력을 Intent로 변환해 Model에 전달한다.
    ///   - Service / Repository 직접 호출 없음.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        private enum SideButtonType
        {
            Inventory,
            Ability,
            Smithy,
            Quest,
            Shop,
            Setting
        }
        
        [Serializable]
        private class SideButton
        {
            public SideButtonType type;
            public Button button;
        }
        
        [Inject] private InGameModel _model;

        [SerializeField] private Button returnToLobbyButton;

        [Header("Dungeon Result")]
        [Tooltip("던전 클리어(몬스터 전멸) 결과 패널. 미할당이어도 동작은 무해(토글만 생략).")]
        [SerializeField] private DungeonClear dungeonClearView;
        [Tooltip("던전 실패(참가자 전원 다운) 패널. 미할당이어도 동작은 무해(토글만 생략).")]
        [SerializeField] private DungeonFailed dungeonFailedView;
        [Tooltip("던전 클리어 후 결과 패널을 띄우기까지 지연(초). 그 사이 막타 드랍을 주울 수 있다. 패널은 입력을 막지 않는다.")]
        [SerializeField] private float dungeonClearPanelDelaySeconds = 4f;

        [Header("Side Buttons")]
        [SerializeField] private SideButton[] sideButtons;
        
        [Header("Buff Slot Container")]
        [SerializeField] private LayoutGroup buffSlotContainer;
        [SerializeField] private BattleEffectSlot buffSlotPrefab;

        private readonly List<BattleEffectSlot> _buffSlots = new List<BattleEffectSlot>();
        private bool _buffPoolInitialized;
        private bool _clearShowScheduled;

        [Header("Quick Buttons")]
        [SerializeField] private QuickButtonSlot[] quickButtons;
        
        [Header("Player Status")]
        [SerializeField] private SliderBall hpSlider;
        [SerializeField] private SliderBall mpSlider;
        [SerializeField] private Slider expSlider;
        [SerializeField] private TextMeshProUGUI expValue; // {현재 경험치}/{다음 레벨업에 필요한 경험치}
        
        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            sideButtons = new SideButton[]
            {
                new SideButton { type = SideButtonType.Inventory, button = this.FindChildComponentByName<Button>("btn_Inventory") },
                new SideButton { type = SideButtonType.Ability, button = this.FindChildComponentByName<Button>("btn_Ability") },
                new SideButton { type = SideButtonType.Smithy, button = this.FindChildComponentByName<Button>("btn_Smithy") },
                new SideButton { type = SideButtonType.Quest, button = this.FindChildComponentByName<Button>("btn_Quest") },
                new SideButton { type = SideButtonType.Shop, button = this.FindChildComponentByName<Button>("btn_Shop") },
                new SideButton { type = SideButtonType.Setting, button = this.FindChildComponentByName<Button>("btn_Setting") },
            };
            
            buffSlotContainer = this.FindChildComponentByPath<LayoutGroup>("window_actionbar/BuffSlots");
            
            quickButtons = this.GetComponentsInChildren<QuickButtonSlot>(true);
            
            hpSlider = this.FindChildComponentByName<SliderBall>("HP_Ball");
            mpSlider = this.FindChildComponentByName<SliderBall>("MP_Ball");
            
            expSlider = this.FindChildComponentByName<Slider>("expSlider");
            expValue = this.FindChildComponentByName<TextMeshProUGUI>("expValue");
        }
        
        private void Start()
        {
            returnToLobbyButton.onClick.AddListener(OnClickReturnToLobby);

            // 결과 패널의 자체 return 버튼도 같은 복귀 흐름으로 연결.
            if (dungeonClearView != null) dungeonClearView.Bind(OnClickReturnToLobby);
            if (dungeonFailedView != null) dungeonFailedView.Bind(OnClickReturnToLobby);

            // 사이드버튼 배선 — 현재는 Inventory만 연동(나머지 Ability/Shop 등은 후속).
            // 버튼 클릭 → InGameModel 토글 신호 → InventoryViewController가 창 로드/토글(I키와 동일 funnel).
            BindSideButton(SideButtonType.Inventory, () => _model.Accept(InGameIntent.ToggleInventory.Instance));

            InitBuffPool();

            _model.State
                .Subscribe(Render)
                .AddTo(destroyCancellationToken);
        }

        /// <summary>컨테이너에 미리 배치된 슬롯이 있으면 풀로 흡수하고 모두 숨긴다.</summary>
        private void InitBuffPool()
        {
            if (_buffPoolInitialized || buffSlotContainer == null)
                return;

            buffSlotContainer.GetComponentsInChildren(true, _buffSlots);
            foreach (var slot in _buffSlots)
                slot.Hide();
            _buffPoolInitialized = true;
        }

        private void Update()
        {
            // I키 인벤토리 토글 — .inputactions 의 Inventory 액션 C# 래퍼가 아직 미생성이라
            // 임시로 Keyboard 를 직접 폴링(HUD 버튼과 동일 funnel). 래퍼 재생성 후 InputRouter 경로로 이관 예정.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (_model == null || kb == null)
                return;

            // I키 = 인벤토리+장비 쌍 토글, K키 = 장비창 단독 토글(.inputactions Equipment=K). 임시 폴링(래퍼 이관 예정).
            if (kb.iKey.wasPressedThisFrame)
                _model.Accept(InGameIntent.ToggleInventory.Instance);
            if (kb.kKey.wasPressedThisFrame)
                _model.Accept(InGameIntent.ToggleEquipment.Instance);
        }

        private void OnClickReturnToLobby()
        {
            _model.Accept(InGameIntent.ReturnToLobby.Instance);
        }

        /// <summary>sideButtons 배열에서 해당 타입의 버튼을 찾아 클릭 핸들러를 연결한다.</summary>
        private void BindSideButton(SideButtonType type, global::System.Action onClick)
        {
            if (sideButtons == null) return;
            foreach (var sb in sideButtons)
            {
                if (sb != null && sb.type == type && sb.button != null)
                {
                    sb.button.onClick.AddListener(() => onClick());
                    return;
                }
            }
        }

        private void Render(InGameState state)
        {
            // 복귀 처리 중에는 버튼 비활성화 (중복 클릭 방지)
            returnToLobbyButton.interactable = !state.IsReturning;

            // 던전 클리어(몬스터 전멸) → 결과 패널을 '지연' 표시(그 사이 막타 드랍 줍기 가능, 입력은 안 막음).
            // 상태(IsDungeonCleared)는 즉시 true지만 패널 SetActive만 dungeonClearPanelDelaySeconds 만큼 늦춘다.
            if (dungeonClearView != null)
            {
                if (state.IsDungeonCleared)
                {
                    if (!_clearShowScheduled)
                    {
                        _clearShowScheduled = true;
                        ShowDungeonClearAfterDelay(state.RewardExp).Forget();
                    }
                }
                else
                {
                    _clearShowScheduled = false;
                    if (dungeonClearView.gameObject.activeSelf)
                        dungeonClearView.gameObject.SetActive(false);
                }
            }

            // 던전 실패(참가자 전원 다운) → 실패 패널 표시.
            if (dungeonFailedView != null && dungeonFailedView.gameObject.activeSelf != state.IsDungeonFailed)
                dungeonFailedView.gameObject.SetActive(state.IsDungeonFailed);

            // 로컬 플레이어 스탯 → 게이지 (GAS Attribute에서 중계된 값)
            if (hpSlider != null)
                hpSlider.SetValue(state.Hp, state.MaxHp);
            if (mpSlider != null)
                mpSlider.SetValue(state.Mp, state.MaxMp);

            RenderExp(state);
            RenderBuffs(state.Buffs);
        }

        /// <summary>
        /// 클리어 결과 패널을 dungeonClearPanelDelaySeconds 뒤에 표시한다.
        /// 지연 동안 플레이어는 막타로 떨어진 드랍을 줍고 자유롭게 이동할 수 있다(패널은 모달이 아님).
        /// 지연 중 상태가 클리어 해제(복귀 등)되면 _clearShowScheduled가 false로 풀려 표시를 건너뛴다.
        /// </summary>
        private async UniTaskVoid ShowDungeonClearAfterDelay(long rewardExp)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(dungeonClearPanelDelaySeconds),
                    cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (dungeonClearView == null || !_clearShowScheduled)
                return;

            dungeonClearView.SetReward(rewardExp);
            dungeonClearView.gameObject.SetActive(true);
        }

        /// <summary>진행(레벨/Exp) → exp 게이지 + 텍스트. 서버 권위 GetProgression 중계값(InGameModel). 만렙이면 MAX.</summary>
        private void RenderExp(InGameState state)
        {
            bool isMax = state.ExpToNext <= 0;
            if (expSlider != null)
            {
                // 표시 전용 게이지 — SetValueWithoutNotify 로 onValueChanged 발동을 막는다.
                // (값을 받아 텍스트를 쓰는 용도라 콜백 불필요. 프리팹에 잘못 연결된 리스너가 있어도 안전.)
                float fill = isMax ? 1f : Mathf.Clamp01((float)((double)state.Exp / state.ExpToNext));
                expSlider.SetValueWithoutNotify(fill);
            }
            if (expValue != null)
                expValue.text = isMax ? "MAX" : $"{state.Exp}/{state.ExpToNext}";
        }

        /// <summary>활성 버프 목록을 슬롯 풀에 바인딩. 부족하면 prefab으로 확장, 남으면 숨김.</summary>
        private void RenderBuffs(IReadOnlyList<BuffView> buffs)
        {
            if (buffSlotContainer == null)
                return;

            while (_buffSlots.Count < buffs.Count && buffSlotPrefab != null)
                _buffSlots.Add(Instantiate(buffSlotPrefab, buffSlotContainer.transform));

            for (int i = 0; i < _buffSlots.Count; i++)
            {
                if (i < buffs.Count)
                    _buffSlots[i].Bind(buffs[i]);
                else
                    _buffSlots[i].Hide();
            }
        }
    }
}
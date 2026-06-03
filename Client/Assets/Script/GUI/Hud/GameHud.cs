using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Presentation.InGame;
using R3;
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

        [Header("Side Buttons")]
        [SerializeField] private SideButton[] sideButtons;
        
        [Header("Buff Slot Container")]
        [SerializeField] private LayoutGroup buffSlotContainer;
        [SerializeField] private BattleEffectSlot buffSlotPrefab;

        private readonly List<BattleEffectSlot> _buffSlots = new List<BattleEffectSlot>();
        private bool _buffPoolInitialized;

        [Header("Quick Buttons")]
        [SerializeField] private QuickButtonSlot[] quickButtons;
        
        [Header("Player Status")]
        [SerializeField] private SliderBall hpSlider;
        [SerializeField] private SliderBall mpSlider;
        
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
        }
        
        private void Start()
        {
            returnToLobbyButton.onClick.AddListener(OnClickReturnToLobby);

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

        private void OnClickReturnToLobby()
        {
            _model.Accept(InGameIntent.ReturnToLobby.Instance);
        }

        private void Render(InGameState state)
        {
            // 복귀 처리 중에는 버튼 비활성화 (중복 클릭 방지)
            returnToLobbyButton.interactable = !state.IsReturning;

            // 로컬 플레이어 스탯 → 게이지 (GAS Attribute에서 중계된 값)
            if (hpSlider != null)
                hpSlider.SetValue(state.Hp, state.MaxHp);
            if (mpSlider != null)
                mpSlider.SetValue(state.Mp, state.MaxMp);

            RenderBuffs(state.Buffs);
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
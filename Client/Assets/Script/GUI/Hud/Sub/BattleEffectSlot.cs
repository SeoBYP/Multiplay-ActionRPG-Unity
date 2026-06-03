using Game.Presentation.InGame;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GUI.OutGame
{
    /// <summary>
    /// 스킬·아이템 등의 효과로 얻은 버프/디버프의 지속 시간을 표시하는 Slot.
    /// 남은시간 카운트다운은 슬롯이 로컬에서 처리한다 (State는 추가/제거 시에만 발행).
    /// </summary>
    public class BattleEffectSlot : MonoBehaviour
    {
        [SerializeField] private Image buffIcon;
        [SerializeField] private TextMeshProUGUI buffDuration;
        // TODO : Tooltip 연동 관련 Event Trigger
        [SerializeField] private EventTrigger eventTrigger;

        private float _endTime;
        private bool _infinite;
        private bool _active;

        /// <summary>현재 표시 중 남은 초(검증·디버깅용).</summary>
        public float RemainingSeconds => _infinite ? 0f : Mathf.Max(0f, _endTime - Time.time);
        public bool IsActive => _active;

        public void Bind(BuffView buff)
        {
            if (buffIcon != null)
            {
                buffIcon.sprite = buff.Icon;
                buffIcon.color = buff.Tint;
                buffIcon.enabled = buff.Icon != null;
            }

            _infinite = buff.IsInfinite;
            _endTime = Time.time + buff.RemainingSeconds;
            _active = true;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            UpdateDurationText();
        }

        public void Hide()
        {
            _active = false;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_active)
                UpdateDurationText();
        }

        private void UpdateDurationText()
        {
            if (buffDuration == null)
                return;

            if (_infinite)
            {
                buffDuration.text = string.Empty;
                return;
            }

            float remaining = Mathf.Max(0f, _endTime - Time.time);
            buffDuration.text = Mathf.CeilToInt(remaining).ToString();
        }
    }
}

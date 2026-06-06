using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI
{
    /// <summary>
    /// 던전 클리어 결과 패널 View. GameHud(InGameModel 구독)가 활성/보상값을 주입한다.
    /// 자체로는 System/Network 를 모른다 — primitive(long)와 콜백만 받는다(MVI 레이어 규칙).
    /// </summary>
    public class DungeonClear : MonoBehaviour
    {
        [SerializeField] private Button returnButton;
        [SerializeField] private TextMeshProUGUI expGainText;

        private Action _onReturn;

        private void Awake()
        {
            if (returnButton != null)
                returnButton.onClick.AddListener(() => _onReturn?.Invoke());
        }

        /// <summary>return 버튼 클릭 시 호출할 콜백을 1회 연결한다(GameHud → ReturnToLobby).</summary>
        public void Bind(Action onReturn) => _onReturn = onReturn;

        /// <summary>클리어 보상 Exp 표시.</summary>
        public void SetReward(long rewardExp)
        {
            if (expGainText != null)
                expGainText.text = $"+{rewardExp} EXP";
        }
    }
}

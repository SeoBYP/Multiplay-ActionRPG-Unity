using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI
{
    /// <summary>
    /// 던전 실패(참가자 전원 다운) 패널 View. GameHud(InGameModel 구독)가 활성/콜백을 주입한다.
    /// 실패는 보상이 없다 — return 버튼만.
    /// </summary>
    public class DungeonFailed : MonoBehaviour
    {
        
        [SerializeField] private Button returnButton;

        private Action _onReturn;

        private void Awake()
        {
            if (returnButton != null)
                returnButton.onClick.AddListener(() => _onReturn?.Invoke());
        }

        /// <summary>return 버튼 클릭 시 호출할 콜백을 1회 연결한다(GameHud → ReturnToLobby).</summary>
        public void Bind(Action onReturn) => _onReturn = onReturn;
    }
}

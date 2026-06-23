using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Quest
{
    /// <summary>퀘스트 목록 한 줄(고정 풀). 이름 + 선택 토글 + 완료(수령) 체크마크. Quest View 가 Bind.</summary>
    public class QuestSlot : MonoBehaviour
    {
        [SerializeField] private Toggle questToggle;
        [SerializeField] private Image questCheckMark;
        [SerializeField] private Image questIcon;
        [SerializeField] private TextMeshProUGUI questName;

        /// <summary>한 퀘스트를 표시·선택 콜백 연결. selected=현재 선택 / claimed=수령완료(체크마크).</summary>
        public void Bind(string displayName, bool selected, bool claimed, Action onSelect)
        {
            if (questName != null) questName.text = displayName;
            if (questCheckMark != null) questCheckMark.gameObject.SetActive(claimed);

            if (questToggle != null)
            {
                questToggle.onValueChanged.RemoveAllListeners();
                questToggle.SetIsOnWithoutNotify(selected);
                questToggle.onValueChanged.AddListener(isOn => { if (isOn) onSelect?.Invoke(); });
            }
        }
    }
}

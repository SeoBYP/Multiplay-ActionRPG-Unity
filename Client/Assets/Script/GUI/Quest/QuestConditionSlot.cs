using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Quest
{
    /// <summary>선택 퀘스트의 목표 조건 한 줄. 텍스트 + 충족 체크마크. Quest View 가 Bind.</summary>
    public class QuestConditionSlot : MonoBehaviour
    {
        [SerializeField] private Image conditionCheckMark;
        [SerializeField] private Image conditionBackground;
        [SerializeField] private TextMeshProUGUI conditionText;

        public void Bind(string text, bool met)
        {
            if (conditionText != null) conditionText.text = text;
            if (conditionCheckMark != null) conditionCheckMark.gameObject.SetActive(met);
        }
    }
}

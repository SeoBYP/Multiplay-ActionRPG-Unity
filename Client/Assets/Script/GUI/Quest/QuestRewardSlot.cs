using TMPro;
using UnityEngine;

namespace Game.GUI.Quest
{
    /// <summary>선택 퀘스트의 보상 한 항목(예: "경험치 50"). Quest View 가 Bind.</summary>
    public class QuestRewardSlot : MonoBehaviour
    {
        [SerializeField] private Transform rewardSlotContainer;
        [SerializeField] private TextMeshProUGUI rewardText;

        public void Bind(string text)
        {
            if (rewardText != null) rewardText.text = text;
        }
    }
}

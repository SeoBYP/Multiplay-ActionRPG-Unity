using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GUI.OutGame
{
    public class QuickButtonSlot : MonoBehaviour
    {
        [Header("Contents Setting")]
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private TextMeshProUGUI itemCount;
        
        [Header("Slot Setting")]
        [SerializeField] private TextMeshProUGUI slotNumber;
        [SerializeField] private Button quickButton;

        [SerializeField] private EventTrigger tooltipButton;
        
        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            icon = this.FindChildComponentByName<Image>("icon");
            frame = this.FindChildComponentByName<Image>("item_slot");
            itemCount = this.FindChildComponentByName<TextMeshProUGUI>("item_count");
            
            slotNumber = this.FindChildComponentByName<TextMeshProUGUI>("slotNumber");
            quickButton = this.FindChildComponentByName<Button>("button_states");
            
            tooltipButton = this.FindChildComponentByName<EventTrigger>("button_states");
        }
    }
}
using Game.Presentation.GameScene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    public class Loading : MonoBehaviour, ILoadingView
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI text;
        
        public void SetProgress(float progress)
        {
            slider.value = progress;
            text.text = $"로딩중 {(int)progress}%";
        }

        public void SetMessage(string message)
        {
            if (text != null) text.text = message;
        }
    }
}
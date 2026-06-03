using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.GUI.OutGame
{
    public class SliderBall : MonoBehaviour
    {
        [SerializeField] private Image mask;
        [SerializeField] private GameObject fillEnding;
        [SerializeField] private TextMeshProUGUI amountText;
        RectTransform ballFillTopTransform;

        float ballFill;
        Vector3 ballFillTopPosition = new Vector3 ();
        float ballFillX;

        [InspectorButton("Quick Setting")]
        private void QuickSetting()
        {
            mask = this.FindChildComponentByPath<Image>("ball");
            fillEnding = this.FindChildComponentByPath<Transform>("ball_flow/fill").gameObject;
            amountText = this.FindChildComponentByName<TextMeshProUGUI>("amountText");
        }

        /// <summary>현재 게이지 채움량(0~1). 렌더 결과 검증·디버깅용 읽기 접근자.</summary>
        public float FillAmount => mask != null ? mask.fillAmount : 0f;

        /// <summary>현재/최대값으로 게이지 채움량과 수치 텍스트를 갱신한다.</summary>
        public void SetValue(int current, int max)
        {
            if (mask != null)
                mask.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            if (amountText != null)
                amountText.text = $"{current}/{max}";
        }

        void Start () {
            ballFill = mask.fillAmount;
            ballFillTopTransform = fillEnding.GetComponent<Image> ().rectTransform;
        }
		
        void LateUpdate () {
            ballFill = mask.fillAmount;

            ballFillX += 1f;
            if (ballFillX >= 100f) {
                ballFillX = 0f;
            }

            ballFillTopPosition.Set(ballFillX, 172f * ballFill, 0);
            ballFillTopTransform.localPosition = ballFillTopPosition;
        }
    }
}
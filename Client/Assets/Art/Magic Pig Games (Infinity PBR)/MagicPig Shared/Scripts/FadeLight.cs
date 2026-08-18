using System;
using System.Collections;
using UnityEngine;

/*
 * Attach this to a light, or assign a light to it. It will capture the start intensity, and then will fade to the final
 * intensity over the time. Final intensity is determined by the right side of the curve multiplied by the start intensity.
 */

namespace MagicPigGames.Shared
{
    [Documentation("Attach this to a light, or assign a light to it. It will capture the start intensity, " +
                   "and then will fade to the final intensity over the time. Final intensity is determined by the " +
                   "right side of the curve multiplied by the start intensity.")]
    [Serializable]
    public class FadeLight : MagicPigMonoBehaviour
    {
        [Tooltip("The light to fade.")] public Light thisLight;

        [Tooltip("Time to fade to the final intensity.")]
        public float fadeTime = 0.2f;

        [Tooltip("The curve used to fade the light.")]
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Tooltip("When true, the fade will start when the object is enabled.")]
        public bool fadeOnEnable = true;

        private float _startIntensity = -1;

        private void OnEnable()
        {
            if (thisLight == null)
                thisLight = GetComponent<Light>();

            // Start Intensity may already have been captured, in which case, we likely got taken from a pool.
            if (_startIntensity < 0)
                _startIntensity = thisLight.intensity;
            thisLight.intensity = _startIntensity;
            if (fadeOnEnable)
                StartFade();
        }

        public void StartFade()
        {
            StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            var t = 0f;
            var startIntensity = _startIntensity;

            while (t < fadeTime)
            {
                t += Time.deltaTime;
                var curveValue = fadeCurve.Evaluate(t / fadeTime);
                var targetIntensity = curveValue * startIntensity;
                thisLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t / fadeTime);
                yield return null;
            }
        }
    }
}
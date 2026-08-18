using System.Collections;
using UnityEngine;

namespace MagicPigGames.Shared
{
    [Documentation("Scale the object to a target scale when enabled.")]
    public class ScaleOnEnable : MagicPigMonoBehaviour
    {
        [Header("Setup")] public Vector3 targetScale = Vector3.one;

        public float secondsToScale = 0.2f;

        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;

        private void OnEnable()
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(Scale());
        }

        private void OnDisable()
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            transform.localScale = _originalScale;
        }

        private IEnumerator Scale()
        {
            _originalScale = transform.localScale;
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime / secondsToScale;
                transform.localScale = Vector3.Lerp(_originalScale, targetScale, t);
                yield return null;
            }
        }
    }
}
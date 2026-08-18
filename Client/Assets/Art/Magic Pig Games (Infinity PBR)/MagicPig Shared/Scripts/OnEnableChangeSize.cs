using System;
using System.Collections;
using UnityEngine;

// Use this to change the size of an object when it is enabled. This is useful for projectiles that are large,
// but need to be small when they are launched from a smaller object. They can quickly grow in size and appear
// more natural.

namespace MagicPigGames.Shared
{
    [Documentation("Change the size of an object when it is enabled.")]
    [Serializable]
    public class OnEnableChangeSize : MagicPigMonoBehaviour
    {
        [Tooltip("The desired size of the object.")]
        public float desiredSize = 5f;

        [Tooltip("The time it takes to change the size.")]
        public float timeToChangeSize = 0.25f;

        [Tooltip("If true, the object will reset to its initial size when disabled.")]
        public bool resetOnDisable = true;

        [Tooltip("The delay before the size change starts.")]
        public float delay;

        private Coroutine _changeSizeCoroutine;

        private Vector3 _initialScale;

        private void OnEnable()
        {
            _initialScale = transform.localScale;
            _changeSizeCoroutine = StartCoroutine(ChangeSize());
        }

        private void OnDisable()
        {
            if (_changeSizeCoroutine != null)
                StopCoroutine(_changeSizeCoroutine);
            if (resetOnDisable)
                transform.localScale = _initialScale;
        }

        private IEnumerator ChangeSize()
        {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            float time = 0;
            while (time < timeToChangeSize)
            {
                time += Time.deltaTime;
                transform.localScale = Vector3.Lerp(_initialScale, Vector3.one * desiredSize, time / timeToChangeSize);
                yield return null;
            }
        }
    }
}
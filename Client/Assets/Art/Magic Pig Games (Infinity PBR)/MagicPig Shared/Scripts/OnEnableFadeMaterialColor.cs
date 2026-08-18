using System;
using System.Collections;
using UnityEngine;

// Fade the material color when the object is enabled.

namespace MagicPigGames.Shared
{
    [Documentation("Fade the material color when the object is enabled.")]
    [Serializable]
    public class OnEnableFadeMaterialColor : MagicPigMonoBehaviour
    {
        public Color desiredColor;
        public float timeToChangeColor = 0.25f;
        public bool resetOnDisable = true;
        public float delay;
        private Coroutine _changeColorCoroutine;
        private Color _initialColor;

        private Material _material;

        private void OnEnable()
        {
            if (_material == null)
                _material = GetComponent<Renderer>().material;
            _initialColor = _material.color;
            _changeColorCoroutine = StartCoroutine(ChangeColor());
        }

        private void OnDisable()
        {
            if (_changeColorCoroutine != null)
                StopCoroutine(_changeColorCoroutine);
            if (resetOnDisable)
                _material.color = _initialColor;
        }

        private IEnumerator ChangeColor()
        {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            float time = 0;
            while (time < timeToChangeColor)
            {
                time += Time.deltaTime;
                _material.color = Color.Lerp(_initialColor, desiredColor, time / timeToChangeColor);
                yield return null;
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InfinityPBR.Demo
{
    public class InfinityDemoAnimationButton : MonoBehaviour, IPointerClickHandler
    {
        public Text text;

        private Animator _animator;
        private string _trigger;

        public void Setup(string trigger, Animator animator)
        {
            _trigger = trigger;
            text.text = _trigger;
            name = _trigger;
            _animator = animator;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _animator.SetTrigger(_trigger);
        }
    }
    
}

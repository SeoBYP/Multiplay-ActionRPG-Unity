using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.GUI.Common
{
    public class BasePopup : UIBehaviour
    {
        [SerializeField] private Image backgroundImage; // 딤 오버레이 — 클릭 시 닫기
        [SerializeField] private Image glowImage;       // 루트 glow 이미지
        [SerializeField] private Sprite[] glowSprites;  // [0]=Info(Blue) [1]=Success(Green) [2]=Warning(Yellow) [3]=Danger(Red)

        private AddressableInstance _owner;

        protected override void Awake()
        {
            base.Awake();

            if (backgroundImage != null)
            {
                var trigger = backgroundImage.gameObject.AddComponent<EventTrigger>();
                var entry   = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(_ => Close());
                trigger.triggers.Add(entry);
            }
        }

        public void SetAddressableOwner(AddressableInstance inst) => _owner = inst;

        protected void ApplyGlow(PopupGlowType type)
        {
            if (glowImage == null) return;
            var idx = (int)type;
            if (glowSprites != null && idx < glowSprites.Length && glowSprites[idx] != null)
                glowImage.sprite = glowSprites[idx];
        }

        public virtual void Close()
        {
            if (_owner != null)
            {
                _owner.Dispose();
                _owner = null;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}

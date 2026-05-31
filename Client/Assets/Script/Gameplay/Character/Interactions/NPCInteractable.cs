using UnityEngine;
using UnityEngine.Events;

namespace Game.Gameplay.Character
{
    public class NPCInteractable : MonoBehaviour, IActiveInteractable
    {
        [SerializeField]
        private float m_delayBetweenInteractions = 3;
        private float m_currentDelay = 0;
        
        public bool IsInteractionActive { get; private set; } = true;
        
        public event UnityAction OnWaveInput;
        
        public void Interact(GameObject interactor)
        {
            if (IsInteractionActive == false)
            {
                return;
            }

            OnWaveInput?.Invoke();
            m_currentDelay = m_delayBetweenInteractions;
            IsInteractionActive = false;
        }

        private void Update()
        {
            if (IsInteractionActive == false)
            {
                if (m_currentDelay > 0)
                {
                    m_currentDelay -= Time.deltaTime;
                    return;
                }
                IsInteractionActive = true;
            }
        }
    }
}
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class InteractionDetector : MonoBehaviour
    {
        [SerializeField] private float m_detectionRange = 1.0f;
        [SerializeField] private float m_detectionRadius = 0.5f;
        [SerializeField] private float m_height = 1.0f;
        [SerializeField] private LayerMask m_detectionLayer;

        public IInteractable CurrentInteractable { get; private set; }
        private Highlight m_currentHighlight;

        private readonly Collider[] _results = new Collider[10];
        
        public void DetectInteractable()
        {
            var center = transform.position + Vector3.up * m_height + transform.forward * m_detectionRange;
            var size = Physics.OverlapSphereNonAlloc(center, m_detectionRadius, _results, m_detectionLayer);

            IInteractable foundInteractable = null;
            Highlight foundHighlight = null;

            for (int i = 0; i < size; i++)
            {
                var collider = _results[i];
                if (collider == null)
                    continue;

                var interactable = collider.GetComponent<IInteractable>();
                if (interactable == null)
                    continue;

                if (interactable is IActiveInteractable active && !active.IsInteractionActive)
                    continue;

                foundInteractable = interactable;
                foundHighlight = collider.GetComponent<Highlight>();
                break;
            }

            if (foundInteractable != CurrentInteractable)
            {
                ClearCurrentInteractable();

                CurrentInteractable = foundInteractable;
                m_currentHighlight = foundHighlight;
                m_currentHighlight?.EnableHighlight();

                if (CurrentInteractable != null)
                    Debug.Log($"New interactable detected: {CurrentInteractable}");
            }

            if (foundInteractable == null)
            {
                ClearCurrentInteractable();
            }
        }


        private void ClearCurrentInteractable()
        {
            m_currentHighlight?.DisableHighlight();
            m_currentHighlight = null;
            CurrentInteractable = null;
        }

        private void OnDrawGizmosSelected()
        {
            // Draw the starting point of the sphere cast
            Gizmos.color = Color.blue;
            if (CurrentInteractable != null)
            {
                Gizmos.color = Color.green;
            }

            Vector3 center = transform.position + Vector3.up * m_height + transform.forward * m_detectionRange;
            Gizmos.DrawWireSphere(center, 0.5f);
        }
    }
}

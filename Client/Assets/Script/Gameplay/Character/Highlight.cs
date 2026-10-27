using UnityEngine;

namespace Game.Gameplay.Character
{
    public class Highlight : MonoBehaviour
    {
        [SerializeField]
        private Renderer m_objectRenderer;
        [SerializeField]
        private string m_outlineLayerName = "Outline";
        private int m_originalLayer;

        private void Awake()
        {
            m_originalLayer = m_objectRenderer.gameObject.layer;
        }

        public void EnableHighlight()
        {
            m_objectRenderer.gameObject.layer = LayerMask.NameToLayer(m_outlineLayerName);
        }

        public void DisableHighlight()
        {
            m_objectRenderer.gameObject.layer = m_originalLayer;
        }
    }
}
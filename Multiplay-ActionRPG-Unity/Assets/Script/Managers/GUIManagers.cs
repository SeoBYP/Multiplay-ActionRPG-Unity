using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Managers
{
    public class GUIManagers : PersistentSingleton<GUIManagers>
    {
        [SerializeField] private List<CanvasUIBehaviour> canvasUIBehaviours;
        
        protected override void OnInitializeSingleton()
        {
            canvasUIBehaviours = GetComponentsInChildren<CanvasUIBehaviour>().ToList();
        }

        public T Get<T>() where T : UIBehaviour
        {
            foreach (CanvasUIBehaviour canvasUIBehaviour in canvasUIBehaviours)
            {
                if (canvasUIBehaviour is T behaviour)
                {
                    return behaviour;
                }
            }
            return null;
        }
    }
}
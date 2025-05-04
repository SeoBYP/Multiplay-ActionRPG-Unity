using Unity.VisualScripting;
using UnityEngine;

namespace Game.Core
{
    public static class MonoBehaviourExtentions
    {
        public static T GetOrAddComponent<T>(this MonoBehaviour behaviour) where T : Component
        {
            T component = behaviour.GetComponent<T>();
            if (component.IsUnityNull())
            {
                component = behaviour.gameObject.AddComponent<T>();
            }
            return component;
        }
    }
}
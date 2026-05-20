using UnityEngine;

namespace Game.Core
{
    public static class MonoBehaviourExtentions
    {
        public static T GetOrAddComponent<T>(this MonoBehaviour behaviour) where T : Component
        {
            T component = behaviour.GetComponent<T>();
            if (component == null)
            {
                component = behaviour.gameObject.AddComponent<T>();
            }
            return component;
        }
        
        public static T GetAroundComponent<T>(this MonoBehaviour behaviour)
        {
            return GetAroundComponent<T>(behaviour.gameObject);
        }
        
        public static T GetAroundComponent<T>(this Transform behaviour)
        {
            return GetAroundComponent<T>(behaviour.gameObject);
        }
        
        public static T GetAroundComponent<T>(this GameObject behaviour) 
        {
            T component = behaviour.GetComponentInParent<T>();
            if (component == null)
                component = behaviour.GetComponentInChildren<T>();
            if (component == null)
                component = behaviour.gameObject.GetComponent<T>();
            return component;
        }
    }
}
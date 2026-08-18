using UnityEditor;
using UnityEngine;

namespace MagicPigGames
{
    public abstract class LazyScriptableObject<T> : ScriptableObject where T : LazyScriptableObject<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance) 
                    return _instance;
                
                _instance = Resources.Load<T>(typeof(T).Name);
                if (!_instance)
                    Debug.LogError($"{typeof(T).Name} not found. Ensure that a {typeof(T).Name} ScriptableObject exists within a Resources folder.");
                return _instance;
            }
        }

        protected virtual void OnValidate()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError($"Multiple instances of {typeof(T).Name} are not allowed. Using only the first instance.");
                DestroyImmediate(this);
            }

#if UNITY_EDITOR
            var assetPath = AssetDatabase.GetAssetPath(this);
            if (!assetPath.Contains("/Resources/"))
                Debug.LogError($"{this.name} is not in a Resources folder! Please move it into a Resources folder in your Unity project.");
#endif
        }
    }
}
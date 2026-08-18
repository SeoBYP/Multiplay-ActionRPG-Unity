using UnityEngine;

namespace MagicPigGames
{
    public abstract class LazySingleton<T> : MonoBehaviour where T : Component
    {
        private static T _instance;

        public static T Instance
        { 
            get 
            {
                if (_instance != null) 
                    return _instance;
                
                _instance = FindObjectOfType<T>();

                if (_instance != null) 
                    return _instance;
                
                var singletonObject = new GameObject();
                _instance = singletonObject.AddComponent<T>();
                DontDestroyOnLoad(singletonObject);
                return _instance;
            }
        }
        
        // Ensures no other components can create an instance
        protected LazySingleton() {} 
    }
}
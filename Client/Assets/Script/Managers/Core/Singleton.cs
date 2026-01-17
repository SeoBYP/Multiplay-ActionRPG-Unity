using Unity.VisualScripting;
using UnityEngine;

namespace Game.Managers
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public bool AutoUnparentOnAwake = true;

        private static T _instance;

        public static bool HasInstance => _instance != null;
        public static T TryGetInstance() => HasInstance ? _instance : null;

        public static T Instance
        {
            get
            {
                if (_instance.IsUnityNull())
                {
                    var obj = new GameObject(typeof(T).Name, typeof(T));
                    _instance = obj.GetOrAddComponent<T>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            InitializeSingleton();
        }

        private void InitializeSingleton()
        {
            if (!Application.isPlaying) return;

            if (AutoUnparentOnAwake)
            {
                transform.SetParent(null);
            }

            if (_instance == null)
            {
                _instance = this as T;
            }
            else
            {
                if (_instance != this)
                {
                    Destroy(gameObject);
                }
            }

            OnInitializeSingleton();
        }

        protected abstract void OnInitializeSingleton();
    }
}
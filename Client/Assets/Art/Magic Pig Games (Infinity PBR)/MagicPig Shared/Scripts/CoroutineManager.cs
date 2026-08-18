using System;
using System.Collections;
using UnityEngine;

/*
 * Instances of this are brought into the scene automatically. You don't need to bring this into the scene manually.
 * It is used to allow non-MonoBehaviours to start coroutines.
 */

namespace MagicPigGames.Shared
{
    [Serializable]
    public class CoroutineManager : MagicPigMonoBehaviour
    {
        private static CoroutineManager _instance;

        public static CoroutineManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var go = new GameObject("CoroutineManager");
                _instance = go.AddComponent<CoroutineManager>();
                return _instance;
            }
        }

        public Coroutine StartManagedCoroutine(IEnumerator coroutine)
        {
            var runningCoroutine = StartCoroutine(coroutine);
            return runningCoroutine;
        }
    }
}
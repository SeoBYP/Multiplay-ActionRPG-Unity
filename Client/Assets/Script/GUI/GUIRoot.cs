using System;
using UnityEngine;

namespace Game.GUI
{
    public class GUIRoot : MonoBehaviour
    {
        private static GUIRoot _instance;
        
        public static GUIRoot Instance => _instance;

        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
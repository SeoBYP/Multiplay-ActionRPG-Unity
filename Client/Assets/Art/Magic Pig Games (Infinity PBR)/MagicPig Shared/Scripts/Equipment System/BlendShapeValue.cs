using System.Collections.Generic;
using UnityEngine;

namespace InfinityPBR
{
    [System.Serializable]
    public class BlendShapeValue
    {
        public int objectIndex;
        public int index;
        public bool display = false;
        public string fullName;
        public string triggerName;
        public bool isMinus = false;

        public float value = 0f;
        public float min = 0f;
        public float max = 100f;

        [HideInInspector] public float limitMin = 0f;
        [HideInInspector] public float limitMax = 100f;

        [HideInInspector] public float lastValue = 0f;
        [HideInInspector] public bool showValueOptions = false;
        [HideInInspector] public bool isOpen = false;

        public float FromValue { get; set; }
        public float ToValue { get; set; }
        

        // If this is being controlled by another shape, set these
        public int matchThisObjectIndex = 0;
        public int matchThisValueIndex = 0;
        public bool matchAnotherValue = false;
        
        // Shapes that this value controls will go here.
        public List<MatchValue> otherValuesMatchThis = new List<MatchValue>();
    }
}
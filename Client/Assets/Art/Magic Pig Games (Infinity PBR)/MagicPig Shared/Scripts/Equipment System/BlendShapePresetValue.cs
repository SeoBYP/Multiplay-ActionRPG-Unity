using UnityEngine;

namespace InfinityPBR
{
    [System.Serializable]
    public class BlendShapePresetValue
    {
        public string objectName;
        public string valueTriggerName;
        public string onTriggerMode;
        [HideInInspector] public int onTriggerModeIndex = 0;
        public float shapeValue;

        public float limitMin;
        public float limitMax;
        public float min;
        public float max;
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace InfinityPBR
{
    [System.Serializable]
    public class BlendShapePreset
    {
        public string name;
        public List<BlendShapePresetValue> presetValues = new List<BlendShapePresetValue>();
        [HideInInspector] public bool showValues = false;
        
        // Add new BlendShapePresetValue to the list from a BlendShapeValue
        public void AddPresetValue(BlendShapeValue blendShapeValue)
        {
            var presetValue = new BlendShapePresetValue
            {
                shapeValue = blendShapeValue.value
            };
            presetValues.Add(presetValue);
        }
    }
}
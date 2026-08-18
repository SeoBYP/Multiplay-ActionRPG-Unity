using System.Collections.Generic;
using UnityEngine;

namespace InfinityPBR
{
    [System.Serializable]
    public class BlendShapeGameObject
    {
        public string gameObjectName;
        public GameObject gameObject;
        public SkinnedMeshRenderer smr;
        public int displayableValues = 0;
        public List<BlendShapeValue> blendShapeValues = new List<BlendShapeValue>();
        [HideInInspector] public bool showValues = false;
    }
}
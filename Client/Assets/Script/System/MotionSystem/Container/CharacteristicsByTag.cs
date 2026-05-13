using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class CharacteristicsByTag
    {
        public string id;
        public List<BoneCharacteristic> characteristics;
        [Range(0f, 1f)]
        public float weightFutureOffset = 1;
        [Range(0f, 1f)]
        public float weightFutureDirection = 1;
        [Range(0f, 1f)]
        public float weightPastOffset = 1;
        [Range(0f, 1f)]
        public float weightPastDirection = 1;
    }
}
using System;
using UnityEngine;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class BoneCharacteristic
    {
        public AvatarBone bone;
        [Range(0f, 1f)]
        public float weightPosition = 1;
        [Range(0f, 1f)]
        public float weightVelocity = 1;
    }
}
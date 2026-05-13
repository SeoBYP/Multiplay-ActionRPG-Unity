using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class ActionTag : TagBase
    {
        public bool hasRecoveryState;
        public bool hasInitState;

        [HideInInspector] public int[] animationIDSerialized = new int[3];

        //Interruptions
        public bool[] isInterruptibleByState = new bool[3];
        public InterruptibleBy interruptibleType = InterruptibleBy.None;
        public List<string> allowedInterruptionNames = new List<string>();

        //Warping properties
        public WarpingType warpingType;
        public WarpingMode posWarpingMode;
        public WarpingMode rotWarpingMode;

        [Range(0, 1)] public float positionWarpWeight = 0.0f;
        [Range(0, 1)] public float rotationWarpWeight = 0.0f;

        //Custom curves
        [HideInInspector] public AnimationCurve customWarpPositionCurve;
        [HideInInspector] public AnimationCurve customWarpRotationCurve;

        public bool contactWarping;
        public List<AvatarBone> warpContactBones;
        
        [HideInInspector] public bool simulateRootMotion; //Only used on loop actions

        //By inheritance, it has ranges property (each range has one anim) //0 - Init, 1 - Action, 2 - Recovery

        public ActionTag(string name) : base(name)
        {
        }

        public ActionTag(string name, bool hasInitState = false, bool hasRecoveryState = false) : base(name)
        {
            //Let's assume every state is stored inside a range

            this.hasInitState = hasInitState;
            this.hasRecoveryState = hasRecoveryState;
        }

        public ActionTag(Tag so, bool hasInitState = false, bool hasRecoveryState = false) : base(so)
        {
            //Let's assume every state is stored inside a range

            this.hasInitState = hasInitState;
            this.hasRecoveryState = hasRecoveryState;
        }

        public bool HasInitState()
        {
            return hasInitState;
        }

        public bool HasRecoveryState()
        {
            return hasRecoveryState;
        }
    }
}
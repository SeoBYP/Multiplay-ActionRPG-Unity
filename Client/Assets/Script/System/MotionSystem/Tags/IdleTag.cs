using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class IdleTag : TagBase
    {
        public bool hasRecoveryState;
        public bool hasInitState;

        public List<TagRange> initRanges;
        public List<TagRange> loopRanges;

        [HideInInspector] public int[] transitionIDSerialized;
        [HideInInspector] public int[] loopIDSerialized;

        //By inheritance, it has ranges property (each range has one anim) //0 - Init, 1 - Action, 2 - Recovery

        public IdleTag(string name) : base(name)
        {
            hasInitState = true;
            hasRecoveryState = false;
        }

        public IdleTag(string name, bool hasTransition) : base(name)
        {
            hasInitState = hasTransition;
            hasRecoveryState = false;
        }

        public IdleTag(Tag so, List<TagRange> init, List<TagRange> loops) : base(so)
        {
            //Let's assume every state is stored inside a range
            hasInitState = true;
            hasRecoveryState = false;

            initRanges = init;
            loopRanges = loops;
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
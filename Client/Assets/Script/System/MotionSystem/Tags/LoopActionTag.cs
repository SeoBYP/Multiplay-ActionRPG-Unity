using System;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class LoopActionTag : ActionTag
    {
        public LoopActionTag(string name) : base(name)
        {
        }

        public LoopActionTag(string name, bool hasInitState = false, bool hasRecoveryState = false,
            bool simulate = false) : base(name, hasInitState, hasRecoveryState)
        {
            simulateRootMotion = simulate;
        }

        public LoopActionTag(Tag so, bool hasInitState = false, bool hasRecoveryState = false) : base(so)
        {
        }
    }

}
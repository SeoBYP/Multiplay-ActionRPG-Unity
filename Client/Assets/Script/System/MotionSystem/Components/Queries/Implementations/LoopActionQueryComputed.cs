using System;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class LoopActionQueryComputed : ActionQueryComputed
    {
        public LoopActionQueryComputed(ActionTag tagBase, int fEstimates, int pEstimates, int nBones) : base(tagBase, fEstimates, pEstimates, nBones)
        {
        }
    }
}

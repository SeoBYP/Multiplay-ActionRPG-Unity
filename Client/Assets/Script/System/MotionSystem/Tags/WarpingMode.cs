using System;

namespace Game.System.MotionSystem
{
    [Serializable]
    public enum WarpingMode
    {
        None = 0,
        Linear = 1,
        Quadratic = 2,
        Exponential = 3,
        DecayLogarithmic = 4,
        Custom = 5,
        Dynamic = 6
    }
}
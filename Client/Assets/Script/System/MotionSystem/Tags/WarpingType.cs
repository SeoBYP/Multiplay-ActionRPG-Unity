using System;

namespace Game.System.MotionSystem
{
    [Serializable]
    public enum WarpingType
    {
        None = 0,
        Position = 1,
        Rotation = 2,

        //Time = 4,
        PositionRotation = 3,
        // PositionTime = 5,
        // RotationTime = 6,
        // PositionRotationTime = 7
    }
}
using System;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class TagRange
    {
        public string animName;
        public int poseStart; //featureIDStart in QueryRange
        public int poseStop; //featureIDStop in QueryRange
        public int frameStart;
        public int frameStop;

        public TagRange()
        {
        }

        public TagRange(string name, int frameStart, int frameStop)
        {
            animName = name;
            this.frameStart = frameStart;
            this.frameStop = frameStop;
        }
    }
}
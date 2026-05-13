using System;
using System.Collections.Generic;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class TagBase
    {
        public string name;
        public List<TagRange> ranges;

        public TagBase(string name)
        {
            this.name = name;
            ranges = new List<TagRange>();
        }

        public TagBase(Tag so)
        {
            name = so.name;
            ranges = so.ranges;
        }

        public virtual bool IsDone()
        {
            return false;
        }
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    public class Tag : ScriptableObject
    {
        public List<TagRange> ranges;

        public Tag(string name)
        {
            this.name = name;
            ranges = new List<TagRange>();
        }

        public void Init(TagBase tb)
        {
            name = tb.name;
            ranges = tb.ranges;
        }
    }
}
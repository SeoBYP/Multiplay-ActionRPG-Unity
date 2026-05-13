using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    public class Tags : ScriptableObject
    {
        public List<TagBase> tags;
        public List<ActionTag> actionTags;
        public List<IdleTag> idleTags;
    }
}

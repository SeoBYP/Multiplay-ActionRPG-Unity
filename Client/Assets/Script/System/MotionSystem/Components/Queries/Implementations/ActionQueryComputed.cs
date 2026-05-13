using System;
using System.Collections.Generic;

namespace Game.System.MotionSystem
{
    [Serializable]
    public class ActionQueryComputed : QueryComputed
    {
        public ActionTag actionTag;
        public ActionQueryComputed(ActionTag tagBase, int fEstimates, int pEstimates, int nBones) : base(fEstimates, pEstimates, nBones)
        {
            actionTag = tagBase;
            ranges = CreateQueryRange(actionTag.ranges);
        }

        protected List<QueryRange> CreateQueryRange(List<TagRange> tagRanges)
        {
            List<QueryRange> newQueryRanges = new List<QueryRange>();
            foreach (var range in tagRanges)
            {
                QueryRange newRange = new QueryRange(range.poseStart, range.poseStop);
                newQueryRanges.Add(newRange);
            }

            return newQueryRanges;
        }
    }
}

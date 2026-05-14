using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    public enum MotionSearchDatabaseGroup
    {
        Dense,
        Sparse
    }

    public enum MotionSearchDatabaseRole
    {
        Idle,
        Loop,
        Start,
        Stop,
        Pivot,
        TurnInPlace,
        Arc,
        Circle,
        Transition
    }

    [CreateAssetMenu(menuName = "MotionMatching/Pose Search Database")]
    public class MotionSearchDatabaseAsset : ScriptableObject
    {
        public MotionSearchDatabaseGroup group;
        public MotionSearchDatabaseRole role;
        public bool includeInBake = true;
        public List<AnimationClip> animations = new();
    }

    [Serializable]
    public class MotionSearchDatabaseBakeRecord
    {
        public string name;
        public MotionSearchDatabaseGroup group;
        public MotionSearchDatabaseRole role;
        public List<int> animationIDs = new();
        public List<string> animationPaths = new();
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Dataset이 보유하는 모든 query group 목록입니다.
    /// MotionMatching.ManageQueries가 이 ScriptableObject를 읽어 query별 QueryComputedFlow를 생성합니다.
    /// </summary>
    public class QueriesComputed : ScriptableObject
    {
        /// <summary>일반 이동 query 목록입니다. 예: Walk, Run, 방향 이동.</summary>
        public List<MotionQueryComputed> queries;
        /// <summary>시작/진행/회복 상태를 갖는 action query 목록입니다.</summary>
        public List<ActionQueryComputed> actionQueries;
        /// <summary>반복 가능한 action query 목록입니다.</summary>
        public List<LoopActionQueryComputed> loopActionQueries;
        /// <summary>idle 또는 idle-like loop query 목록입니다.</summary>
        public List<IdleQueryComputed> idleQueries;
    }
}

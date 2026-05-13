using System;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 캐릭터 Transform 계층에서 bone Transform을 찾기 위한 확장 메서드 모음입니다.
    /// HumanoidAvatar/GenericAvatar가 boneName으로 runtime skeleton을 매핑할 때 사용합니다.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// 현재 Transform과 모든 자식 Transform을 depth-first로 순회하면서 query를 만족하는 첫 Transform을 반환합니다.
        /// Unity Transform 계층에는 LINQ FirstOrDefault를 바로 적용할 수 없기 때문에 재귀 탐색으로 제공합니다.
        /// </summary>
        public static Transform FirstOrDefault(this Transform transform, Func<Transform, bool> query)
        {
            if (query(transform))
            {
                return transform;
            }

            for (var i = 0; i < transform.childCount; i++)
            {
                var result = FirstOrDefault(transform.GetChild(i), query);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}

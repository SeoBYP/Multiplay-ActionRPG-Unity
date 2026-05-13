using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// HumanoidAvatar용 bone exclusion mask입니다.
    /// bonesToExclude index는 HumanBodyBones enum index와 같은 순서를 사용합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "MotionMatching/Humanoid Exclusion Mask")]
    public class HumanoidExclusionMask : ExclusionMaskBase
    {
        /// <summary>
        /// HumanBodyBones index가 제외 대상으로 표시되어 있는지 확인합니다.
        /// mask 배열보다 큰 id는 제외하지 않은 것으로 처리합니다.
        /// </summary>
        public override bool Contains(int id)
        {
            if (bonesToExclude.Count <= id)
            {
                return false;
            }

            return bonesToExclude[id];
        }
    }
}

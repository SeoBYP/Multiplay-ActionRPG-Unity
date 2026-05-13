using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// GenericAvatar용 bone exclusion mask입니다.
    /// Generic rig는 직접 정의한 AvatarBone 목록을 기준으로 하므로, mask가 참조하는 GenericAvatar와 개수가 맞아야 합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "MotionMatching/Generic Exclusion Mask")]
    public class GenericExclusionMask : ExclusionMaskBase
    {
        /// <summary>
        /// 이 mask가 대상으로 삼는 GenericAvatar입니다.
        /// MotionMatching.ExclusionMaskMatch에서 실제 avatar와 같은 asset인지 검증합니다.
        /// </summary>
        public GenericAvatar genericAvatar;

        /// <summary>
        /// GenericAvatar의 bone definition index가 제외 대상으로 표시되어 있는지 확인합니다.
        /// avatar bone 개수와 mask 개수가 다르면 잘못된 mask로 보고 제외하지 않습니다.
        /// </summary>
        public override bool Contains(int id)
        {
            var bones = genericAvatar.GetAvatarDefinition();
            if (bones.Count != bonesToExclude.Count)
            {
                return false;
            }

            if (bonesToExclude.Count <= id)
            {
                return false;
            }

            return bonesToExclude[id];
        }
    }
}

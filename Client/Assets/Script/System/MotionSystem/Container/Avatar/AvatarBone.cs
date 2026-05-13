using System;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching 데이터베이스에서 사용하는 본 하나의 식별 정보입니다.
    /// 런타임 Transform을 직접 저장하지 않고, id와 이름 정보를 저장해서
    /// bake 단계와 runtime skeleton 매핑 단계에서 같은 본 순서를 유지하게 합니다.
    /// </summary>
    [Serializable]
    public struct AvatarBone
    {
        /// <summary>
        /// 데이터베이스 내부에서 사용하는 본 인덱스입니다.
        /// HumanoidAvatar에서는 HumanBodyBones enum 값과 맞추고, GenericAvatar에서는 avatarBones 리스트 순서를 따릅니다.
        /// </summary>
        public int id;

        /// <summary>
        /// 사람이 읽기 쉬운 별칭입니다.
        /// 예: Hips, LeftFoot, RightHand 같은 검색/디버그 표시용 이름입니다.
        /// </summary>
        public string alias;

        /// <summary>
        /// 실제 Unity Transform 이름입니다.
        /// Generic rig나 Humanoid humanDescription에서 캐릭터 계층의 Transform을 찾을 때 사용합니다.
        /// </summary>
        public string boneName;
    }
}

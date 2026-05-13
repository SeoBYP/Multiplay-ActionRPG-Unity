using System.Collections.Generic;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching에서 특정 bone 또는 root motion 적용을 제외하기 위한 공통 mask입니다.
    /// Avatar가 runtime Transform 배열을 만들 때 이 mask를 확인해서 제외된 bone을 null로 남깁니다.
    /// </summary>
    public abstract class ExclusionMaskBase : ScriptableObject
    {
        /// <summary>
        /// true면 animation root motion을 직접 적용하지 않습니다.
        /// CharacterController, NavMeshAgent, Rigidbody 같은 별도 motor가 root 이동을 책임질 때 사용합니다.
        /// </summary>
        public bool disableRootMotion;

        /// <summary>
        /// bone index별 제외 여부입니다.
        /// index는 CustomAvatar.GetAvatarDefinition이 반환하는 bone 순서와 일치해야 합니다.
        /// </summary>
        public List<bool> bonesToExclude;

        /// <summary>
        /// 전달된 bone id가 이 mask에 의해 제외되는지 반환합니다.
        /// Humanoid/Generic 구현체는 자신의 avatar 타입과 bone 개수에 맞춰 범위를 검증합니다.
        /// </summary>
        public abstract bool Contains(int id);
    }
}

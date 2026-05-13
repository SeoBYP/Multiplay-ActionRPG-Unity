using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching 시스템이 캐릭터 리그를 공통 방식으로 다루기 위한 Avatar 추상화입니다.
    /// Humanoid와 Generic 리그는 본을 찾는 방식이 다르지만, runtime pose search/apply 단계에서는
    /// Transform 배열, root bone index, 원본 회전값만 동일한 형태로 필요합니다.
    /// </summary>
    public abstract class CustomAvatar : ScriptableObject
    {
        /// <summary>
        /// Unity Avatar asset입니다.
        /// HumanoidAvatar에서는 humanDescription을 통해 HumanBodyBones와 실제 skeleton bone 이름을 매핑합니다.
        /// </summary>
        public Avatar avatar;
        
        /// <summary>
        /// Motion Matching에서 root로 취급할 본 인덱스입니다.
        /// root motion/trajectory 계산과 root 제외 mask 판단에 사용됩니다.
        /// </summary>
        [HideInInspector]
        public int rootBone;

        private int _length;

        /// <summary>
        /// 이 Avatar가 제어하거나 검색 feature로 사용할 본 개수입니다.
        /// 구현체마다 Humanoid enum 개수 또는 Generic avatarBones 개수로 계산합니다.
        /// </summary>
        public int Length
        {
            get => GetLength();
            protected set => _length = value;
        }
        
        /// <summary>
        /// 현재 Avatar에서 root로 사용할 본 인덱스를 반환합니다.
        /// </summary>
        public virtual int GetRootBone()
        {
            return rootBone;
        }

        /// <summary>
        /// root로 사용할 본 인덱스를 설정합니다.
        /// Humanoid 구현체는 이 값을 HumanBodyBones로도 동기화합니다.
        /// </summary>
        public virtual void SetRootBone(int root)
        {
            rootBone = root;
        }

        /// <summary>
        /// 구현체별 본 개수를 반환합니다.
        /// </summary>
        protected abstract int GetLength();
        
        /// <summary>
        /// 캐릭터 root 아래에서 Motion Matching이 사용할 본 Transform 배열을 구성합니다.
        /// 배열 인덱스는 Avatar 정의의 본 id와 일치해야 하며, 제외 mask에 걸린 본은 null로 둡니다.
        /// </summary>
        public abstract Transform[] GetCharacterTransforms(Transform root, ExclusionMaskBase exclusionMask);

        /// <summary>
        /// 에디터/디버그/데이터셋 생성용 본 정의 목록을 반환합니다.
        /// </summary>
        public abstract List<AvatarBone> GetAvatarDefinition();

        /// <summary>
        /// 캐릭터의 원래 회전값과 기본 pose 회전값을 계산합니다.
        /// Motion Matching에서 baked pose와 현재 캐릭터 리그의 차이를 보정할 때 필요한 기준값입니다.
        /// </summary>
        public abstract void GetOriginalAvatarRotations(
            out quaternion[] originalCharacterRotations,
            out quaternion[] defaultRotations,
            Transform[] characterTransforms, 
            Transform transform);
    }
}

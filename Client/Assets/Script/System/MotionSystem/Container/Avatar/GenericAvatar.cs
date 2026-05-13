using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Generic rig용 Avatar 정의입니다.
    /// Humanoid처럼 Unity가 표준 HumanBodyBones 매핑을 제공하지 않는 캐릭터를 위해,
    /// 직접 등록한 AvatarBone 목록의 boneName으로 Transform 계층을 검색합니다.
    /// </summary>
    public class GenericAvatar : CustomAvatar
    {
        /// <summary>
        /// Generic 캐릭터에서 Motion Matching이 사용할 본 목록입니다.
        /// 리스트 순서가 곧 database의 bone index가 되므로 bake와 runtime에서 같은 asset을 사용해야 합니다.
        /// </summary>
        [SerializeField]
        private List<AvatarBone> avatarBones;
        
        /// <summary>
        /// GenericAvatar는 등록된 AvatarBone 개수를 본 개수로 사용합니다.
        /// </summary>
        protected override int GetLength()
        {
            return avatarBones.Count;
        }
        
        /// <summary>
        /// root Transform 아래에서 avatarBones의 boneName과 같은 이름을 가진 Transform을 재귀 검색합니다.
        /// ExclusionMaskBase가 특정 id를 제외하면 해당 배열 슬롯은 null로 남겨,
        /// pose search/apply 단계에서 그 본을 사용하지 않게 합니다.
        /// </summary>
        public override Transform[] GetCharacterTransforms(Transform root, ExclusionMaskBase exclusionMask)
        {
            Transform[] characterTransforms = new Transform[Length];
            var transformsByName = root
                .GetComponentsInChildren<Transform>(true)
                .GroupBy(current => current.name)
                .ToDictionary(group => group.Key, group => group.First());
            for(int i = 0; i < avatarBones.Count; i++)
            {
                try
                {
                    if (i > Length - 1 || (exclusionMask != null && exclusionMask.Contains(i)))
                    {
                        continue;
                    }
                    transformsByName.TryGetValue(avatarBones[i].boneName, out characterTransforms[i]);
                }
                catch
                {
                    // ignored
                }
                
            }
            
            return characterTransforms;
        }

        /// <summary>
        /// 현재 GenericAvatar가 사용하는 본 정의를 반환합니다.
        /// Dataset 생성, exclusion mask UI, 디버그 표시에서 사용됩니다.
        /// </summary>
        public override List<AvatarBone> GetAvatarDefinition()
        {
            return avatarBones;
        }

        /// <summary>
        /// 외부에서 구성한 Generic 본 정의를 이 Avatar에 복사합니다.
        /// 원본 리스트를 그대로 참조하지 않아 호출 측 변경이 asset 내부 리스트를 즉시 오염시키지 않게 합니다.
        /// </summary>
        public void SetAvatarDefinition(List<AvatarBone> bones)
        {
            avatarBones = new List<AvatarBone>(bones);
        }
        
        /// <summary>
        /// Generic rig는 Humanoid T-pose 보정 정보가 없으므로 현재 localRotation을 원본/기본 회전으로 사용합니다.
        /// null 본은 identity로 채워서 이후 배열 접근이 깨지지 않게 합니다.
        /// </summary>
        public override void GetOriginalAvatarRotations(
            out quaternion[] originalCharacterRotations, 
            out quaternion[] defaultRotations,
            Transform[] characterTransforms, 
            Transform transform)
        {
            originalCharacterRotations = new quaternion[characterTransforms.Length];
            for (int i = 0; i < characterTransforms.Length; i++)
            {
                if (characterTransforms[i] == null)
                {
                    originalCharacterRotations[i] = Quaternion.identity;
                    continue;
                }

                originalCharacterRotations[i] = characterTransforms[i].localRotation;
            }

            defaultRotations = originalCharacterRotations;
        }
    }
}

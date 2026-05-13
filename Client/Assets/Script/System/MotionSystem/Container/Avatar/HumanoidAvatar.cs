using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Unity Humanoid Avatar용 본 매핑 구현입니다.
    /// Unity Avatar의 humanDescription을 읽어 HumanBodyBones enum과 실제 skeleton Transform 이름을 연결합니다.
    /// GenericAvatar와 달리 표준 Humanoid 본 순서를 database index로 사용할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "MotionMatching/Humanoid Avatar")]
    public class HumanoidAvatar : CustomAvatar
    {
        /// <summary>
        /// Motion Matching에서 root로 사용할 Humanoid 본입니다.
        /// 일반적으로 Hips가 root trajectory와 pose 기준 본으로 사용됩니다.
        /// </summary>
        [SerializeField]
        private HumanBodyBones humanRootBone;
        
        /// <summary>
        /// Humanoid root bone을 int index로 반환합니다.
        /// HumanBodyBones enum 값이 배열 index로 쓰입니다.
        /// </summary>
        public override int GetRootBone()
        {
            return (int)humanRootBone;
        }

        /// <summary>
        /// root bone index를 설정하고 HumanBodyBones 값도 함께 갱신합니다.
        /// </summary>
        public override void SetRootBone(int root)
        {
            rootBone = root;
            humanRootBone = (HumanBodyBones)root;
        }

        /// <summary>
        /// LastBone은 실제 본이 아니라 enum sentinel이므로 제외합니다.
        /// </summary>
        protected override int GetLength()
        {
            return Enum.GetNames(typeof(HumanBodyBones)).Length - 1;
        }

        /// <summary>
        /// HumanBodyBones 순서대로 캐릭터 Transform 배열을 구성합니다.
        /// Unity Avatar의 humanDescription에서 humanName과 boneName을 매핑한 뒤,
        /// root 계층에서 실제 Transform 이름을 찾아 배열에 넣습니다.
        /// </summary>
        public override Transform[] GetCharacterTransforms(Transform root, ExclusionMaskBase exclusionMask)
        {
            HumanBone[] humanBones = GetHumanBones();
            IEnumerable<HumanBodyBones> values = Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>();
            Transform[] characterTransforms = new Transform[Length];
            var transformsByName = root
                .GetComponentsInChildren<Transform>(true)
                .GroupBy(current => current.name)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var (boneValue, i) in values.Select((value, i) => (value, i)))
            {
                try
                {
                    if (i > Length - 1 || (exclusionMask != null && exclusionMask.Contains(i)))
                    {
                        continue;
                    }

                    HumanBone currentBone = humanBones.First(x => x.humanName.Equals(boneValue.ToString()));
                    transformsByName.TryGetValue(currentBone.boneName, out characterTransforms[i]);
                }
                catch
                {
                    // ignored
                }
            }

            return characterTransforms;
        }

        private HumanBone[] GetHumanBones()
        {
            // Unity humanDescription의 humanName에는 공백이 들어갈 수 있습니다.
            // HumanBodyBones enum 이름과 비교하기 위해 공백을 제거한 복사본을 사용합니다.
            return avatar.humanDescription.human
                .Select(current =>
                {
                    current.humanName = current.humanName.Replace(" ", "");
                    return current;
                })
                .ToArray();
        }

        
        /// <summary>
        /// Humanoid Avatar의 표준 본 정의 목록을 생성합니다.
        /// 매핑되지 않은 본은 id -1의 빈 AvatarBone으로 채워 배열 순서를 유지합니다.
        /// </summary>
        public override List<AvatarBone> GetAvatarDefinition()
        {
            //ToDo: change this method to add nulls + check bonesOptions in Configuration
            // return GetHumanBones().Select((bone, index) => new AvatarBone()
            // {
            //     id = index,
            //     alias = bone.humanName,
            //     boneName = bone.boneName
            // }).ToList();
            
            HumanBone[] humanBones = GetHumanBones();
            IEnumerable<HumanBodyBones> values = Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>();
            List<AvatarBone> avatarBones = new List<AvatarBone>();
            foreach (var (boneValue, i) in values.Select((value, i) => (value, i)))
            {
                try
                {
                    if (i > Length - 1)
                    {
                        continue;
                    }

                    HumanBone currentBone = humanBones.First(x => x.humanName.Equals(boneValue.ToString()));
                    avatarBones.Add(new AvatarBone()
                    {
                        id = i,
                        alias = currentBone.humanName,
                        boneName = currentBone.boneName,
                    });
                }
                catch
                {
                    avatarBones.Add(new AvatarBone()
                    {
                        id = -1,
                        alias = "",
                        boneName = "",
                    });
                }
            }

            return avatarBones;
        }

        /// <summary>
        /// Humanoid 리그의 원본 회전값과 T-pose 기준 회전값을 계산합니다.
        /// Motion Matching pose data가 다른 캐릭터 리그에 적용될 때 필요한 회전 보정 기준입니다.
        /// </summary>
        public override void GetOriginalAvatarRotations(out quaternion[] originalCharacterRotations, out quaternion[] defaultRotations,
            Transform[] characterTransforms, Transform transform)
        {
            defaultRotations = new quaternion[characterTransforms.Length];
            SetTPoseRotation(characterTransforms, defaultRotations);
            GetStartingRotation(out originalCharacterRotations, characterTransforms, transform);
        }
        
        /// <summary>
        /// Avatar skeleton에 저장된 T-pose rotation을 실제 Transform에 임시 적용합니다.
        /// 적용 전 localRotation은 currentRotations에 저장해 나중에 기본 회전값으로 사용할 수 있게 합니다.
        /// </summary>
        private void SetTPoseRotation(Transform[] characterTransforms, quaternion[] currentRotations)
        {
            var humanBodyBoneValues = Enum.GetValues(typeof(HumanBodyBones));
            SkeletonBone[] skeletonBones = avatar.humanDescription.skeleton;
            foreach (var sb in skeletonBones)
            {
                foreach (HumanBodyBones bone in humanBodyBoneValues)
                {
                    if (bone == HumanBodyBones.LastBone)
                    {
                        continue;
                    }
                    
                    var currentTransform = characterTransforms[(int)bone];
                    if (currentTransform == null)
                    {
                        continue;
                    }
                    
                    if (!currentTransform.name.Equals(sb.name))
                    {
                        continue;
                    }
                    
                    currentRotations[(int)bone] = currentTransform.localRotation;
                    
                    var localBoneRotation = sb.rotation;
                    currentTransform.localRotation = localBoneRotation;
                }
            }
        }
        
        /// <summary>
        /// T-pose가 적용된 상태에서 캐릭터 root 기준의 상대 회전을 구합니다.
        /// 이 값은 baked pose rotation과 현재 캐릭터의 실제 transform rotation 차이를 맞추는 기준으로 사용됩니다.
        /// </summary>
        private void GetStartingRotation(out quaternion[] initialValues,Transform[] characterTransforms, Transform characterRoot)
        {
            var humanBodyBoneValues = Enum.GetValues(typeof(HumanBodyBones));
            initialValues = new quaternion[humanBodyBoneValues.Length];
            SkeletonBone[] skeletonBones = avatar.humanDescription.skeleton;
            foreach (var sb in skeletonBones)
            {
                foreach (HumanBodyBones bone in humanBodyBoneValues)
                {
                    if (bone == HumanBodyBones.LastBone)
                    {
                        continue;
                    }
                    
                    var currentTransform = characterTransforms[(int)bone];
                    if (currentTransform == null)
                    {
                        continue;
                    }
                    
                    if (!currentTransform.name.Equals(sb.name))
                    {
                        continue;
                    }
                
                    //Base - working
                    var relativeRotation = Quaternion.Inverse(characterRoot.rotation) * currentTransform.rotation;  //Rotation relative to character root
                    initialValues[(int)bone] = relativeRotation;
                }
            }
        }
    }
}

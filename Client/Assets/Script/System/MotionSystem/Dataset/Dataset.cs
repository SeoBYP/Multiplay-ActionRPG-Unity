using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose Player 기반 Motion Matching에서 사용할 animation pose database입니다.
    /// Editor bake 결과는 managed 데이터로 저장하고, runtime에서는 NativeArray 데이터로 변환해서 검색/적용합니다.
    /// </summary>
    public class Dataset : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// 런타임에서 사용할 NativeArray 기반 pose 데이터입니다.
        /// 바깥쪽 List는 animation clip, 안쪽 List는 해당 clip의 frame pose를 의미합니다.
        /// </summary>
        public List<List<AnimationDataNative>> animationsDataNative;

        /// <summary>
        /// 이 Dataset이 기준으로 삼는 Avatar 정의입니다.
        /// bone 순서, root bone, Humanoid/Generic runtime Transform 매핑 기준을 제공합니다.
        /// </summary>
        public CustomAvatar avatar;

        /// <summary>
        /// future trajectory를 몇 개의 sample point로 비교할지 정합니다.
        /// 값이 클수록 더 긴 미래 궤적을 비교하지만 feature 비용도 증가합니다.
        /// </summary>
        public int futureEstimates;

        /// <summary>
        /// future trajectory가 바라보는 전체 시간 범위입니다.
        /// futureEstimates와 함께 각 미래 sample 간격을 결정합니다.
        /// </summary>
        public float futureEstimatesTime;

        /// <summary>
        /// past trajectory를 몇 개의 sample point로 저장/비교할지 정합니다.
        /// 현재 pose가 어떤 이동 흐름에서 왔는지 판단하는 데 사용합니다.
        /// </summary>
        public int pastEstimates;

        /// <summary>
        /// past trajectory가 참조하는 전체 시간 범위입니다.
        /// 방향 전환, UTurn, 정지 전환의 continuity 판단에 중요합니다.
        /// </summary>
        public float pastEstimatesTime;

        /// <summary>
        /// Unity 직렬화가 가능한 managed pose 데이터입니다.
        /// 바깥쪽 List는 animation clip, 안쪽 List는 frame pose입니다.
        /// </summary>
        [HideInInspector] public List<List<AnimationData>> animationsData;

        /// <summary>
        /// Unity가 중첩 List를 직접 직렬화하지 못하는 문제를 피하기 위한 평탄화 저장소입니다.
        /// OnBeforeSerialize/OnAfterDeserialize에서 animationsData와 상호 변환됩니다.
        /// </summary>
        [HideInInspector] public List<AnimationDataTemp> animationsDataTemp = new();

        /// <summary>
        /// Dataset bake 기준 skeleton의 원본 bone rotation입니다.
        /// 다른 runtime 캐릭터에 pose를 적용할 때 rig 기준 차이를 보정하는 데 사용합니다.
        /// </summary>
        [HideInInspector] public quaternion[] originalBonesRotation;

        /// <summary>
        /// animation clip을 몇 초 간격으로 sample했는지 나타냅니다.
        /// 현재 frame에서 다음 frame으로 이어갈 때 시간 계산의 기준입니다.
        /// </summary>
        [HideInInspector] public float poseStep;

        /// <summary>
        /// velocity 기록에 사용할 frame 간격 또는 sample 설정 값입니다.
        /// Baker가 bone/root velocity를 계산할 때 참조할 수 있습니다.
        /// </summary>
        [HideInInspector] public int recordVelocity;

        /// <summary>
        /// 각 animation clip의 마지막 유효 pose index입니다.
        /// loop/non-loop frame 진행과 범위 검증에 사용할 수 있습니다.
        /// </summary>
        public List<int> lastAnimationPoses;
        public QueriesComputed queriesComputed;
        public Characteristics characteristics;
        public Tags tagsList;
        public List<MotionSearchDatabaseAsset> motionSearchDatabaseAssets;
        public List<MotionSearchDatabaseBakeRecord> motionSearchDatabases;

        public string GetDatabaseNameForAnimation(int animationID)
        {
            if (motionSearchDatabases == null || animationID < 0)
            {
                return "None";
            }

            var names = new List<string>();
            foreach (var database in motionSearchDatabases)
            {
                if (database?.animationIDs == null || !database.animationIDs.Contains(animationID))
                    continue;

                names.Add(database.name);
            }

            return names.Count == 0 ? "None" : string.Join(", ", names);
        }

        public string GetAnimationName(int animationID)
        {
            string path = GetAnimationPath(animationID);
            if (string.IsNullOrEmpty(path))
                return "None";

            int separatorIndex = path.LastIndexOf("//", global::System.StringComparison.Ordinal);
            string fileName = separatorIndex >= 0 ? path.Substring(separatorIndex + 2) : path;

            int slashIndex = fileName.LastIndexOf('/');
            if (slashIndex >= 0)
                fileName = fileName.Substring(slashIndex + 1);

            int dotIndex = fileName.LastIndexOf('.');
            return dotIndex > 0 ? fileName.Substring(0, dotIndex) : fileName;
        }

        private string GetAnimationPath(int animationID)
        {
            if (motionSearchDatabases == null || animationID < 0)
                return string.Empty;

            foreach (var database in motionSearchDatabases)
            {
                if (database?.animationIDs == null || database.animationPaths == null)
                    continue;

                int index = database.animationIDs.IndexOf(animationID);
                if (index >= 0 && index < database.animationPaths.Count)
                    return database.animationPaths[index];
            }

            return string.Empty;
        }

        /// <summary>
        /// Dataset의 trajectory sampling 설정과 내부 List를 초기화합니다.
        /// Bake 시작 전에 호출되어 animation frame 데이터가 쌓일 공간을 준비합니다.
        /// </summary>
        public void Initialize(
            int futureEstimates,
            float futureEstimatesTime,
            int pastEstimates,
            float pastEstimatesTime)
        {
            this.futureEstimates = futureEstimates;
            this.futureEstimatesTime = futureEstimatesTime;
            this.pastEstimates = pastEstimates;
            this.pastEstimatesTime = pastEstimatesTime;
            animationsData = new List<List<AnimationData>>();
            lastAnimationPoses = new List<int>();
        }

        /// <summary>
        /// Dataset runtime 사용 전에 managed 데이터를 NativeArray 데이터로 로드합니다.
        /// </summary>
        public void LoadData()
        {
            LoadAnimationsData();
        }

        /// <summary>
        /// animationsData를 animationsDataNative로 변환합니다.
        /// NativeArray는 직접 Dispose해야 하므로 Unload와 한 쌍으로 관리해야 합니다.
        /// </summary>
        public void LoadAnimationsData()
        {
            if (animationsDataNative != null)
            {
                return;
            }

            animationsDataNative = new List<List<AnimationDataNative>>();
            for (int i = 0; i < animationsData.Count; i++)
            {
                var animationPoses = new List<AnimationDataNative>();
                for (int j = 0; j < animationsData[i].Count; j++)
                {
                    animationPoses.Add(new AnimationDataNative
                    {
                        bonesData = new NativeArray<BoneData>(animationsData[i][j].bonesData, Allocator.Persistent),
                        isLoop = animationsData[i][j].isLoop,
                        rootPosition = animationsData[i][j].rootPosition,
                        rootRotation = animationsData[i][j].rootRotation
                    });
                }

                animationsDataNative.Add(animationPoses);
            }
        }

        /// <summary>
        /// 검색 결과 featureID가 가리키는 Native animation frame 데이터를 반환합니다.
        /// QueryComputed.featuresData의 animationID/animFrame을 Dataset index로 해석합니다.
        /// </summary>
        public AnimationDataNative GetAnimationDataNativeFromFeature(int featureID, QueryComputed queryComputed)
        {
            var feature = queryComputed.featuresData[featureID];
            return animationsDataNative[feature.animationID][feature.animFrame];
        }

        /// <summary>
        /// 검색 결과 featureID가 가리키는 managed animation frame 데이터를 반환합니다.
        /// Editor/debug 경로에서 pose 내용을 확인할 때 사용하기 쉽습니다.
        /// </summary>
        public AnimationData GetAnimationDataFromFeature(int featureID, QueryComputed queryComputed)
        {
            var feature = queryComputed.featuresData[featureID];
            return animationsData[feature.animationID][feature.animFrame];
        }

        /// <summary>
        /// 검색 결과가 속한 animation clip의 모든 managed frame pose를 반환합니다.
        /// 현재 frame 주변을 이어 재생하거나 clip 범위를 확인할 때 사용합니다.
        /// </summary>
        public List<AnimationData> GetAnimationPoses(int featureID, QueryComputed queryComputed)
        {
            var feature = queryComputed.featuresData[featureID];
            return animationsData[feature.animationID];
        }

        /// <summary>
        /// 검색 결과가 속한 animation clip의 모든 Native frame pose를 반환합니다.
        /// 런타임 pose advance/search 경로에서 사용합니다.
        /// </summary>
        public List<AnimationDataNative> GetAnimationNativePoses(int featureID, QueryComputed queryComputed)
        {
            var feature = queryComputed.featuresData[featureID];
            return animationsDataNative[feature.animationID];
        }

        /// <summary>
        /// 특정 animation frame의 특정 bone 데이터를 기록합니다.
        /// Baker가 AnimationClip을 sample하면서 bone pose/velocity를 누적 저장하는 진입점입니다.
        /// </summary>
        public void SetAnimationBoneData(
            int animationID,
            int animationFrame,
            int boneDatasLength,
            int boneID,
            float3 position,
            float3 localPosition,
            float3 scale,
            float3 velocity,
            float3 localVelocity,
            float3 angularVelocity,
            quaternion rotation)
        {
            AnimationData data = GetAnimationData(animationID, animationFrame, boneDatasLength);
            BoneData current = new BoneData
            {
                position = position,
                localPosition = localPosition,
                scale = scale,
                velocity = velocity,
                localVelocity = localVelocity,
                angularVelocity = angularVelocity,
                rotation = rotation,
                isValid = true
            };

            var index = boneID;
            data.bonesData[index] = current;

            animationsData[animationID].AddOrReplace(animationFrame, data);
        }

        /// <summary>
        /// 특정 animation frame이 loop 가능한 clip에 속한다는 정보를 기록합니다.
        /// </summary>
        public void SetAnimationIsLooping(
            int animationID,
            int animationFrame,
            int boneDatasLength)
        {
            AnimationData data = GetAnimationData(animationID, animationFrame, boneDatasLength);
            data.isLoop = true;
            animationsData[animationID].AddOrReplace(animationFrame, data);
        }

        /// <summary>
        /// 특정 animation frame의 root 위치/회전을 기록합니다.
        /// root trajectory와 motor movement를 분리하기 위한 원본 데이터입니다.
        /// </summary>
        public void SetAnimationRootData(
            int animationID,
            int animationFrame,
            int boneDatasLength,
            quaternion rootRotation,
            float3 rootPosition)
        {
            AnimationData data = GetAnimationData(animationID, animationFrame, boneDatasLength);

            data.rootPosition = rootPosition;
            data.rootRotation = rootRotation;
            animationsData[animationID].AddOrReplace(animationFrame, data);
        }

        /// <summary>
        /// animationID/frame 위치에 저장된 AnimationData를 가져오거나 새 frame 데이터를 만듭니다.
        /// 아직 없는 clip/frame이면 필요한 컨테이너를 만들어 bake 기록이 가능하게 합니다.
        /// </summary>
        private AnimationData GetAnimationData(int animationID, int animationFrame, int boneDatasLength)
        {
            if (animationsData.Count > animationID)
            {
                if (animationsData[animationID].Count > animationFrame)
                {
                    return animationsData[animationID][animationFrame];
                }

                return new AnimationData
                {
                    bonesData = new BoneData[boneDatasLength]
                };
            }

            animationsData.AddOrReplace(animationID, new List<AnimationData>());
            return new AnimationData
            {
                bonesData = new BoneData[boneDatasLength]
            };
        }

        /// <summary>
        /// Unity 직렬화 직전에 중첩 List 구조를 평탄화합니다.
        /// ScriptableObject asset에는 animationsDataTemp가 저장되고, NativeArray 데이터는 저장하지 않습니다.
        /// </summary>
        public void OnBeforeSerialize()
        {
            animationsDataTemp?.Clear();
            if (animationsData == null) return;

            for (int i = 0; i < animationsData.Count; i++)
            {
                for (int j = 0; j < animationsData[i].Count; j++)
                {
                    AnimationData anim = animationsData[i][j];
                    animationsDataTemp.Add(new AnimationDataTemp
                    {
                        animationID = i,
                        keyFrame = j,
                        bonesData = anim.bonesData,
                        rootPosition = anim.rootPosition,
                        rootRotation = anim.rootRotation,
                        isLoop = anim.isLoop
                    });
                }
            }
        }

        /// <summary>
        /// Unity 역직렬화 직후 평탄화된 데이터를 다시 clip/frame 중첩 List로 복원합니다.
        /// 런타임 NativeArray 변환은 LoadData에서 별도로 수행합니다.
        /// </summary>
        public void OnAfterDeserialize()
        {
            animationsData = new List<List<AnimationData>>();
            foreach (var animationDataTemp in animationsDataTemp)
            {
                if (animationsData.Count <= animationDataTemp.animationID)
                {
                    animationsData.Insert(animationDataTemp.animationID, new List<AnimationData>());
                }

                animationsData[animationDataTemp.animationID].Insert(
                    animationDataTemp.keyFrame,
                    new AnimationData
                    {
                        isLoop = animationDataTemp.isLoop,
                        bonesData = animationDataTemp.bonesData,
                        rootPosition = animationDataTemp.rootPosition,
                        rootRotation = animationDataTemp.rootRotation
                    });
            }
        }

        /// <summary>
        /// LoadData에서 생성한 NativeArray를 해제합니다.
        /// Dataset을 사용하는 MotionMatching 컴포넌트가 종료될 때 반드시 호출해야 메모리 누수를 막을 수 있습니다.
        /// </summary>
        public void Unload()
        {
            if (animationsDataNative == null)
            {
                return;
            }

            foreach (var animation in animationsDataNative)
            {
                foreach (var pose in animation)
                {
                    var bonesData = pose.bonesData;
                    bonesData.Dispose();
                }
            }

            animationsDataNative = null;
        }
    }
}

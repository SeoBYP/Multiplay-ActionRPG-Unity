using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 런타임 검색/적용 단계에서 사용할 AnimationData의 NativeArray 버전입니다.
    /// managed 배열을 그대로 순회하면 GC와 bounds 처리 비용이 커질 수 있으므로,
    /// Job/Burst 기반 pose search로 확장할 때 이 구조를 사용합니다.
    /// </summary>
    public struct AnimationDataNative
    {
        /// <summary>
        /// 이 pose sequence가 마지막 frame 이후 처음 frame으로 이어질 수 있는지 나타냅니다.
        /// 현재 frame 주변을 계속 재생할 때 다음 frame 계산에 사용됩니다.
        /// </summary>
        public bool isLoop;

        /// <summary>
        /// 하나의 animation frame에 포함된 모든 bone pose 데이터입니다.
        /// 배열 index는 CustomAvatar가 정의한 bone index와 일치해야 합니다.
        /// </summary>
        public NativeArray<BoneData> bonesData;

        /// <summary>
        /// 이 frame의 root 회전입니다.
        /// root 방향, trajectory 방향, root offset 보정의 기준 값입니다.
        /// </summary>
        public quaternion rootRotation;

        /// <summary>
        /// 이 frame의 root 위치입니다.
        /// root delta, velocity, future/past trajectory 계산의 기준 값입니다.
        /// </summary>
        public float3 rootPosition;
    }

    /// <summary>
    /// Unity가 직렬화할 수 있는 managed animation frame 데이터입니다.
    /// Bake 결과는 이 타입으로 저장하고, 런타임에는 AnimationDataNative로 변환해서 사용할 수 있습니다.
    /// </summary>
    [Serializable]
    public struct AnimationData
    {
        /// <summary>
        /// 샘플링된 frame의 root 회전입니다.
        /// </summary>
        public quaternion rootRotation;

        /// <summary>
        /// 샘플링된 frame의 root 위치입니다.
        /// </summary>
        public float3 rootPosition;

        /// <summary>
        /// 샘플링된 frame의 bone pose 배열입니다.
        /// CustomAvatar의 bone definition 순서와 반드시 동일해야 합니다.
        /// </summary>
        public BoneData[] bonesData;

        /// <summary>
        /// 원본 animation clip 또는 pose sequence가 loop 가능한지 나타냅니다.
        /// </summary>
        public bool isLoop;
    }

    /// <summary>
    /// Unity가 중첩 List를 안정적으로 직렬화하지 못하는 문제를 피하기 위한 임시 평탄화 데이터입니다.
    /// Dataset.OnBeforeSerialize에서 clip/frame 정보를 펼쳐 저장하고,
    /// OnAfterDeserialize에서 다시 List&lt;List&lt;AnimationData&gt;&gt; 형태로 복원합니다.
    /// </summary>
    [Serializable]
    public struct AnimationDataTemp
    {
        /// <summary>
        /// 원본 clip이 loop 가능한지 여부입니다.
        /// </summary>
        public bool isLoop;

        /// <summary>
        /// animationsData의 바깥쪽 index입니다. 하나의 animation clip을 의미합니다.
        /// </summary>
        public int animationID;

        /// <summary>
        /// animationID 안에서의 frame index입니다.
        /// </summary>
        public int keyFrame;

        /// <summary>
        /// 해당 frame의 모든 bone pose 데이터입니다.
        /// </summary>
        public BoneData[] bonesData;

        /// <summary>
        /// 해당 frame의 root 회전입니다.
        /// </summary>
        public quaternion rootRotation;

        /// <summary>
        /// 해당 frame의 root 위치입니다.
        /// </summary>
        public float3 rootPosition;
    }

    /// <summary>
    /// 하나의 bone이 특정 frame에서 가지는 pose와 motion feature입니다.
    /// 검색에는 position/velocity 계열을, 실제 pose 적용에는 localPosition/rotation/scale을 사용합니다.
    /// </summary>
    [Serializable]
    public struct BoneData
    {
        /// <summary>
        /// 이 bone 데이터가 실제로 샘플링된 유효 데이터인지 나타냅니다.
        /// ExclusionMask로 제외되었거나 runtime skeleton에 없는 bone은 false로 둘 수 있습니다.
        /// </summary>
        public bool isValid;

        /// <summary>
        /// world 또는 character root 기준 bone 위치입니다.
        /// 현재 pose와 database pose의 위치 차이를 비교하는 feature로 사용할 수 있습니다.
        /// </summary>
        public float3 position;

        /// <summary>
        /// parent bone 기준 local position입니다.
        /// Pose Player가 Transform.localPosition에 직접 적용할 수 있는 값입니다.
        /// </summary>
        public float3 localPosition;

        /// <summary>
        /// bone local scale입니다.
        /// 대부분의 Humanoid pose matching에서는 고정값이지만 Generic rig 대응을 위해 저장합니다.
        /// </summary>
        public float3 scale;

        /// <summary>
        /// world 또는 character root 기준 bone 속도입니다.
        /// 현재 움직임의 흐름과 database frame의 움직임 흐름을 비교할 때 사용합니다.
        /// </summary>
        public float3 velocity;

        /// <summary>
        /// parent bone 기준 local velocity입니다.
        /// local-space pose continuity 비교에 사용할 수 있습니다.
        /// </summary>
        public float3 localVelocity;

        /// <summary>
        /// bone의 회전 속도입니다.
        /// 회전 흐름까지 matching score에 넣기 위한 확장 feature입니다.
        /// </summary>
        public float3 angularVelocity;

        /// <summary>
        /// bone 회전입니다.
        /// 현재 구조에서는 baked pose를 runtime Transform에 적용할 핵심 데이터입니다.
        /// </summary>
        public quaternion rotation;
    }
}

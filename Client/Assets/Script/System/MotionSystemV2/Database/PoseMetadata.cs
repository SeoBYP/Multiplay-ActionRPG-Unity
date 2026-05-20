using System;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Database에 Bake된 포즈 한 장의 메타데이터. NativeArray에 저장되는 blittable 구조체.
    /// Feature 데이터는 별도 float[] 평탄 배열에 poseIndex * featureDim 오프셋으로 저장된다.
    /// </summary>
    [Serializable]
    public struct PoseMetadata
    {
        // Database.entries 인덱스
        public int entryIndex;

        // 클립 내 프레임 번호
        public int frameIndex;

        // 클립 내 시간 (초)
        public float clipTime;

        // PoseFlags 비트 필드
        public int flags;

        public bool HasFlag(PoseFlags flag) => (flags & (int)flag) != 0;
    }
}

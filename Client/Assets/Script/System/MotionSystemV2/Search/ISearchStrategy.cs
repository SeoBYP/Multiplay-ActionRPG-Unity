using System;
using Unity.Collections;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 포즈 검색 전략 인터페이스.
    /// BruteForce와 KDTree 모두 이 계약을 구현한다.
    /// IDisposable: NativeArray를 소유하는 구현체(KDTreeSearch)를 위해 확장.
    /// </summary>
    public interface ISearchStrategy : IDisposable
    {
        /// <summary>
        /// queryFeatures와 가장 가까운 포즈 인덱스를 반환한다.
        /// </summary>
        /// <param name="queryFeatures">쿼리 Feature Vector (schema.TotalFeatureDimension 크기)</param>
        /// <param name="excludePoseIndex">이 인덱스 근처의 포즈로 점프 금지 (-1이면 제외 없음)</param>
        /// <param name="jumpThresholdFrames">excludePoseIndex 기준 ±N 프레임 이내는 제외</param>
        /// <returns>선택된 포즈 인덱스</returns>
        int FindBestPose(
            NativeSlice<float> queryFeatures,
            int excludePoseIndex,
            int jumpThresholdFrames);
    }
}

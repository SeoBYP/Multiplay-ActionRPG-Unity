using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 특정 query 조건에 대해 미리 계산된 feature 목록의 공통 기반 타입입니다.
    /// 예를 들어 Idle, Walk, Run, UTurn 같은 query는 각자 사용할 FeatureData 후보군을 가질 수 있습니다.
    /// </summary>
    [Serializable]
    public abstract class QueryComputed
    {
        /// <summary>
        /// 이 query를 식별하는 tag 이름 목록입니다. QueryComputedFlow는 현재 요청 query와 이 값이 모두 맞는 flow를 선택합니다.
        /// </summary>
        public string[] query;
        /// <summary>
        /// featuresData 안에서 이 query가 검색할 수 있는 구간 목록입니다.
        /// Motion/Idle/Action 상태별로 검색 범위를 분리하는 데 사용합니다.
        /// </summary>
        public List<QueryRange> ranges;

        /// <summary>future trajectory 예측에 사용할 전방 이동 속도입니다.</summary>
        public float forwardSpeed;
        /// <summary>future trajectory 예측에 사용할 후방 이동 속도입니다.</summary>
        public float backwardSpeed;
        /// <summary>future trajectory 예측에 사용할 좌우 이동 속도입니다.</summary>
        public float sideSpeed;
        
        /// <summary>
        /// 이 query에서 검색 대상이 되는 baked feature frame 목록입니다.
        /// </summary>
        [HideInInspector] public List<FeatureData> featuresData;
        [HideInInspector] public float3[] meanFeaturePosition;
        [HideInInspector] public float3[] stdFeaturePosition;
        [HideInInspector] public float3[] meanFeatureVelocity;
        [HideInInspector] public float3[] stdFeatureVelocity;
        [HideInInspector] public float3[] meanFutureOffset;
        [HideInInspector] public float3[] stdFutureOffset;
        [HideInInspector] public float3[] meanFutureDirection;
        [HideInInspector] public float3[] stdFutureDirection;
        [HideInInspector] public float3[] meanPastOffset;
        [HideInInspector] public float3[] stdPastOffset;
        [HideInInspector] public float3[] meanPastDirection;
        [HideInInspector] public float3[] stdPastDirection;
        
        private FeaturesComputedNative _featuresComputedNative;
        private NativeArray<QueryRange> _ranges;

        protected QueryComputed(int fEstimates, int pEstimates, int nBones)
        {
            meanFeaturePosition = new float3[nBones];
            stdFeaturePosition = new float3[nBones];
            meanFeatureVelocity = new float3[nBones];
            stdFeatureVelocity = new float3[nBones];
            
            meanFutureOffset    = new float3[fEstimates];
            meanFutureDirection = new float3[fEstimates];
            stdFutureOffset     = new float3[fEstimates];
            stdFutureDirection  = new float3[fEstimates];
            
            meanPastOffset      = new float3[pEstimates];
            meanPastDirection   = new float3[pEstimates];
            stdPastOffset       = new float3[pEstimates];
            stdPastDirection    = new float3[pEstimates];
            featuresData = new List<FeatureData>();
        }

        public virtual List<QueryRange> GetRanges()
        {
            return ranges;
        }
        
        /// <summary>
        /// managed feature/normalization 데이터를 NativeArray 형태로 변환해 반환합니다.
        /// PoseFinder의 Burst job 검색 경로에서 사용합니다.
        /// </summary>
        public virtual FeaturesComputedNative GetFeaturesQueryComputedNative()
        {
            return _featuresComputedNative.GetQueryComputedNative(this);
        }
        
        /// <summary>
        /// managed QueryRange 목록을 NativeArray로 변환해 반환합니다.
        /// </summary>
        public NativeArray<QueryRange> GetRangesNative()
        {
            if (_ranges.IsCreated)
            {
                return _ranges;
            }

            _ranges = new NativeArray<QueryRange>(ranges.ToArray(), Allocator.Persistent);
            return _ranges;
        }

        public virtual void Destroy()
        {
            _featuresComputedNative.Destroy();

            if (!_ranges.IsCreated)
            {
                return;
            }

            _ranges.Dispose();
        }
    }
}

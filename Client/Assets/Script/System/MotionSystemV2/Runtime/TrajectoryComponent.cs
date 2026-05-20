using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 캐릭터의 이동 궤적을 수집하고 미래 궤적을 예측한다.
    /// FixedUpdate마다 현재 위치/방향을 기록해 과거 히스토리를 유지하고,
    /// 입력 방향 기반 간단한 선형 예측으로 미래 샘플을 생성한다.
    /// </summary>
    public sealed class TrajectoryComponent : IDisposable
    {
        // 과거 + 현재 + 미래 샘플을 하나의 배열로 관리
        // timeOffset 오름차순 정렬 보장
        private NativeArray<TrajectorySampleData> _history;
        private readonly float[] _sampleOffsets;
        private readonly Transform _root;

        private float3 _prevPosition;
        private float3 _smoothedDirection;

        // 방향 스무딩 계수 (0=즉시 반응, 1=전혀 반응 안 함)
        private const float DirectionSmoothing = 0.85f;

        public NativeArray<TrajectorySampleData> History => _history;

        public TrajectoryComponent(Transform root, float[] sampleTimeOffsets)
        {
            _root          = root;
            _sampleOffsets = sampleTimeOffsets;
            _history       = new NativeArray<TrajectorySampleData>(
                sampleTimeOffsets.Length, Allocator.Persistent);

            _prevPosition     = root.position;
            _smoothedDirection = root.forward;

            // 초기값 채우기
            for (int i = 0; i < sampleTimeOffsets.Length; i++)
            {
                _history[i] = new TrajectorySampleData
                {
                    timeOffset = sampleTimeOffsets[i],
                    position   = float3.zero,
                    direction  = root.forward
                };
            }
        }

        /// <summary>
        /// FixedUpdate에서 호출. 현재 프레임 상태를 히스토리에 반영하고
        /// 미래 샘플을 입력 기반으로 예측한다.
        /// </summary>
        /// <param name="desiredDirection">입력 기반 이동 방향 (정규화, 크기 0이면 정지)</param>
        /// <param name="desiredSpeed">입력 기반 목표 속도 (m/s). 미래 trajectory 예측에 사용.</param>
        /// <param name="deltaTime">FixedDeltaTime</param>
        public void Update(float3 desiredDirection, float desiredSpeed, float deltaTime)
        {
            float3 currentPos = _root.position;
            float3 rootInv_x  = _root.right;
            float3 rootInv_z  = _root.forward;

            // 이동 방향 스무딩
            bool isMoving = math.lengthsq(desiredDirection) > 0.01f;
            if (isMoving)
            {
                _smoothedDirection = math.normalizesafe(
                    math.lerp(_smoothedDirection, desiredDirection, 1f - DirectionSmoothing));
            }

            // 현재 실제 속도 (월드) — 과거 샘플 보간용
            float3 actualVelocity = deltaTime > 0f
                ? (currentPos - _prevPosition) / deltaTime
                : float3.zero;
            _prevPosition = currentPos;

            for (int i = 0; i < _sampleOffsets.Length; i++)
            {
                float offset = _sampleOffsets[i];

                float3 worldPos;
                float3 worldDir;

                if (offset <= 0f)
                {
                    // 과거 / 현재: 실제 이동 기반 선형 외삽
                    worldPos = currentPos + actualVelocity * offset;
                    worldDir = isMoving ? _smoothedDirection : _root.forward;
                }
                else
                {
                    // 미래 예측: 실제 속도 대신 desiredSpeed 사용
                    // 이유: 캐릭터가 아직 물리적으로 움직이지 않아도 (Idle 포즈 재생 중)
                    //       플레이어 입력이 반영된 trajectory를 DB와 비교해야 Walk/Run을 선택할 수 있다.
                    float3 predictedDir = isMoving ? _smoothedDirection : float3.zero;
                    worldPos = currentPos + predictedDir * (desiredSpeed * offset);
                    worldDir = isMoving ? _smoothedDirection : _root.forward;
                }

                // 루트 로컬 스페이스로 변환
                float3 delta    = worldPos - currentPos;
                float3 localPos = new float3(
                    math.dot(delta, rootInv_x),
                    0f,
                    math.dot(delta, rootInv_z));
                float3 localDir = new float3(
                    math.dot(worldDir, rootInv_x),
                    0f,
                    math.dot(worldDir, rootInv_z));

                _history[i] = new TrajectorySampleData
                {
                    timeOffset = offset,
                    position   = localPos,
                    direction  = math.normalizesafe(localDir, new float3(0f, 0f, 1f))
                };
            }
        }

        public void Dispose()
        {
            if (_history.IsCreated) _history.Dispose();
        }
    }
}

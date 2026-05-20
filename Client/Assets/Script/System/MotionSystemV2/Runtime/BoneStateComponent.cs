using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// 런타임 스켈레톤의 본 위치/속도를 NativeArray로 유지한다.
    /// HumanBodyBones 인덱스 기준. FixedUpdate마다 갱신.
    /// </summary>
    public sealed class BoneStateComponent : IDisposable
    {
        private NativeArray<float3> _positions;
        private NativeArray<float3> _velocities;
        private NativeArray<float3> _prevPositions;

        private readonly Animator   _animator;
        private readonly Transform  _root;
        private readonly int        _boneCount;

        // 포즈 전환 직후 N 프레임간 velocity=0 강제 (블렌드 혼합값에서 오는 spike 방지)
        private int _velocityInvalidFrames = 0;

        // 물리적으로 불가능한 속도는 스파이크로 간주하고 0으로 대체 (단위: m/s)
        private const float MaxBoneSpeed = 10f;

        public NativeArray<float3> Positions  => _positions;
        public NativeArray<float3> Velocities => _velocities;

        public BoneStateComponent(Animator animator, Transform root)
        {
            _animator  = animator;
            _root      = root;
            _boneCount = (int)HumanBodyBones.LastBone;

            _positions     = new NativeArray<float3>(_boneCount, Allocator.Persistent);
            _velocities    = new NativeArray<float3>(_boneCount, Allocator.Persistent);
            _prevPositions = new NativeArray<float3>(_boneCount, Allocator.Persistent);

            // 초기값 채우기 (속도 0)
            CaptureCurrentPositions(_prevPositions);
            CaptureCurrentPositions(_positions);
        }

        /// <summary>
        /// 포즈 전환 직후 N 프레임간 velocity를 0으로 강제한다.
        /// _prevPositions가 블렌드 혼합값에서 왔을 때 velocity spike를 방지한다.
        /// </summary>
        public void InvalidateVelocity(int frames) => _velocityInvalidFrames = frames;

        /// <summary>
        /// FixedUpdate에서 호출. 본 위치를 갱신하고 속도를 계산한다.
        /// </summary>
        public void Update(float deltaTime)
        {
            // 이전 프레임 위치 저장
            _positions.CopyTo(_prevPositions);

            // 현재 본 위치 캡처
            CaptureCurrentPositions(_positions);

            for (int i = 0; i < _boneCount; i++)
            {
                if (deltaTime > 0f && _velocityInvalidFrames <= 0)
                {
                    var v = (_positions[i] - _prevPositions[i]) / deltaTime;
                    // 물리적으로 불가능한 속도는 spike로 간주 → 0 대체
                    _velocities[i] = math.length(v) > MaxBoneSpeed ? float3.zero : v;
                }
                else
                {
                    _velocities[i] = float3.zero;
                }
            }

            if (_velocityInvalidFrames > 0) _velocityInvalidFrames--;
        }

        private void CaptureCurrentPositions(NativeArray<float3> target)
        {
            if (_animator == null || !_animator.isHuman) return;

            float4x4 rootInverse = math.inverse(float4x4.TRS(
                _root.position, _root.rotation, Vector3.one));

            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform t = _animator.GetBoneTransform(bone);
                if (t == null) continue;
                target[(int)bone] = math.transform(rootInverse, (float3)(Vector3)t.position);
            }
        }

        public void Dispose()
        {
            if (_positions.IsCreated)     _positions.Dispose();
            if (_velocities.IsCreated)    _velocities.Dispose();
            if (_prevPositions.IsCreated) _prevPositions.Dispose();
        }
    }
}

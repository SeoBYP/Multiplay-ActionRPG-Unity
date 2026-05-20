using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// Motion Matching이 검색할 포즈 데이터베이스.
    /// Schema 규칙으로 AnimationClip 목록을 Bake해 float[] + PoseMetadata[]로 저장한다.
    /// 런타임에는 NativeArray로 변환해 사용한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PoseSearchDatabase", menuName = "MotionMatchingV2/Database")]
    public class PoseSearchDatabase : ScriptableObject
    {
        [Tooltip("이 데이터베이스의 Feature 규칙을 정의하는 Schema")]
        public PoseSearchSchema schema;

        [Tooltip("검색 대상 AnimationClip 목록")]
        public List<PoseSearchEntry> entries = new();

        [Tooltip("현재 포즈 지속 시 전체 Cost에 추가되는 바이어스. 음수=오래 유지")]
        public float continuingPoseCostBias = -0.05f;

        // ── Bake된 데이터 (에디터 직렬화, 런타임 읽기 전용) ──────────────────────
        [SerializeField, HideInInspector] private float[]         _bakedFeatures;
        [SerializeField, HideInInspector] private PoseMetadata[]  _bakedMetadata;

        // ── 런타임 NativeArray (LoadNativeData / UnloadNativeData로 수명 관리) ──
        private NativeArray<float>        _nativeFeatures;
        private NativeArray<PoseMetadata> _nativeMetadata;
        private bool _loaded;

        public bool IsBaked  => _bakedFeatures != null && _bakedFeatures.Length > 0;
        public int  PoseCount => _bakedMetadata?.Length ?? 0;

        /// <summary>
        /// 에디터 Bake 결과를 받아 ScriptableObject에 저장한다. DatabaseBuilder가 호출한다.
        /// </summary>
        public void SetBakedData(float[] features, PoseMetadata[] metadata)
        {
            _bakedFeatures = features;
            _bakedMetadata = metadata;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 직렬화된 배열을 NativeArray로 변환한다. MotionMatchingComponent.Awake에서 호출.
        /// </summary>
        public void LoadNativeData()
        {
            if (_loaded || !IsBaked) return;

            _nativeFeatures = new NativeArray<float>(_bakedFeatures, Allocator.Persistent);
            _nativeMetadata = new NativeArray<PoseMetadata>(_bakedMetadata, Allocator.Persistent);
            _loaded = true;
        }

        /// <summary>
        /// NativeArray를 해제한다. MotionMatchingComponent.OnDestroy에서 호출.
        /// </summary>
        public void UnloadNativeData()
        {
            if (!_loaded) return;
            if (_nativeFeatures.IsCreated) _nativeFeatures.Dispose();
            if (_nativeMetadata.IsCreated) _nativeMetadata.Dispose();
            _loaded = false;
        }

        /// <summary>
        /// poseIndex번째 포즈의 Feature 뷰를 반환한다.
        /// </summary>
        public FeatureVectorView GetPose(int poseIndex)
        {
            int dim    = schema.TotalFeatureDimension;
            int offset = poseIndex * dim;
            return new FeatureVectorView(
                new NativeSlice<float>(_nativeFeatures, offset, dim),
                _nativeMetadata[poseIndex]);
        }

        public NativeArray<float>        NativeFeatures => _nativeFeatures;
        public NativeArray<PoseMetadata> NativeMetadata => _nativeMetadata;

        private void OnDisable() => UnloadNativeData();
    }
}

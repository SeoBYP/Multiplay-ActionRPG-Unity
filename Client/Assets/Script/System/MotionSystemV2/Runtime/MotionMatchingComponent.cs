using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Script.System.MotionSystemV2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed partial class MotionMatchingComponent : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PoseSearchDatabase _database;

        [Tooltip("TrajectoryChannel/BoneState 좌표계 기준 트랜스폼.\n" +
                 "Model이 PlayerCharacter 자식으로 있을 때 PlayerCharacter를 할당.\n" +
                 "비워두면 이 컴포넌트의 transform을 사용한다.")]
        [SerializeField] private Transform _coordinateRoot;

        [Header("Search")]
        [Tooltip("KDTree(권장) 또는 BruteForce. BruteForce는 DB 소규모 디버그용")]
        [SerializeField] private SearchMode _searchMode = SearchMode.KDTree;
        [Tooltip("검색을 N FixedUpdate마다 수행. 낮을수록 반응성↑ CPU↑")]
        [SerializeField, Range(1, 10)] private int _searchIntervalFrames = 3;
        [Tooltip("같은 클립 내 현재 프레임 ±N 이내 포즈 점프 방지")]
        [SerializeField, Range(0, 30)]  private int _jumpThresholdFrames = 10;

        [Header("Blend")]
        [Tooltip("포즈 전환 시 Inertialization 블렌드 지속 시간 (초)")]
        [SerializeField, Range(0f, 0.5f)] private float _blendDuration = 0.2f;

        // Player controller가 매 프레임 설정하는 월드 스페이스 이동 속도
        public float3 DesiredVelocity { get; set; }

        // 외부 참조용 읽기 전용 상태
        public bool            IsActive         => _graph.IsValid() && _graph.IsPlaying();
        public int             CurrentPoseIndex => _currentPoseIndex;
        public PoseSearchEntry CurrentEntry     => _currentEntry;

        /// <summary>PlayableGraph를 시작하고 다음 FixedUpdate에서 즉시 검색한다.</summary>
        public void Resume()
        {
            if (!_graph.IsValid() || _graph.IsPlaying()) return;
            _graph.Play();
            _frameCounter = _searchIntervalFrames; // 즉시 검색 트리거
        }

        /// <summary>PlayableGraph를 멈춘다. AnimatorController가 자동으로 제어권을 가져간다.</summary>
        public void Pause()
        {
            if (_graph.IsValid() && _graph.IsPlaying())
                _graph.Stop();
        }

        // 포즈 전환 시 발생 (poseIndex, entry, clipTime)
        public event Action<int, PoseSearchEntry, float> OnPoseSelected;

        // ── 내부 컴포넌트 ────────────────────────────────────────────────────────
        private Animator        _animator;
        private TrajectoryComponent _trajectory;
        private BoneStateComponent  _boneState;
        private ISearchStrategy     _search;

        // _coordinateRoot 미할당 시 자신의 transform을 사용하는 유효 루트
        private Transform _effRoot;

        // ── Playables — 2슬롯 더블버퍼 ──────────────────────────────────────────
        // slot 0: active  (새 포즈, 가중치 1-blend)
        // slot 1: fading  (이전 포즈, 가중치 blend → 0)
        private PlayableGraph          _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable  _activePlayable;
        private AnimationClipPlayable  _fadingPlayable;

        // ── Inertialization ───────────────────────────────────────────────────────
        private InertializationState _inertia;

        private enum SearchMode { KDTree, BruteForce }

        // ── 상태 ─────────────────────────────────────────────────────────────────
        private int             _currentPoseIndex = -1;
        private PoseSearchEntry _currentEntry;
        private int             _frameCounter;

        // 포즈 전환 후 쿨다운 (블렌드 완료 직후 velocity spike로 인한 즉시 재전환 방지)
        // ApplyPose 시 _postSwitchCooldown = _searchIntervalFrames * PostSwitchMultiplier 로 설정.
        // 쿨다운 중에는 RunSearch 스킵. 블렌드 중(_inertia.IsActive) 카운트 안 함.
        [SerializeField, Range(0, 10), Tooltip("전환 후 RunSearch 차단 배수 (searchInterval × N 프레임)")]
        private int _postSwitchMultiplier = 3;
        private int _postSwitchCooldown   = 0;

        // ── 엔트리 범위 캐시 (AdvanceCurrentPoseIndex 루프 대응용) ────────────────
        // ApplyPose 시 현재 엔트리의 첫/마지막 포즈 인덱스를 캐싱한다.
        // AdvanceCurrentPoseIndex에서 O(1) 범위 체크 및 루프 wrap에 사용.
        private int   _entryFirstPose    = -1;
        private int   _entryLastPose     = -1;
        private int   _poseIndexAtApply  = -1;  // ApplyPose 시점의 poseIndex (절대 기준)
        private float _applyPoseFixedTime = 0f; // ApplyPose 시점의 Time.fixedTime

        // ── Unity 생명주기 ────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_database == null || !_database.IsBaked)
            {
                Debug.LogError("[MotionMatching] Database가 없거나 Bake되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _database.LoadNativeData();

            _effRoot    = _coordinateRoot != null ? _coordinateRoot : transform;
            _trajectory = new TrajectoryComponent(_effRoot, GetTrajectoryOffsets());
            _boneState  = new BoneStateComponent(_animator, _effRoot);
            _search     = _searchMode == SearchMode.KDTree
                ? (ISearchStrategy)new KDTreeSearch(_database)
                : new BruteForceSearch(_database);

            SetupPlayableGraph();
            InitFileLog();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            float3 desiredDir   = math.lengthsq(DesiredVelocity) > 0.001f
                ? math.normalizesafe(DesiredVelocity)
                : float3.zero;
            float  desiredSpeed = math.length(DesiredVelocity);

            _trajectory.Update(desiredDir, desiredSpeed, dt);
            _boneState.Update(dt);

            // 블렌드 가중치 갱신은 매 프레임 (검색 간격과 무관)
            TickBlend(dt);

            // Notify 처리는 매 프레임 (클립 시간 연속성 보장)
            ProcessNotifies();

            // Pause 중 (Jump/Fall/Attack 등)에는 검색 스킵
            // trajectory·boneState는 계속 갱신해 Resume 직후 최신 상태 유지
            if (!_graph.IsPlaying()) return;

            // Inertialization 블렌드 중에는 검색 스킵.
            // 블렌드 중 BoneState는 이전 클립과 새 클립의 혼합값 → CurrCost가 비정상적으로 높아져
            // 즉시 다른 포즈로 전환되는 oscillation 유발. 블렌드 완료 후 첫 탐색에서 정상 비교.
            if (_inertia.IsActive) return;

            // 블렌드 완료 직후 velocity spike 안정화 대기.
            // _prevPositions가 블렌드 혼합값에서 왔기 때문에 첫 N 프레임간 velocity가 폭발.
            if (_postSwitchCooldown > 0)
            {
                _postSwitchCooldown--;
                return;
            }

            if (++_frameCounter < _searchIntervalFrames) return;
            _frameCounter = 0;

            RunSearch(dt);
        }

        private void OnDestroy()
        {
            DisposeFileLog();
            if (_graph.IsValid()) _graph.Destroy();
            _trajectory?.Dispose();
            _boneState?.Dispose();
            _search?.Dispose();
            _database?.UnloadNativeData();
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private float[] GetTrajectoryOffsets()
        {
            foreach (var ch in _database.schema.Channels)
            {
                if (ch is TrajectoryChannel tc)
                {
                    var offsets = new float[tc.Samples.Length];
                    for (int i = 0; i < tc.Samples.Length; i++)
                        offsets[i] = tc.Samples[i].timeOffset;
                    return offsets;
                }
            }
            return new[] { -0.5f, 0f, 0.5f, 1.0f };
        }

        private void SetupPlayableGraph()
        {
            _graph = PlayableGraph.Create("MotionMatching");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            // 슬롯 2개: 0=active, 1=fading
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            _mixer.SetInputWeight(0, 1f);
            _mixer.SetInputWeight(1, 0f);

            var output = AnimationPlayableOutput.Create(_graph, "MM_Output", _animator);
            output.SetSourcePlayable(_mixer);

            _graph.Play();
        }

        /// <summary>
        /// Inertialization 진행 상태에 맞게 mixer 가중치를 갱신한다.
        /// 블렌드 중이 아니면 active=1, fading=0으로 고정.
        /// </summary>
        private void TickBlend(float dt)
        {
            _inertia.Tick(dt);

            float blendWeight = _inertia.BlendWeight; // 1→0
            _mixer.SetInputWeight(0, 1f - blendWeight);
            _mixer.SetInputWeight(1, blendWeight);

            // 블렌드 종료 시 fading 클립 정리
            if (!_inertia.IsActive && _fadingPlayable.IsValid())
            {
                _graph.Disconnect(_mixer, 1);
                _fadingPlayable.Destroy();
                _fadingPlayable = default;
            }
        }

        private void RunSearch(float dt)
        {
            // ── _currentPoseIndex를 실제 클립 재생 위치에 동기화 ──────────────────
            // ApplyPose() 이후 클립은 계속 진행하지만 _currentPoseIndex는 전환 시점에
            // 고정된다. 이 상태에서 CurrentCost를 계산하면 query(실제 재생 위치)와
            // DB(전환 시점)가 달라 cost가 부풀려지고, 항상 다른 포즈가 더 싸 보여
            // 진동(oscillation)이 발생한다. 매 RunSearch마다 재생 진행량만큼 전진시켜 방지.
            AdvanceCurrentPoseIndex();

            int dim = _database.schema.TotalFeatureDimension;
            using var query = new NativeArray<float>(dim, Allocator.Temp);

            BuildQueryVector(query, dt);

            var querySlice = new NativeSlice<float>(query);
            int best = _search.FindBestPose(querySlice, _currentPoseIndex, _jumpThresholdFrames);

            if (best == _currentPoseIndex)
            {
                CaptureSearchDebug(querySlice, best, "SAME_POSE");
                return;
            }

            // ContinuingPoseCostBias: 현재 포즈가 best보다 bias 이상 나쁠 때만 전환
            if (_currentPoseIndex >= 0)
            {
                float bias = _database.continuingPoseCostBias;
                if (_currentEntry != null) bias += _currentEntry.continuingPoseCostBias;

                if (bias < 0f)
                {
                    float bestCost    = ComputeQueryCost(querySlice, best);
                    float currentCost = ComputeQueryCost(querySlice, _currentPoseIndex);
                    if (bestCost > currentCost + bias)
                    {
                        CaptureSearchDebug(querySlice, best, "BIAS_BLOCKED", bestCost, currentCost, bias);
                        return;
                    }
                    CaptureSearchDebug(querySlice, best, "SWITCHED", bestCost, currentCost, bias);
                }
                else
                {
                    CaptureSearchDebug(querySlice, best, "SWITCHED_NO_BIAS");
                }
            }
            else
            {
                CaptureSearchDebug(querySlice, best, "INITIAL");
            }

            ApplyPose(best);
        }

        /// <summary>
        /// ApplyPose 이후 실제 경과 시간을 기준으로 _currentPoseIndex를 갱신한다.
        /// _searchIntervalFrames × dt 대신 Time.fixedTime - _applyPoseFixedTime을 사용해
        /// ApplyPose 직후 RunSearch가 빨리 발생해도 과도 전진하지 않는다.
        /// 루핑: PoseFlags.IsLooping 기준으로 wrap. 논루핑: 마지막 포즈에 클램프.
        /// </summary>
        private void AdvanceCurrentPoseIndex()
        {
            if (_poseIndexAtApply < 0 || _entryFirstPose < 0) return;

            float poseStep    = _database.schema.PoseStep;
            float elapsed     = Time.fixedTime - _applyPoseFixedTime;
            int   totalAdvance = Mathf.FloorToInt(elapsed / poseStep);

            int entryLen   = _entryLastPose - _entryFirstPose + 1;
            int posAtApply = _poseIndexAtApply - _entryFirstPose;
            int nextPos    = posAtApply + totalAdvance;

            bool isLooping = (_database.NativeMetadata[_poseIndexAtApply].flags & (int)PoseFlags.IsLooping) != 0;

            if (nextPos < entryLen)
            {
                _currentPoseIndex = _entryFirstPose + nextPos;
            }
            else if (isLooping && entryLen > 0)
            {
                _currentPoseIndex = _entryFirstPose + (nextPos % entryLen);
            }
            else
            {
                _currentPoseIndex = _entryLastPose;
            }
        }

        /// <summary>
        /// 현재 _currentPoseIndex가 속한 엔트리의 첫/마지막 포즈 인덱스를 캐싱한다.
        /// ApplyPose 시 1회 호출. 메타데이터가 entryIndex 순으로 연속 저장되므로 선형 탐색 O(entry 포즈 수).
        /// </summary>
        private void CacheEntryBounds()
        {
            if (_currentPoseIndex < 0)
            {
                _entryFirstPose = _entryLastPose = -1;
                return;
            }

            int entryIdx = _database.NativeMetadata[_currentPoseIndex].entryIndex;

            _entryFirstPose = _currentPoseIndex;
            while (_entryFirstPose > 0 &&
                   _database.NativeMetadata[_entryFirstPose - 1].entryIndex == entryIdx)
                _entryFirstPose--;

            _entryLastPose = _currentPoseIndex;
            while (_entryLastPose < _database.PoseCount - 1 &&
                   _database.NativeMetadata[_entryLastPose + 1].entryIndex == entryIdx)
                _entryLastPose++;
        }

        private float ComputeQueryCost(NativeSlice<float> query, int poseIndex)
        {
            var   schema     = _database.schema;
            int   featureDim = schema.TotalFeatureDimension;
            var   db         = _database.NativeFeatures;
            float total      = 0f;

            for (int c = 0; c < schema.Channels.Count; c++)
            {
                int   offset = schema.GetChannelOffset(c);
                int   dim    = schema.Channels[c].FeatureDimension;
                float w      = schema.GetNormalizedWeight(c);
                int   baseDb = poseIndex * featureDim + offset;

                float channelCost = 0f;
                for (int j = 0; j < dim; j++)
                {
                    float d = query[offset + j] - db[baseDb + j];
                    channelCost += d * d;
                }
                total += channelCost * w;
            }
            return total;
        }

        private void BuildQueryVector(NativeArray<float> query, float dt)
        {
            var schema = _database.schema;
            var ctx = new QueryBuildContext
            {
                rootTransform     = _effRoot,
                rootInverse       = math.inverse(float4x4.TRS(
                                        _effRoot.position, _effRoot.rotation, Vector3.one)),
                bonePositions     = _boneState.Positions,
                boneVelocities    = _boneState.Velocities,
                trajectoryHistory = _trajectory.History,
                deltaTime         = dt
            };

            for (int i = 0; i < schema.Channels.Count; i++)
            {
                int offset = schema.GetChannelOffset(i);
                int dim    = schema.Channels[i].FeatureDimension;
                schema.Channels[i].BuildQuery(ctx, new NativeSlice<float>(query, offset, dim));
            }
        }

        /// <summary>
        /// 새 포즈를 적용한다. 기존 active clip을 fading 슬롯으로 이동한 뒤
        /// 새 clip을 active 슬롯에 연결하고 Inertialization 블렌드를 시작한다.
        /// </summary>
        private void ApplyPose(int poseIndex)
        {
            var meta  = _database.NativeMetadata[poseIndex];
            var entry = _database.entries[meta.entryIndex];

            // CSV 로그: 전환 전 정보 수집
            string prevClipName = _currentEntry?.clipDisplayName
                ?? _currentEntry?.clip?.name ?? "-";
            float  prevClipTime = _activePlayable.IsValid() ? (float)_activePlayable.GetTime() : 0f;

            // 1. 이전 fading 클립 파괴 (블렌드 미완료 시에도 덮어쓰기)
            if (_fadingPlayable.IsValid())
            {
                _graph.Disconnect(_mixer, 1);
                _fadingPlayable.Destroy();
            }

            // 2. 현재 active 클립을 fading 슬롯으로 이동
            if (_activePlayable.IsValid())
            {
                _graph.Disconnect(_mixer, 0);
                _graph.Connect(_activePlayable, 0, _mixer, 1);
            }
            _fadingPlayable = _activePlayable;

            // 3. 새 active 클립 생성
            _activePlayable = AnimationClipPlayable.Create(_graph, entry.clip);
            _activePlayable.SetTime(meta.clipTime);
            _graph.Connect(_activePlayable, 0, _mixer, 0);

            // 4. Inertialization 블렌드 시작 (weight 1→0)
            _inertia.Begin(_blendDuration);

            _currentPoseIndex    = poseIndex;
            _currentEntry        = entry;
            _poseIndexAtApply    = poseIndex;
            _applyPoseFixedTime  = Time.fixedTime;
            CacheEntryBounds();   // 루프 대응 범위 캐시 갱신

            // CSV 기록 (전환 후 정보와 합산)
            string newClipName = !string.IsNullOrEmpty(entry.clipDisplayName)
                ? entry.clipDisplayName : (entry.clip != null ? entry.clip.name : "?");
            WriteLogLine(prevClipName, prevClipTime,
                         newClipName, meta.clipTime,
                         DbgDecision,
                         DbgBestCost, DbgCurrCost, DbgAppliedBias);

            // Notify: 포즈 점프 발생 플래그. 다음 ProcessNotifies에서 활성 구간 강제 종료
            _pendingJump   = true;
            _prevClipTime  = meta.clipTime;

            // 블렌드 완료 후 velocity spike가 안정될 때까지 검색 차단
            _postSwitchCooldown = _searchIntervalFrames * _postSwitchMultiplier;
            _boneState.InvalidateVelocity(_searchIntervalFrames * _postSwitchMultiplier + 2);

            OnPoseSelected?.Invoke(poseIndex, entry, meta.clipTime);
        }
    }
}

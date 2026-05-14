using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Pose transition에 사용할 기본 보간 방식입니다.
    /// position/scale은 Lerp, rotation은 Slerp를 기본 선택지로 사용합니다.
    /// </summary>
    public enum BlendingTypes
    {
        /// <summary>
        /// 선형 보간입니다.
        /// position, scale처럼 직선 보간이 자연스러운 값에 적합합니다.
        /// </summary>
        Lerp,

        /// <summary>
        /// 구면 선형 보간입니다.
        /// bone rotation/root rotation처럼 회전 보간이 필요한 값에 적합합니다.
        /// </summary>
        Slerp
    }

    [Serializable]
    public class MotionMatchingDebugSnapshot
    {
        public string stage;
        public string query;
        public string expectedQuery;
        public string flowType;
        public int currentFeatureID;
        public int animationID;
        public int animationFrame;
        public string clipName;
        public string databaseName;
        public int rangeStart;
        public int rangeStop;
        public float distance;
        public bool distanceIsInvalid;
        public float delta;
        public float elapsedTime;
        public bool isSearch;
        public bool isQueryDone;
        public Vector3 moveInput;
        public Vector3 facing;
        public Vector3 nextFutureOffset0;
        public Vector3 nextFutureDirection0;
        public int boneIndex;
        public string boneName;
        public bool hasRuntimeBone;
        public bool hasTargetBone;
        public bool appliesPosition;
        public bool appliesRotation;
        public Vector3 targetLocalPosition;
        public Vector3 blendStartLocalPosition;
        public Vector3 blendEndLocalPosition;
        public Vector3 blendedLocalPosition;
        public Vector4 targetRotation;
        public bool targetRotationInvalid;
        public Vector4 startRotation;
        public Vector4 endRotation;
        public Vector4 blendedRotation;
        public Vector4 finalRotation;
        public bool finalRotationInvalid;
        public Vector3 finalLocalPosition;
        public Vector3 runtimeLocalPosition;
        public Vector4 runtimeLocalRotation;
        public Vector4 runtimeWorldRotation;
        public Vector3 targetMinusRuntimeLocalPosition;
        public Vector3 finalMinusRuntimeLocalPosition;
    }

    [Serializable]
    public class MotionMatchingBoneDebugRow
    {
        public int boneIndex;
        public string boneName;
        public bool hasRuntimeBone;
        public bool hasTargetBone;
        public bool appliesPosition;
        public bool appliesRotation;
        public Vector3 targetLocalPosition;
        public Vector3 blendStartLocalPosition;
        public Vector3 blendEndLocalPosition;
        public Vector3 blendedLocalPosition;
        public Vector3 finalLocalPosition;
        public Vector3 runtimeLocalPosition;
        public Vector3 targetMinusRuntimeLocalPosition;
        public Vector3 finalMinusRuntimeLocalPosition;
        public Vector4 targetRotation;
        public Vector4 finalRotation;
        public Vector4 runtimeLocalRotation;
        public Vector4 runtimeWorldRotation;
        public bool targetRotationInvalid;
        public bool finalRotationInvalid;
    }

    /// <summary>
    /// Pose Player 기반 Motion Matching의 런타임 coordinator입니다.
    /// 현재 단계에서는 Dataset 로드, Avatar 선택, ExclusionMask 검증, runtime skeleton cache,
    /// blending/search/inertialization에 필요한 NativeArray 버퍼 초기화를 담당합니다.
    /// </summary>
    public class MotionMatching : MonoBehaviour
    {
        private const string Idle = "Idle";

        public string img;
        public CustomAvatar avatar;
        public Dataset dataset;
        [Tooltip("Seconds between full motion-matching searches. Lower values react faster but cost more CPU.")]
        [Min(0f)] public float searchRate = 0.08f;
        [Tooltip("Extra distance cost applied when a search candidate switches to a different animation clip.")]
        [Min(0f)] public float animationSwitchPenalty = 0.15f;
        public bool isRunning = true;

        [Range(0.05f, 0.2f)] public float halfLife = 0.05f;
        public bool wantApplyPositions;
        public bool wantApplyScales;
        public bool wantApplyRootBonePosition;

        [HideInInspector] public int currentAnimationID;
        [HideInInspector] public int currentAnimationFrame;

        [HideInInspector] public int animationName;

        private float _timeFromLastSearch;

        private bool _isInertialized = true;

        [Header("Estimation properties")] [Tooltip("Responsiveness for direction control")]
        public float responsivenessDirections = 0.75f;

        [Tooltip("Responsiveness for position control")]
        public float responsivenessPositions = 0.75f;

        [Header("Character Controller")]
        [Tooltip("ScriptableObject with your custom character controller implementation")]
        [SerializeField]
        protected CharacterControllerBase characterControllerBase;

        protected CharacterControllerBase CharacterControllerBaseInstantiated;

        [SerializeField] private ExclusionMaskBase exclusionMask;

        [Header("Weight Bones")] [Range(0f, 1f)]
        public float weightBonesPosition = 1f;

        [Range(0f, 1f)] public float weightBonesVelocity = 1f;

        [Header("Weight Futures")] [Range(0f, 1f)]
        public float weightFutureRootPosition = 1f;

        [Range(0f, 1f)] public float weightFutureRootDirection = 1f;

        [Header("Weight Pasts")] [Range(0f, 1f)]
        public float weightPastRootPosition = 1f;

        [Range(0f, 1f)] public float weightPastRootDirection = 1f;

        [Header("Weight Global")] [Range(0f, 1f)]
        public float weightBones = 1f;

        [Range(0f, 1f)] public float weightFutures = 1f;

        [Range(0f, 1f)] public float weightPasts = 1f;

        [HideInInspector] [Tooltip("It's only informative")]
        public string[] currentPlayedQuery;

        [HideInInspector] public string[] _currentQuery;
        [HideInInspector] public string[] startingQuery = { "Idle" };

        [Header("Blending Configuration")] public BlendingTypes blendType = BlendingTypes.Slerp;
        [HideInInspector] public bool isBlendActivated = true;

        [Header("Debug")] [Tooltip("Debug trajectory visually")]
        public bool debugTrajectory = true;
        [Tooltip("Draw current character velocity in SceneView.")]
        public bool debugVelocity = true;
        [Tooltip("Draw current move input and facing vectors in SceneView.")]
        public bool debugInputVectors = true;
        [Tooltip("Draw current query, animation id, frame, and vector values as a SceneView label.")]
        public bool debugPoseInfo = true;
        public float debugDrawHeight = 0.08f;
        public float debugVelocityScale = 0.25f;
        public float debugInputScale = 0.75f;
        [Header("Runtime Pose Debug")]
        public bool captureRuntimePoseDebug = true;
        public int debugBoneIndex = (int)HumanBodyBones.Hips;
        [HideInInspector] public MotionMatchingDebugSnapshot debugSnapshot = new();
        [Range(1, 55)] public int debugMaxBoneRows = 16;
        [HideInInspector] public MotionMatchingBoneDebugRow[] debugBoneRows = Array.Empty<MotionMatchingBoneDebugRow>();

        private FuturePrediction _nextPrediction;

        //Components
        protected TrajectoryEstimation TrajectoryEstimation;

        private float _delta;
        private bool _isSearch;
        //public Action onTransition;

        //private int _counter;

        private List<QueryComputedFlow> _flows;
        public QueryComputedFlow currentQueryFlow;
        private MotionData _motionData;

        //Transforms
        private Transform[] _characterTransforms;
        private TransformAccessArray _characterTransformsNative;
        private NativeArray<int> _characterTransformBoneIndices;

        //Generalize avatar
        private NativeArray<quaternion> _originalDiffRotations;

        private float _weightBonesPositionTemp;
        private float _weightBonesVelocityTemp;
        private float _weightFutureRootPositionTemp;
        private float _weightFutureRootDirectionTemp;
        private float _weightBonesTemp;
        private float _weightFuturesTemp;

        private bool _wantStop;
        private int _bonesLength;

        private bool _isTestRangesActive;
        private int _rangeToTest;

        private float _timeOnLastPositions;
        private GlobalWeights _globalWeights;

        private BlendingResults _blendingResults;
        private BlendBoundaries _blendBoundaries;

        private NativeArray<BoneData> _currentBoneDatas;
        private NativeArray<BoneData> _nextBoneDatas;
        private NativeArray<float3> _rootPositionBoundariesResults;
        private NativeArray<quaternion> _rootRotationBoundariesResults;
        private NativeArray<float3> _trajectoryFloat3Results;
        private NativeArray<float3> _bonePositionResults;
        private NativeArray<float3> _boneScaleResults;
        private NativeArray<quaternion> _boneRotationResults;
        private PoseFinderGenericVariables _poseFinderGenericVariables;
        private CurrentBoneTransformsValues _currentBoneTransformsValues;
        private NativeArray<DistanceResult> _poseResult;
        protected PastTrajectory PastTrajectory;
        private Vector3 _lastPos;
        [HideInInspector] public float3 currentVelocity;

        private float4x4 _rtsModel;
        private float4x4 _rtsModelInverse;
        private Coroutine _synchronizeCoroutine;

        private bool _hasExcludedMaskChanged;

        private bool _wantDebugDistances;

        /// <summary>
        /// Dataset, Avatar, runtime skeleton cache, pose search/blending용 NativeArray 버퍼를 초기화합니다.
        /// 아직 pose search/apply는 연결되어 있지 않고, Pose Player 시스템이 동작할 기반 데이터만 준비합니다.
        /// </summary>
        public virtual void Awake()
        {
            // Dataset bake/query 세팅이 끝나기 전에는 런타임 초기화를 건너뜁니다.
            // 빈 Dataset으로 QueryFlow를 만들면 Idle query를 찾지 못해 Play 진입 시 예외가 발생합니다.
            if (!isRunning || !HasValidRuntimeDataset())
            {
                isRunning = false;
                return;
            }

            dataset.LoadData();

            if (!avatar)
            {
                avatar = dataset.avatar;
            }

            if (!ExclusionMaskMatch(exclusionMask))
            {
                exclusionMask = null;
            }

            _bonesLength = avatar.Length;
            _characterTransforms = avatar.GetCharacterTransforms(transform, exclusionMask);
            CreateTransformAccessData(_characterTransforms);
            _currentBoneTransformsValues = new CurrentBoneTransformsValues
            {
                positions = new NativeArray<float3>(_characterTransforms.Length, Allocator.Persistent),
                rotations = new NativeArray<quaternion>(_characterTransforms.Length, Allocator.Persistent),
                localPositions = new NativeArray<float3>(_characterTransforms.Length, Allocator.Persistent),
                localRotations = new NativeArray<quaternion>(_characterTransforms.Length, Allocator.Persistent),
                localScales = new NativeArray<float3>(_characterTransforms.Length, Allocator.Persistent),
                bonesCounter = _characterTransforms.Length
            };

            InitializeInitialRotations();

            UpdateTransformsValues();
            UpdateModelValues();

            InitializeMotionData(_bonesLength);
            _poseFinderGenericVariables = new PoseFinderGenericVariables();
            _poseFinderGenericVariables.Create(dataset.futureEstimates, dataset.pastEstimates, _bonesLength);

            InitializeResults(_characterTransforms.Length);
            InitializeBoundaries(_characterTransforms.Length);

            PoseFinder.GetRootBasedPositions(
                ref _motionData.PreviousPositions,
                _currentBoneTransformsValues.positions,
                _currentBoneTransformsValues.bonesCounter,
                _rtsModelInverse);

            Inertialization.FirstFillInMotionData(ref _motionData, _originalDiffRotations,
                transform.rotation, _characterTransforms, _bonesLength);

            //Character Controller
            CharacterControllerBaseInstantiated = Instantiate(characterControllerBase)
                .Also(cc => cc.Initialize(this));
            
            TrajectoryEstimation =
                new TrajectoryEstimation(dataset.futureEstimates, dataset.pastEstimates, responsivenessDirections, responsivenessPositions, dataset, transform.position, transform.forward);
            
            _globalWeights = new GlobalWeights()
            {
                weightBones = weightBones,
                weightFutures = weightFutures,
                weightBonesPosition = weightBonesPosition,
                weightBonesVelocity = weightBonesVelocity,
                weightFutureRootDirection = weightFutureRootDirection,
                weightFutureRootPosition = weightFutureRootPosition,
                weightPastRootPosition = weightPastRootPosition,
                weightPastRootDirection = weightPastRootDirection,
                weightpasts = weightPasts
            };
            
            //Initial pose Update from poseLookUp
            ManageQueries();
            currentAnimationFrame = 0;
            if (startingQuery[0].Equals(Idle))
            {
                SendIdleQuery();
            }
            else
            {
                SetQuery(startingQuery);
            }
            _synchronizeCoroutine = StartCoroutine(SynchronizeTransforms());

            _lastPos = transform.position;
        }
        
        private void FixedUpdate()
        {
            ManageMotionMatching();
        }
        
        private void ManageMotionMatching()
        {
            if (!isRunning) return;
            
            // Get current trajectory estimation based on project input/AI controller.
            UpdateInput();
            UpdateModelValues();
            
            _nextPrediction = TrajectoryEstimation.GetFutureEstimations(
                ref _trajectoryFloat3Results,
                currentQueryFlow.GetQueryComputed(),
                _rtsModelInverse,
                transform,
                CharacterControllerBaseInstantiated.currentMoveInput,
                CharacterControllerBaseInstantiated.currentForward,
                CharacterControllerBaseInstantiated.IsStrafing(),
                Time.fixedDeltaTime,
                Time.fixedDeltaTime);

            ManageCharacterVelocity();
            
            PastTrajectory = TrajectoryEstimation.ManagePastTrajectory(transform);
            //Update visual frame every poseStep
            _delta += Time.fixedDeltaTime;
            if (_delta >= dataset.poseStep)
            {
                currentQueryFlow.GetNewPose(
                    ref _timeOnLastPositions, 
                    ref _motionData, 
                    ref _blendBoundaries,
                    ref _delta, 
                    _rtsModelInverse,
                    _poseFinderGenericVariables,
                    _rootPositionBoundariesResults,
                    _rootRotationBoundariesResults,
                    _poseResult,
                    _currentBoneDatas,
                    _nextBoneDatas,
                    _currentBoneTransformsValues.positions,
                    _originalDiffRotations,
                    _currentBoneTransformsValues.bonesCounter,
                    _nextPrediction, 
                    PastTrajectory,
                    avatar.GetRootBone(),
                    responsivenessDirections, 
                    IsDisabledRoot(),
                    _isInertialized,
                    wantApplyPositions,
                    wantApplyScales,
                    wantApplyRootBonePosition,
                    _wantDebugDistances,
                    _isTestRangesActive);
                currentQueryFlow.elapsedTime = _delta;
            }
            else
            {
                currentQueryFlow.elapsedTime += Time.fixedDeltaTime;
            }

            currentQueryFlow.GenerateNewPoseValues(
                ref _motionData.Offsets,
                ref _bonePositionResults,
                ref _boneScaleResults,
                ref _boneRotationResults,
                ref _blendingResults,
                out _timeOnLastPositions,
                _rtsModelInverse,
                _currentBoneTransformsValues.positions,
                _motionData.PreviousPositions,
                _originalDiffRotations,
                transform.rotation,
                _blendBoundaries,
                blendType,
                _currentBoneTransformsValues.bonesCounter,
                avatar.GetRootBone(),
                halfLife,
                IsDisabledRoot(),
                _isInertialized,
                isBlendActivated,
                wantApplyPositions,
                wantApplyScales,
                wantApplyRootBonePosition);
            
            if (currentQueryFlow.isQueryDone)
            {
                ResetQueryAfterAction();
            }
            
            currentAnimationFrame = currentQueryFlow.GetFeatures()[currentQueryFlow.currentFeatureID].animFrame;
            currentAnimationID =  currentQueryFlow.GetFeatures()[currentQueryFlow.currentFeatureID].animationID;
            CaptureRuntimePoseDebug("ManageMotionMatching");
        }
        
        private IEnumerator SynchronizeTransforms() // This is because of Final IK
        {
            yield return new WaitForFixedUpdate();
            if (isRunning)
            {
                currentQueryFlow
                    .SynchronizeTransforms(
                        _bonePositionResults, 
                        _boneScaleResults,
                        _boneRotationResults, 
                        _blendingResults,
                        CharacterControllerBaseInstantiated,
                        avatar.GetRootBone(),
                        IsDisabledRoot(),
                        wantApplyPositions,
                        wantApplyScales,
                        wantApplyRootBonePosition);
                UpdateTransformsValues();
                CaptureRuntimePoseDebug("SynchronizeTransforms");
            }
            
            _synchronizeCoroutine = StartCoroutine(SynchronizeTransforms());
        }
        private bool IsDisabledRoot()
        {
            return exclusionMask && exclusionMask.disableRootMotion;
        }

        private void UpdateInput()
        {
            CharacterControllerBaseInstantiated?.UpdateMotion(Time.fixedDeltaTime);
        }
        
        private void ManageCharacterVelocity()
        {
            var currentPos = transform.position;
            currentVelocity = (currentPos - _lastPos) / Time.fixedDeltaTime;
            _lastPos = currentPos;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !isRunning)
                return;

            if (!debugTrajectory && !debugVelocity && !debugInputVectors && !debugPoseInfo)
                return;

            var drawOrigin = transform.position + Vector3.up * debugDrawHeight;

            if (debugTrajectory && CanDrawTrajectoryDebug())
            {
                // Green: current input prediction, Red/Magenta: baked future/past trajectory on the selected pose.
                TrajectoryEstimation.DrawGizmos(transform, _characterTransforms, _nextPrediction, PastTrajectory,
                    currentQueryFlow);
            }

            if (debugVelocity)
            {
                DrawDebugArrow(drawOrigin, Flatten(currentVelocity) * debugVelocityScale, Color.cyan);
            }

            if (debugInputVectors && CharacterControllerBaseInstantiated != null)
            {
                DrawDebugArrow(drawOrigin + Vector3.up * 0.08f,
                    Flatten(CharacterControllerBaseInstantiated.currentMoveInput) * debugInputScale,
                    new Color(1f, 0.55f, 0.05f));

                DrawDebugArrow(drawOrigin + Vector3.up * 0.16f,
                    Flatten(CharacterControllerBaseInstantiated.currentForward) * debugInputScale,
                    Color.blue);
            }

#if UNITY_EDITOR
            if (debugPoseInfo)
            {
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2.1f, BuildDebugLabel());
            }
#endif
        }

        private bool CanDrawTrajectoryDebug()
        {
            if (TrajectoryEstimation == null ||
                currentQueryFlow == null ||
                _characterTransforms == null ||
                !_nextPrediction.futureOffsets.IsCreated ||
                !_nextPrediction.futureOffsetDirections.IsCreated ||
                !PastTrajectory.pastGlobalPosition.IsCreated ||
                !PastTrajectory.pastGlobalDirection.IsCreated)
            {
                return false;
            }

            if (_nextPrediction.futureOffsets.Length == 0 ||
                _nextPrediction.futureOffsetDirections.Length == 0 ||
                PastTrajectory.pastGlobalPosition.Length == 0 ||
                PastTrajectory.pastGlobalDirection.Length == 0)
            {
                return false;
            }

            return !math.isnan(_nextPrediction.futureOffsets[0].x);
        }

        private static Vector3 Flatten(float3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private static void DrawDebugArrow(Vector3 origin, Vector3 vector, Color color)
        {
            if (vector.sqrMagnitude <= 0.0001f)
                return;

            Gizmos.color = color;
            GizmosExtensions.DrawArrow(origin, vector, 0.15f);
        }

        private string BuildDebugLabel()
        {
            var queryText = currentPlayedQuery != null && currentPlayedQuery.Length > 0
                ? string.Join(", ", currentPlayedQuery)
                : "None";

            var moveInput = CharacterControllerBaseInstantiated != null
                ? CharacterControllerBaseInstantiated.currentMoveInput
                : float3.zero;

            var facing = CharacterControllerBaseInstantiated != null
                ? CharacterControllerBaseInstantiated.currentForward
                : float3.zero;

            var selectedAnimationPath = GetCurrentAnimationPath();
            var selectedDatabaseName = GetCurrentDatabaseName();
            var currentRange = currentQueryFlow != null
                ? $"{currentQueryFlow.currentRange.featureIDStart}-{currentQueryFlow.currentRange.featureIDStop}"
                : "None";
            var expectedQuery = CharacterControllerBaseInstantiated?.DebugExpectedQuery ?? "None";
            var queryMismatch = IsQueryMismatch(expectedQuery);

            return
                $"Motion Matching\n" +
                $"Query: {queryText}\n" +
                $"Expected: {expectedQuery} {(queryMismatch ? "< MISMATCH" : string.Empty)}\n" +
                $"Reason: {CharacterControllerBaseInstantiated?.DebugQueryReason}\n" +
                $"Flow: {currentQueryFlow?.GetType().Name}\n" +
                $"Animation: {currentAnimationID} / Frame: {currentAnimationFrame}\n" +
                $"Clip: {selectedAnimationPath}\n" +
                $"Database: {selectedDatabaseName}\n" +
                $"Range: {currentRange} / Distance: {currentQueryFlow?.currentMinDistance:0.000}\n" +
                $"Velocity: {currentVelocity} ({math.length(currentVelocity):0.00})\n" +
                $"Input: {moveInput}\n" +
                $"Facing: {facing}\n" +
                $"Strafing: {CharacterControllerBaseInstantiated?.IsStrafing()}";
        }

        private bool IsQueryMismatch(string expectedQuery)
        {
            if (string.IsNullOrEmpty(expectedQuery) || expectedQuery == "None")
                return false;

            if (currentQueryFlow is ActionQueryComputedFlow)
            {
                return _currentQuery == null ||
                       _currentQuery.Length != 1 ||
                       _currentQuery[0] != expectedQuery;
            }

            if (currentPlayedQuery == null || currentPlayedQuery.Length == 0)
                return true;

            string[] expectedTags = expectedQuery
                .Split(',')
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrEmpty(tag))
                .ToArray();

            return expectedTags.Length != currentPlayedQuery.Length ||
                   expectedTags.Any(tag => !currentPlayedQuery.Contains(tag));
        }

        private string GetCurrentAnimationPath()
        {
            return dataset != null ? dataset.GetAnimationName(currentAnimationID) : "None";
        }

        private string GetCurrentDatabaseName()
        {
            return dataset != null
                ? dataset.GetDatabaseNameForAnimation(currentAnimationID)
                : "None";
        }

        private void ResetQueryAfterAction()
        {
            //Set regular query instead of action
            if (_currentQuery[0].Equals(Idle))
            {
                SetIdleQuery();
            }
            else
            {
                SetQuery(_currentQuery);
            }
            
            //Reset physics and collisions
            CharacterControllerBaseInstantiated.ToggleCollisionsAndPhysics(true, true);
            
        }

        private void CaptureRuntimePoseDebug(string stage)
        {
            if (!captureRuntimePoseDebug || debugSnapshot == null || currentQueryFlow == null || dataset == null)
                return;

            int featureID = currentQueryFlow.currentFeatureID;
            var features = currentQueryFlow.GetFeatures();
            FeatureData feature = featureID >= 0 && featureID < features.Count
                ? features[featureID]
                : default;

            int animationID = feature.animationID;
            int animationFrame = feature.animFrame;
            string clipName = dataset.GetAnimationName(animationID);

            int boneIndex = Mathf.Clamp(debugBoneIndex, 0, Math.Max(0, _bonesLength - 1));
            Transform boneTransform = _characterTransforms != null && boneIndex < _characterTransforms.Length
                ? _characterTransforms[boneIndex]
                : null;

            BoneData targetBone = default;
            bool hasTargetBone = false;
            if (animationID >= 0 &&
                animationID < dataset.animationsData.Count &&
                animationFrame >= 0 &&
                animationFrame < dataset.animationsData[animationID].Count &&
                dataset.animationsData[animationID][animationFrame].bonesData != null &&
                boneIndex < dataset.animationsData[animationID][animationFrame].bonesData.Length)
            {
                targetBone = dataset.animationsData[animationID][animationFrame].bonesData[boneIndex];
                hasTargetBone = true;
            }

            debugSnapshot.stage = stage;
            debugSnapshot.query = currentPlayedQuery != null ? string.Join(", ", currentPlayedQuery) : "None";
            debugSnapshot.expectedQuery = CharacterControllerBaseInstantiated?.DebugExpectedQuery ?? "None";
            debugSnapshot.flowType = currentQueryFlow.GetType().Name;
            debugSnapshot.currentFeatureID = featureID;
            debugSnapshot.animationID = animationID;
            debugSnapshot.animationFrame = animationFrame;
            debugSnapshot.clipName = clipName;
            debugSnapshot.databaseName = dataset.GetDatabaseNameForAnimation(animationID);
            debugSnapshot.rangeStart = currentQueryFlow.currentRange.featureIDStart;
            debugSnapshot.rangeStop = currentQueryFlow.currentRange.featureIDStop;
            debugSnapshot.distance = currentQueryFlow.currentMinDistance;
            debugSnapshot.distanceIsInvalid = float.IsNaN(currentQueryFlow.currentMinDistance) || float.IsInfinity(currentQueryFlow.currentMinDistance);
            debugSnapshot.delta = _delta;
            debugSnapshot.elapsedTime = currentQueryFlow.elapsedTime;
            debugSnapshot.isSearch = currentQueryFlow.isSearch;
            debugSnapshot.isQueryDone = currentQueryFlow.isQueryDone;
            debugSnapshot.moveInput = ToVector3(CharacterControllerBaseInstantiated?.currentMoveInput ?? float3.zero);
            debugSnapshot.facing = ToVector3(CharacterControllerBaseInstantiated?.currentForward ?? float3.zero);
            debugSnapshot.nextFutureOffset0 = HasElements(_nextPrediction.futureOffsets) ? ToVector3(_nextPrediction.futureOffsets[0]) : Vector3.zero;
            debugSnapshot.nextFutureDirection0 = HasElements(_nextPrediction.futureOffsetDirections) ? ToVector3(_nextPrediction.futureOffsetDirections[0]) : Vector3.zero;

            debugSnapshot.boneIndex = boneIndex;
            debugSnapshot.boneName = boneTransform != null ? boneTransform.name : "Missing";
            debugSnapshot.hasRuntimeBone = boneTransform != null;
            debugSnapshot.hasTargetBone = hasTargetBone && targetBone.isValid;
            debugSnapshot.appliesPosition = ShouldApplyBonePosition(boneIndex);
            debugSnapshot.appliesRotation = hasTargetBone && targetBone.isValid;
            debugSnapshot.targetLocalPosition = hasTargetBone ? ToVector3(targetBone.localPosition) : Vector3.zero;
            debugSnapshot.blendStartLocalPosition = HasIndex(_blendBoundaries.startPositionValues, boneIndex) ? ToVector3(_blendBoundaries.startPositionValues[boneIndex]) : Vector3.zero;
            debugSnapshot.blendEndLocalPosition = HasIndex(_blendBoundaries.endPositionValues, boneIndex) ? ToVector3(_blendBoundaries.endPositionValues[boneIndex]) : Vector3.zero;
            debugSnapshot.blendedLocalPosition = HasIndex(_blendingResults.bonesPosition, boneIndex) ? ToVector3(_blendingResults.bonesPosition[boneIndex]) : Vector3.zero;
            debugSnapshot.targetRotation = hasTargetBone ? ToVector4(targetBone.rotation) : Vector4.zero;
            debugSnapshot.targetRotationInvalid = hasTargetBone && IsInvalid(targetBone.rotation);
            debugSnapshot.startRotation = HasIndex(_blendBoundaries.startRotationValues, boneIndex) ? ToVector4(_blendBoundaries.startRotationValues[boneIndex]) : Vector4.zero;
            debugSnapshot.endRotation = HasIndex(_blendBoundaries.endRotationValues, boneIndex) ? ToVector4(_blendBoundaries.endRotationValues[boneIndex]) : Vector4.zero;
            debugSnapshot.blendedRotation = HasIndex(_blendingResults.bonesRotation, boneIndex) ? ToVector4(_blendingResults.bonesRotation[boneIndex]) : Vector4.zero;
            debugSnapshot.finalRotation = HasIndex(_boneRotationResults, boneIndex) ? ToVector4(_boneRotationResults[boneIndex]) : Vector4.zero;
            debugSnapshot.finalRotationInvalid = HasIndex(_boneRotationResults, boneIndex) && IsInvalid(_boneRotationResults[boneIndex]);
            debugSnapshot.finalLocalPosition = HasIndex(_bonePositionResults, boneIndex) ? ToVector3(_bonePositionResults[boneIndex]) : Vector3.zero;
            debugSnapshot.runtimeLocalPosition = boneTransform != null ? boneTransform.localPosition : Vector3.zero;
            debugSnapshot.runtimeLocalRotation = boneTransform != null ? ToVector4(boneTransform.localRotation) : Vector4.zero;
            debugSnapshot.runtimeWorldRotation = boneTransform != null ? ToVector4(boneTransform.rotation) : Vector4.zero;
            debugSnapshot.targetMinusRuntimeLocalPosition = debugSnapshot.targetLocalPosition - debugSnapshot.runtimeLocalPosition;
            debugSnapshot.finalMinusRuntimeLocalPosition = debugSnapshot.finalLocalPosition - debugSnapshot.runtimeLocalPosition;

            CaptureBoneDebugRows(animationID, animationFrame);
        }

        private void CaptureBoneDebugRows(int animationID, int animationFrame)
        {
            if (_characterTransforms == null || _bonesLength <= 0)
                return;

            int rowCount = Mathf.Clamp(debugMaxBoneRows, 1, _bonesLength);
            if (debugBoneRows == null || debugBoneRows.Length != rowCount)
            {
                debugBoneRows = new MotionMatchingBoneDebugRow[rowCount];
                for (int i = 0; i < debugBoneRows.Length; i++)
                {
                    debugBoneRows[i] = new MotionMatchingBoneDebugRow();
                }
            }

            for (int i = 0; i < rowCount; i++)
            {
                CaptureBoneDebugRow(debugBoneRows[i], i, animationID, animationFrame);
            }
        }

        private void CaptureBoneDebugRow(MotionMatchingBoneDebugRow row, int boneIndex, int animationID, int animationFrame)
        {
            Transform boneTransform = _characterTransforms != null && boneIndex < _characterTransforms.Length
                ? _characterTransforms[boneIndex]
                : null;

            bool hasTargetBone = TryGetDatasetBone(animationID, animationFrame, boneIndex, out BoneData targetBone);
            bool targetIsValid = hasTargetBone && targetBone.isValid;
            Vector3 targetLocalPosition = targetIsValid ? ToVector3(targetBone.localPosition) : Vector3.zero;
            Vector3 finalLocalPosition = HasIndex(_bonePositionResults, boneIndex) ? ToVector3(_bonePositionResults[boneIndex]) : Vector3.zero;
            Vector3 runtimeLocalPosition = boneTransform != null ? boneTransform.localPosition : Vector3.zero;

            row.boneIndex = boneIndex;
            row.boneName = boneTransform != null ? boneTransform.name : "Missing";
            row.hasRuntimeBone = boneTransform != null;
            row.hasTargetBone = targetIsValid;
            row.appliesPosition = ShouldApplyBonePosition(boneIndex);
            row.appliesRotation = targetIsValid;
            row.targetLocalPosition = targetLocalPosition;
            row.blendStartLocalPosition = HasIndex(_blendBoundaries.startPositionValues, boneIndex) ? ToVector3(_blendBoundaries.startPositionValues[boneIndex]) : Vector3.zero;
            row.blendEndLocalPosition = HasIndex(_blendBoundaries.endPositionValues, boneIndex) ? ToVector3(_blendBoundaries.endPositionValues[boneIndex]) : Vector3.zero;
            row.blendedLocalPosition = HasIndex(_blendingResults.bonesPosition, boneIndex) ? ToVector3(_blendingResults.bonesPosition[boneIndex]) : Vector3.zero;
            row.finalLocalPosition = finalLocalPosition;
            row.runtimeLocalPosition = runtimeLocalPosition;
            row.targetMinusRuntimeLocalPosition = targetLocalPosition - runtimeLocalPosition;
            row.finalMinusRuntimeLocalPosition = finalLocalPosition - runtimeLocalPosition;
            row.targetRotation = targetIsValid ? ToVector4(targetBone.rotation) : Vector4.zero;
            row.finalRotation = HasIndex(_boneRotationResults, boneIndex) ? ToVector4(_boneRotationResults[boneIndex]) : Vector4.zero;
            row.runtimeLocalRotation = boneTransform != null ? ToVector4(boneTransform.localRotation) : Vector4.zero;
            row.runtimeWorldRotation = boneTransform != null ? ToVector4(boneTransform.rotation) : Vector4.zero;
            row.targetRotationInvalid = targetIsValid && IsInvalid(targetBone.rotation);
            row.finalRotationInvalid = HasIndex(_boneRotationResults, boneIndex) && IsInvalid(_boneRotationResults[boneIndex]);
        }

        private bool TryGetDatasetBone(int animationID, int animationFrame, int boneIndex, out BoneData boneData)
        {
            boneData = default;
            if (dataset == null ||
                animationID < 0 ||
                animationID >= dataset.animationsData.Count ||
                animationFrame < 0 ||
                animationFrame >= dataset.animationsData[animationID].Count ||
                dataset.animationsData[animationID][animationFrame].bonesData == null ||
                boneIndex < 0 ||
                boneIndex >= dataset.animationsData[animationID][animationFrame].bonesData.Length)
            {
                return false;
            }

            boneData = dataset.animationsData[animationID][animationFrame].bonesData[boneIndex];
            return true;
        }

        private bool ShouldApplyBonePosition(int boneIndex)
        {
            int rootBone = avatar != null ? avatar.GetRootBone() : (int)HumanBodyBones.Hips;
            return boneIndex != rootBone ? wantApplyPositions : wantApplyRootBonePosition;
        }

        private static bool HasElements<T>(NativeArray<T> array) where T : struct
        {
            return array.IsCreated && array.Length > 0;
        }

        private static bool HasIndex<T>(NativeArray<T> array, int index) where T : struct
        {
            return array.IsCreated && index >= 0 && index < array.Length;
        }

        private static bool IsInvalid(quaternion value)
        {
            return math.isnan(value.value.x) || math.isnan(value.value.y) || math.isnan(value.value.z) || math.isnan(value.value.w) ||
                   math.isinf(value.value.x) || math.isinf(value.value.y) || math.isinf(value.value.z) || math.isinf(value.value.w);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Vector4 ToVector4(quaternion value)
        {
            return new Vector4(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        /// <summary>
        /// pose transition과 inertialization에 필요한 이전 bone 상태 버퍼를 생성합니다.
        /// </summary>
        private void InitializeMotionData(int bonesLength)
        {
            _motionData.Offsets = new NativeArray<OffsetBone>(bonesLength, Allocator.Persistent);
            _motionData.LastRotations = new NativeArray<quaternion>(bonesLength, Allocator.Persistent);
            _motionData.LastPositions = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            _motionData.LastVelocities = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            _motionData.LastScales = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            _motionData.LastVelocityScales = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            _motionData.AngularVelocities = new NativeArray<float3>(bonesLength, Allocator.Persistent);
            _motionData.PreviousPositions = new NativeArray<float3>(bonesLength, Allocator.Persistent);
        }

        /// <summary>
        /// runtime character의 기본 rotation과 Dataset bake 기준 rotation의 차이를 계산합니다.
        /// Humanoid 캐릭터는 이 차이를 통해 같은 baked pose를 다른 skeleton에 맞게 보정할 수 있습니다.
        /// </summary>
        private void InitializeInitialRotations()
        {
            avatar.GetOriginalAvatarRotations(out var originalCharacterRotations, out var defaultRotations,
                _characterTransforms, transform);
            ResetBoneRotations(_characterTransforms, defaultRotations);
            _originalDiffRotations =
                new NativeArray<quaternion>(originalCharacterRotations.Length, Allocator.Persistent);

            bool avatarIsHuman = avatar.avatar.isHuman;
            for (int i = 0; i < originalCharacterRotations.Length; i++)
            {
                if (avatarIsHuman)
                    _originalDiffRotations[i] = originalCharacterRotations[i]
                                                * Quaternion.Inverse(dataset.originalBonesRotation[i]);
                else
                    _originalDiffRotations[i] = quaternion.identity;
            }
        }

        /// <summary>
        /// pose blending과 pose search 결과 계산에 사용할 출력 버퍼를 생성합니다.
        /// </summary>
        private bool HasValidRuntimeDataset()
        {
            if (dataset == null || (avatar == null && dataset.avatar == null))
                return false;

            CustomAvatar targetAvatar = avatar != null ? avatar : dataset.avatar;
            if (targetAvatar == null || targetAvatar.avatar == null)
                return false;

            if (dataset.animationsDataTemp == null || dataset.animationsDataTemp.Count == 0)
                return false;

            if (dataset.originalBonesRotation == null || dataset.originalBonesRotation.Length < targetAvatar.Length)
            {
                Debug.LogWarning(
                    "[MotionMatching] PlayerMotionDataset is not baked for the current avatar yet. " +
                    "Run Tools > Motion Matching > Sample Animation > Bake Player Sample Database in Play Mode.",
                    this);
                return false;
            }

            QueriesComputed queries = dataset.queriesComputed;
            if (queries == null)
                return false;

            return HasQueryFeatures(queries.queries) ||
                   HasQueryFeatures(queries.idleQueries) ||
                   HasQueryFeatures(queries.actionQueries) ||
                   HasQueryFeatures(queries.loopActionQueries);
        }

        private static bool HasQueryFeatures<T>(IEnumerable<T> queries) where T : QueryComputed
        {
            return queries != null && queries.Any(query => query.featuresData != null && query.featuresData.Count > 0);
        }

        private void InitializeResults(int length)
        {
            _blendingResults = new BlendingResults
            {
                bonesPosition = new NativeArray<float3>(length, Allocator.Persistent),
                bonesRotation = new NativeArray<quaternion>(length, Allocator.Persistent),
                bonesScale = new NativeArray<float3>(length, Allocator.Persistent),
                rootPosition = float3.zero,
                rootRotation = quaternion.identity
            };

            _bonePositionResults = new NativeArray<float3>(length, Allocator.Persistent);
            _boneScaleResults = new NativeArray<float3>(length, Allocator.Persistent);
            _boneRotationResults = new NativeArray<quaternion>(length, Allocator.Persistent);

            _rootPositionBoundariesResults = new NativeArray<float3>(2, Allocator.Persistent);
            _rootRotationBoundariesResults = new NativeArray<quaternion>(2, Allocator.Persistent);
            _trajectoryFloat3Results = new NativeArray<float3>(4, Allocator.Persistent);
            _poseResult = new NativeArray<DistanceResult>(1, Allocator.Persistent);
        }

        /// <summary>
        /// blend 시작/목표 pose를 저장할 boundary 버퍼를 생성합니다.
        /// 새 pose가 선택될 때 이 버퍼에 시작값과 목표값을 채운 뒤 BlendingMethods로 보간합니다.
        /// </summary>
        private void InitializeBoundaries(int length)
        {
            _currentBoneDatas = new NativeArray<BoneData>(length, Allocator.Persistent);
            _nextBoneDatas = new NativeArray<BoneData>(length, Allocator.Persistent);
            _blendBoundaries = new BlendBoundaries
            {
                startRotationValues = new NativeArray<quaternion>(length, Allocator.Persistent),
                endRotationValues = new NativeArray<quaternion>(length, Allocator.Persistent),
                startPositionValues = new NativeArray<float3>(length, Allocator.Persistent),
                endPositionValues = new NativeArray<float3>(length, Allocator.Persistent),
                startScaleValues = new NativeArray<float3>(length, Allocator.Persistent),
                endScaleValues = new NativeArray<float3>(length, Allocator.Persistent),
                startRootPositionToBlend = float3.zero,
                endRootPositionToBlend = float3.zero,
                startRootRotationToBlend = quaternion.identity,
                endRootRotationToBlend = quaternion.identity,
            };
        }

        /// <summary>
        /// Avatar에서 계산한 기본 local rotation을 runtime character Transform에 적용합니다.
        /// 초기 pose 기준을 맞춘 뒤 현재 skeleton 값을 다시 캐시하기 위한 준비 단계입니다.
        /// </summary>
        private void ResetBoneRotations(Transform[] characterTransforms, quaternion[] rotations)
        {
            for (int i = 0; i < characterTransforms.Length; i++)
            {
                if (!characterTransforms[i]) continue;

                characterTransforms[i].localRotation = rotations[i];
            }
        }

        /// <summary>
        /// 현재 runtime skeleton Transform 값을 CurrentBoneTransformsValues에 갱신합니다.
        /// </summary>
        private void UpdateTransformsValues()
        {
            new GetCurrentTransformValues
            {
                boneTransformsValues = _currentBoneTransformsValues,
                boneIndices = _characterTransformBoneIndices
            }.Schedule(_characterTransformsNative).Complete();
        }

        private void CreateTransformAccessData(Transform[] characterTransforms)
        {
            var validTransforms = new List<Transform>(characterTransforms.Length);
            var validBoneIndices = new List<int>(characterTransforms.Length);
            for (int i = 0; i < characterTransforms.Length; i++)
            {
                if (characterTransforms[i] == null)
                    continue;

                validTransforms.Add(characterTransforms[i]);
                validBoneIndices.Add(i);
            }

            if (validTransforms.Count == 0)
            {
                Debug.LogError($"MotionMatching could not find any runtime bones for avatar '{avatar.name}' under '{name}'.");
                isRunning = false;
                _characterTransformsNative = new TransformAccessArray(Array.Empty<Transform>());
                _characterTransformBoneIndices = new NativeArray<int>(0, Allocator.Persistent);
                return;
            }

            _characterTransformsNative = new TransformAccessArray(validTransforms.ToArray());
            _characterTransformBoneIndices = new NativeArray<int>(validBoneIndices.Count, Allocator.Persistent);
            for (int i = 0; i < validBoneIndices.Count; i++)
                _characterTransformBoneIndices[i] = validBoneIndices[i];

            int missingCount = characterTransforms.Length - validTransforms.Count;
            if (missingCount > 0)
                Debug.Log($"MotionMatching mapped {validTransforms.Count}/{characterTransforms.Length} avatar bones for '{name}'. Missing optional bones: {missingCount}.");
        }

        /// <summary>
        /// character root 기준 world-to-local matrix를 갱신합니다.
        /// trajectory와 bone feature를 캐릭터 local-space로 변환할 때 사용합니다.
        /// </summary>
        private void UpdateModelValues()
        {
            _rtsModelInverse = MathUtils.CreateInverseModel(transform.position, transform.rotation);
        }

        /// <summary>
        /// 연결된 ExclusionMask가 현재 avatar 타입과 호환되는지 확인합니다.
        /// HumanoidAvatar에는 HumanoidExclusionMask만, GenericAvatar에는 같은 GenericAvatar를 참조하는 GenericExclusionMask만 허용합니다.
        /// </summary>
        private bool ExclusionMaskMatch(ExclusionMaskBase exclusionMaskToCHeck)
        {
            switch (exclusionMaskToCHeck)
            {
                case HumanoidExclusionMask when avatar is not HumanoidAvatar:
                    Debug.LogError("Exclusion mask is not same type as avatar");
                    return false;
                case GenericExclusionMask when avatar is not GenericAvatar:
                    Debug.LogError("Exclusion mask is not same type as avatar");
                    return false;
                case GenericExclusionMask genericExclusionMask when
                    avatar != genericExclusionMask.genericAvatar:
                    Debug.LogError("Generic Exclusion mask avatar and generic avatar don't match");
                    return false;
                default:
                    return true;
            }
        }
        
        private void ManageQueries()
        {
            _flows = new List<QueryComputedFlow>();
            dataset.queriesComputed.queries.ForEach(q =>
                _flows.Add(new MotionQueryComputedFlow(dataset, transform, _currentBoneTransformsValues, _characterTransformsNative, _characterTransformBoneIndices, _globalWeights, searchRate, animationSwitchPenalty)
                    .Also(m => m.Build(q, _bonesLength))));
            
            dataset.queriesComputed.actionQueries.ForEach(q =>
                _flows.Add(new ActionQueryComputedFlow(dataset, transform, _currentBoneTransformsValues, _characterTransformsNative, _characterTransformBoneIndices, _globalWeights, searchRate, animationSwitchPenalty)
                    .Also(m => m.Build(q, _bonesLength))));
            
            dataset.queriesComputed.loopActionQueries.ForEach(q =>
                _flows.Add(new LoopActionQueryComputedFlow(dataset, this, _currentBoneTransformsValues, _characterTransformsNative, _characterTransformBoneIndices, _globalWeights, searchRate, animationSwitchPenalty)
                    .Also(m => m.Build(q, _bonesLength))));

            dataset.queriesComputed.idleQueries.ForEach(q =>
                _flows.Add(new IdleQueryComputedFlow(dataset, transform, _currentBoneTransformsValues, _characterTransformsNative, _characterTransformBoneIndices, _globalWeights, searchRate, animationSwitchPenalty)
                    .Also(l => l.Build(q, _bonesLength))));
        }
        
        /// <summary>
        /// Call this method to set this Motion Matching instance in Idle query.
        /// This won't be executed if there is an action currently in progress.
        /// </summary>
        public bool SendIdleQuery(bool forceUpdate = false)
        {
            _currentQuery = new[]{Idle};
            if (currentQueryFlow is ActionQueryComputedFlow) return false;
            
            SetIdleQuery();

            if (!forceUpdate) return true;
            
            var prevRunning = isRunning;
            _isInertialized = false;
            isRunning = true;
            
            ManageMotionMatching();
            
            isRunning = prevRunning;
            _isInertialized = true;
            
            currentQueryFlow
                .SynchronizeTransforms(
                    _bonePositionResults, 
                    _boneScaleResults,
                    _boneRotationResults, 
                    _blendingResults,
                    CharacterControllerBaseInstantiated,
                    avatar.GetRootBone(),
                    IsDisabledRoot(),
                    wantApplyPositions,
                    wantApplyScales,
                    wantApplyRootBonePosition);
            UpdateTransformsValues();

            return true;
        }
        
        protected void SetQuery(string[] query)
        {
            _currentQuery = query;
            ChangeQueryComputedFlow(query);
        }

        /// <summary>
        /// CharacterControllerBase 구현체가 현재 입력 상태에 맞는 일반 이동 query를 요청할 때 사용하는 진입점입니다.
        /// 예를 들어 PlayerCharacterMotionController는 입력이 있으면 Walk/Run, 입력이 사라지면 Idle을 요청합니다.
        /// </summary>
        public void SendQuery(string query, params string[] values)
        {
            var finalQuery = values.Concat(new[] { query }).ToArray();
            _currentQuery = finalQuery;

            if (currentQueryFlow is ActionQueryComputedFlow actionQueryFlow &&
                !actionQueryFlow.TryInterrupt(finalQuery, true))
                return;

            SetQuery(finalQuery);
        }

        public bool SendActionQuery(string actionQuery, string followUpQuery)
        {
            var action = new[] { actionQuery };
            _currentQuery = new[] { followUpQuery };

            if (currentQueryFlow is ActionQueryComputedFlow actionQueryFlow &&
                !actionQueryFlow.TryInterrupt(action, false))
                return false;

            ChangeQueryComputedFlow(action);
            return true;
        }

        public bool HasQuery(string query)
        {
            return _flows != null && _flows.Any(flow =>
                flow.GetQueryComputed().query.Length == 1 &&
                flow.GetQueryComputed().query[0] == query);
        }

        public bool IsActionQueryPlaying()
        {
            return currentQueryFlow is ActionQueryComputedFlow { isQueryDone: false };
        }

        private void SetIdleQuery()
        {
            if (currentQueryFlow is IdleQueryComputedFlow) return;  //As we only have idle queries as loop, add this check
            
            ChangeQueryComputedFlow(new []{"Idle"});
            ((IdleQueryComputedFlow)currentQueryFlow).InitAnimationBaseIndexes();
        }

        private void ChangeQueryComputedFlow(string[] query)
        {
            currentQueryFlow = GetCurrentQueryComputedFlow(query);
            ResetRootEndBoundaries(transform.position, transform.rotation);
            currentQueryFlow.Reset();
            if (currentQueryFlow is ActionQueryComputedFlow actionQueryFlow)
            {
                actionQueryFlow.Initialize(
                    null,
                    actionQueryFlow.GetFeatures(),
                    dataset.animationsData,
                    CharacterControllerBaseInstantiated,
                    CollisionsPhysicsSetup.BothEnabled,
                    CollisionsPhysicsSetup.BothEnabled,
                    CollisionsPhysicsSetup.BothEnabled);
            }
            _delta = dataset.poseStep;
        }
        
        private QueryComputedFlow GetCurrentQueryComputedFlow(string[] query)
        {
            currentPlayedQuery = query;
            return FindQueryComputedFlowByName(query);
        }

        protected QueryComputedFlow FindQueryComputedFlowByName(string[] query)
        {
            return _flows.First(qc =>
            {
                return query.All(t => qc.GetQueryComputed().query.Contains(t)) && query.Length == qc.GetQueryComputed().query.Length;
            });
        }

        private void ResetRootEndBoundaries(Vector3 position, Quaternion rotation)
        {
            _blendBoundaries.endRootPositionToBlend = position;
            _blendBoundaries.endRootRotationToBlend = rotation;
        }
        
        private void ResetRootStartBoundaries(Vector3 position, Quaternion rotation)
        {
            _blendBoundaries.startRootPositionToBlend = position;
            _blendBoundaries.startRootRotationToBlend = rotation;
        }
        
        /// <summary>
        /// Call this method for ending the current Loop Action Query.
        /// This will have no effect if the current played query is not a LoopActionQuery.
        /// </summary>
        public void EndLoopQuery()
        {
            if (currentQueryFlow is not LoopActionQueryComputedFlow) return;
            
            ((LoopActionQueryComputedFlow)currentQueryFlow).EndLoopAction();
        }
        
        /// <summary>
        /// Set a new current exclusion mask into to this Motion Matching instance.
        /// </summary>
        /// <param name="exclusionMask"></param>
        public void SetExclusionMask(ExclusionMaskBase exclusionMask)
        {
            if (!ExclusionMaskMatch(exclusionMask))
            {
                return;
            }
            
            this.exclusionMask = exclusionMask;
            _characterTransforms = avatar.GetCharacterTransforms(transform, this.exclusionMask);
            if (_characterTransformsNative.isCreated)
                _characterTransformsNative.Dispose();
            if (_characterTransformBoneIndices.IsCreated)
                _characterTransformBoneIndices.Dispose();
            CreateTransformAccessData(_characterTransforms);
        }
        
        
        public ActionAnimationTimes GetActionAnimationTime()
        {
            return currentQueryFlow is not ActionQueryComputedFlow action ? default : action.GetAnimationTimes();
        }
        
        public ActionAnimationTimes GetActionAnimationTime(string query)
        {
            var queryComputed = _flows.First(qc => qc.GetQueryComputed().query.Contains(query));
            return queryComputed is not ActionQueryComputedFlow action ? default : action.GetAnimationTimes();
        }

    }
}

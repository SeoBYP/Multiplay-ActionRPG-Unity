using System;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Script.System.MotionSystemV2
{
    /// <summary>
    /// MotionMatchingComponent — 디버그 시각화 파트 (partial).
    /// V1 MotionMatching.cs의 OnDrawGizmos 스타일을 기준으로 구현.
    ///
    /// [Trajectory]
    ///   파란 구체 = 과거 샘플
    ///   초록 구체 = 미래 샘플 (이동 중)
    ///   노란 구체 = 미래 Ghost (정지 중 — forward 방향으로 항상 표시)
    ///   노란 화살표 = 각 샘플의 이동 방향
    ///
    /// [Velocity]
    ///   마젠타 화살표 = DesiredVelocity (입력 방향 × 속력)
    ///   시안 화살표   = Facing (캐릭터 forward)
    ///
    /// [Label]
    ///   Handles.Label — V1 BuildDebugLabel 스타일
    /// </summary>
    public sealed partial class MotionMatchingComponent
    {
        [Header("Debug")]
        [SerializeField] private bool _showTrajectory     = true;
        [SerializeField] private bool _showQueryBones     = true;
        [SerializeField] private bool _showVelocity       = true;
        [SerializeField] private bool _showBoneVelocities = false;

        [Tooltip("Gizmos 기준 높이 오프셋 (V1 debugDrawHeight 대응)")]
        [SerializeField] private float _debugDrawHeight = 0.08f;

        [Tooltip("정지 중 미래 Ghost 경로의 1초 당 시각 거리 (m). 실제 속력과 무관한 표시용")]
        [SerializeField] private float _ghostPreviewSpeed = 1.5f;

        // Inspector 표시용 — Runtime State / Blend
        [HideInInspector] public string DebugClipName   = "-";
        [HideInInspector] public float  DebugClipTime;
        [HideInInspector] public float  DebugBlendWeight;

        // Inspector 표시용 — Search Cost (RunSearch마다 갱신)
        [HideInInspector] public int    DbgBestPose      = -1;
        [HideInInspector] public string DbgBestClip      = "-";
        [HideInInspector] public float  DbgBestCost;
        [HideInInspector] public string DbgCurrClip      = "-";
        [HideInInspector] public float  DbgCurrCost;
        [HideInInspector] public float  DbgAppliedBias;
        [HideInInspector] public float  DbgDesiredSpeed;
        [HideInInspector] public string DbgDecision      = "-";   // SAME_POSE | BIAS_BLOCKED | SWITCHED | …

        // Inspector 표시용 — 채널별 Cost 분해 (선택 이유 판단)
        [HideInInspector] public string[] DbgChName;      // 채널 이름
        [HideInInspector] public float[]  DbgChBestCost;  // Best 포즈의 채널별 가중 cost
        [HideInInspector] public float[]  DbgChCurrCost;  // Current 포즈의 채널별 가중 cost
        [HideInInspector] public int[]    DbgChDim;        // 채널 feature 차원수
        [HideInInspector] public float[]  DbgChWeight;     // 정규화 가중치

        // 로그 제어
        private string _prevLogDecision = "-";
        private float  _logTimer;

        // ── CSV 파일 로거 ─────────────────────────────────────────────────────────
        [Header("File Logging")]
        [Tooltip("SWITCHED 이벤트를 CSV로 기록. Application.persistentDataPath/MM_PoseLog.csv에 저장")]
        [SerializeField] private bool _enableFileLog = false;

        private StreamWriter _csvWriter;
        private string       _csvPath;

#if UNITY_EDITOR
        // ── 색상 정의 (V1 스타일 대응) ───────────────────────────────────────────
        // V1: cyan = velocity, orange = moveInput, blue = facing
        // V2: 궤적에 맞게 확장
        private static readonly Color ColPast        = new(0.3f, 0.5f, 1f,   0.9f);  // 파랑  — 과거
        private static readonly Color ColFuture      = new(0.15f, 1f,  0.3f, 0.95f); // 초록  — 미래(이동)
        private static readonly Color ColGhost       = new(0.9f, 0.9f, 0.1f, 0.55f); // 노랑  — 미래(정지 ghost)
        private static readonly Color ColTrajDir     = new(1f,   0.85f,0.0f, 0.85f); // 황금  — 방향 화살표
        private static readonly Color ColBone        = new(1f,   0.4f, 0.1f, 0.9f);  // 주황  — 본 위치
        private static readonly Color ColDesiredVel  = new(1f,   0f,   1f,   0.95f); // 마젠타 — DesiredVelocity
        private static readonly Color ColFacing      = new(0.1f, 0.8f, 1f,   0.9f);  // 시안   — Facing
        private static readonly Color ColBoneVel     = new(1f,   0.6f, 0f,   0.85f); // 주황   — 본 속도

        // ── 진입점 ───────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            UpdateDebugFields();

            var drawOrigin = transform.position + Vector3.up * _debugDrawHeight;

            if (_showTrajectory)     DrawTrajectoryGizmos(drawOrigin);
            if (_showQueryBones)     DrawQueryBoneGizmos();
            if (_showVelocity)       DrawVelocityGizmos(drawOrigin);
            if (_showBoneVelocities) DrawBoneVelocityGizmos();
        }

        // ── 필드 갱신 ─────────────────────────────────────────────────────────────

        private void UpdateDebugFields()
        {
            if (_currentEntry != null)
            {
                // clipDisplayName(FBX 파일명) 우선, 없으면 clip.name fallback
                DebugClipName = !string.IsNullOrEmpty(_currentEntry.clipDisplayName)
                    ? _currentEntry.clipDisplayName
                    : (_currentEntry.clip != null ? _currentEntry.clip.name : "-");
                DebugClipTime = _activePlayable.IsValid() ? (float)_activePlayable.GetTime() : 0f;
            }
            DebugBlendWeight = _inertia.BlendWeight;
        }

        // ── 궤적 시각화 ───────────────────────────────────────────────────────────

        private void DrawTrajectoryGizmos(Vector3 drawOrigin)
        {
            if (_trajectory == null) return;
            var history = _trajectory.History;
            if (!history.IsCreated || history.Length == 0) return;

            Vector3    rootPos     = transform.position;
            Quaternion rootRot     = transform.rotation;
            bool       isMoving    = math.lengthsq(DesiredVelocity) > 0.01f;
            Vector3    prevWorld   = rootPos;

            for (int i = 0; i < history.Length; i++)
            {
                var  sample   = history[i];
                bool isFuture = sample.timeOffset > 0.001f;

                Vector3 localPos = new(sample.position.x, 0f, sample.position.z);
                Vector3 worldPos;
                bool    isGhost = false;

                if (isFuture && !isMoving)
                {
                    // 정지 중 미래: forward 방향으로 Ghost 표시 (timeOffset × ghostPreviewSpeed)
                    // → 움직임 여부와 관계없이 항상 미래 경로가 Scene뷰에 표시됨
                    worldPos = rootPos + transform.forward * (sample.timeOffset * _ghostPreviewSpeed);
                    isGhost  = true;
                }
                else
                {
                    worldPos = rootPos + rootRot * localPos;
                }

                Vector3 localDir = new(sample.direction.x, 0f, sample.direction.z);
                Vector3 worldDir = rootRot * localDir;

                // 구체 색상: 과거=파랑, 미래=초록, Ghost=노랑
                Gizmos.color = isFuture
                    ? (isGhost ? ColGhost : ColFuture)
                    : ColPast;

                float sphereR = isFuture ? 0.06f : 0.045f;
                Gizmos.DrawSphere(worldPos, sphereR);

                // 방향 화살표
                Gizmos.color = isGhost
                    ? new Color(ColGhost.r, ColGhost.g, ColGhost.b, 0.7f)
                    : ColTrajDir;
                DrawArrow(worldPos, worldDir * 0.28f);

                // 연결선
                if (i > 0)
                {
                    Color lineCol = isFuture
                        ? (isGhost ? new Color(ColGhost.r,  ColGhost.g,  ColGhost.b,  0.35f)
                                   : new Color(ColFuture.r, ColFuture.g, ColFuture.b, 0.45f))
                        : new Color(ColPast.r, ColPast.g, ColPast.b, 0.4f);
                    Gizmos.color = lineCol;
                    Gizmos.DrawLine(prevWorld, worldPos);
                }
                prevWorld = worldPos;
            }

            // 현재 위치 마커 (V1의 origin circle)
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(rootPos, 0.09f);
        }

        // ── 속도 / 방향 시각화 (V1 debugVelocity + debugInputVectors 대응) ─────────

        private void DrawVelocityGizmos(Vector3 drawOrigin)
        {
            float3 desiredVel = DesiredVelocity;
            float  speed      = math.length(desiredVel);

            // DesiredVelocity 화살표 (마젠타) — V1 debugVelocity
            if (speed > 0.01f)
            {
                Vector3 velVec = new(desiredVel.x, 0f, desiredVel.z);
                Gizmos.color = ColDesiredVel;
                DrawArrow(drawOrigin, velVec);

                Handles.color = ColDesiredVel;
                Handles.Label(drawOrigin + velVec + Vector3.up * 0.15f, $"{speed:F1} m/s");
            }

            // Facing 방향 화살표 (시안) — V1 debugInputVectors의 currentForward
            // 입력 유무 관계없이 항상 표시
            Gizmos.color = ColFacing;
            DrawArrow(drawOrigin + Vector3.up * 0.08f, transform.forward * 0.7f);
        }

        // ── 쿼리 본 위치 시각화 ───────────────────────────────────────────────────

        private void DrawQueryBoneGizmos()
        {
            if (_boneState == null) return;
            var positions = _boneState.Positions;
            if (!positions.IsCreated) return;

            Vector3    rootPos = transform.position;
            Quaternion rootRot = transform.rotation;

            Gizmos.color = ColBone;
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var localPos = positions[(int)bone];
                if (math.lengthsq(localPos) < 0.0001f) continue;

                Vector3 worldPos = rootPos + rootRot * new Vector3(localPos.x, localPos.y, localPos.z);
                Gizmos.DrawSphere(worldPos, 0.025f);
            }
        }

        // ── 본 속도 시각화 ────────────────────────────────────────────────────────

        private void DrawBoneVelocityGizmos()
        {
            if (_boneState == null) return;
            var positions  = _boneState.Positions;
            var velocities = _boneState.Velocities;
            if (!positions.IsCreated || !velocities.IsCreated) return;

            Vector3    rootPos = transform.position;
            Quaternion rootRot = transform.rotation;

            Gizmos.color = ColBoneVel;
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                int idx = (int)bone;

                var localPos = positions[idx];
                var localVel = velocities[idx];
                if (math.lengthsq(localPos) < 0.0001f) continue;
                if (math.lengthsq(localVel) < 0.0001f) continue;

                Vector3 worldPos = rootPos + rootRot * new Vector3(localPos.x, localPos.y, localPos.z);
                Vector3 worldVel = rootRot * new Vector3(localVel.x, localVel.y, localVel.z);
                DrawArrow(worldPos, worldVel * 0.15f);
            }
        }
        // ── CSV 파일 로거 메서드 ──────────────────────────────────────────────────

        /// <summary>
        /// Awake에서 호출. _enableFileLog가 true면 CSV 파일을 열고 헤더를 쓴다.
        /// </summary>
        internal void InitFileLog()
        {
            if (!_enableFileLog) return;

            _csvPath = Path.Combine(Application.persistentDataPath, "MM_PoseLog.csv");
            try
            {
                _csvWriter = new StreamWriter(_csvPath, append: false, encoding: Encoding.UTF8);
                _csvWriter.WriteLine(
                    "FixedTime,Frame,Decision," +
                    "PrevClip,PrevClipTime," +
                    "NewClip,NewClipTime," +
                    "DesiredVelX,DesiredVelY,DesiredVelZ,DesiredSpeed," +
                    "BestCost,CurrCost,AppliedBias," +
                    "PoseIndex,EntryFirstPose,EntryLastPose");
                _csvWriter.Flush();
                Debug.Log($"[MM FileLog] 기록 시작 → {_csvPath}", this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MM FileLog] 파일 열기 실패: {e.Message}", this);
                _csvWriter = null;
            }
        }

        /// <summary>
        /// OnDestroy에서 호출. 파일을 닫는다.
        /// </summary>
        internal void DisposeFileLog()
        {
            _csvWriter?.Flush();
            _csvWriter?.Close();
            _csvWriter = null;
        }

        /// <summary>
        /// ApplyPose 직전에 호출. SWITCHED 이벤트를 CSV에 기록한다.
        /// </summary>
        internal void WriteLogLine(
            string prevClip, float prevClipTime,
            string newClip,  float newClipTime,
            string decision,
            float bestCost, float currCost, float bias)
        {
            if (_csvWriter == null) return;

            float3 vel = DesiredVelocity;
            try
            {
                _csvWriter.WriteLine(
                    $"{Time.fixedTime:F4}," +
                    $"{Time.frameCount}," +
                    $"{decision}," +
                    $"{EscapeCsv(prevClip)},{prevClipTime:F4}," +
                    $"{EscapeCsv(newClip)},{newClipTime:F4}," +
                    $"{vel.x:F3},{vel.y:F3},{vel.z:F3},{math.length(vel):F3}," +
                    $"{bestCost:F5},{currCost:F5},{bias:F4}," +
                    $"{DbgBestPose},{_entryFirstPose},{_entryLastPose}");
                _csvWriter.Flush();
            }
            catch { /* 로그 실패는 게임플레이에 영향 없음 */ }
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Contains(',') ? $"\"{s}\"" : s;
        }

        // ── Search Cost 캡처 (RunSearch → Inspector 표시용) ──────────────────────

        /// <summary>
        /// RunSearch 내부에서 호출. Inspector의 Search Cost 패널에 표시할 스냅샷을 갱신한다.
        /// #if UNITY_EDITOR 블록이 아니므로 런타임에서도 동작하지만 비용이 거의 없다.
        /// </summary>
        internal void CaptureSearchDebug(
            NativeSlice<float> query,
            int bestPose,
            string decision,
            float bestCost    = 0f,
            float currentCost = 0f,
            float appliedBias = 0f)
        {
            DbgBestPose     = bestPose;
            DbgDecision     = decision;
            DbgAppliedBias  = appliedBias;
            DbgDesiredSpeed = math.length(DesiredVelocity);

            // best 클립 이름
            if (bestPose >= 0 && bestPose < _database.PoseCount)
            {
                var meta = _database.NativeMetadata[bestPose];
                if (meta.entryIndex >= 0 && meta.entryIndex < _database.entries.Count)
                {
                    var entry = _database.entries[meta.entryIndex];
                    DbgBestClip = !string.IsNullOrEmpty(entry.clipDisplayName)
                        ? entry.clipDisplayName : (entry.clip != null ? entry.clip.name : "?");
                }
                else
                {
                    DbgBestClip = $"[entryIndex {meta.entryIndex} out of range — Rebake needed]";
                }
            }
            else DbgBestClip = "-";

            // cost 값 — SAME_POSE나 INITIAL처럼 cost 계산을 안 한 케이스는 여기서 계산
            if (decision is "BIAS_BLOCKED" or "SWITCHED")
            {
                DbgBestCost = bestCost;
                DbgCurrCost = currentCost;
            }
            else if (_currentPoseIndex >= 0)
            {
                DbgBestCost = query.Length > 0 ? ComputeQueryCost(query, bestPose) : 0f;
                DbgCurrCost = query.Length > 0 ? ComputeQueryCost(query, _currentPoseIndex) : 0f;
            }

            // 현재 클립 이름
            DbgCurrClip = !string.IsNullOrEmpty(_currentEntry?.clipDisplayName)
                ? _currentEntry.clipDisplayName
                : (_currentEntry?.clip != null ? _currentEntry.clip.name : "-");

            // ── 채널별 Cost 분해 계산 ────────────────────────────────────────────
            // 어떤 채널 때문에 이 포즈가 선택됐는지 Inspector에 표시한다.
            if (_database != null && bestPose >= 0)
            {
                var schema   = _database.schema;
                var db       = _database.NativeFeatures;
                int featDim  = schema.TotalFeatureDimension;
                int chCount  = schema.Channels.Count;

                if (DbgChName == null || DbgChName.Length != chCount)
                {
                    DbgChName     = new string[chCount];
                    DbgChBestCost = new float[chCount];
                    DbgChCurrCost = new float[chCount];
                    DbgChDim      = new int[chCount];
                    DbgChWeight   = new float[chCount];
                }

                for (int c = 0; c < chCount; c++)
                {
                    int   offset  = schema.GetChannelOffset(c);
                    int   dim     = schema.Channels[c].FeatureDimension;
                    float w       = schema.GetNormalizedWeight(c);
                    int   bestBase = bestPose       * featDim + offset;
                    int   currBase = (_currentPoseIndex >= 0) ? _currentPoseIndex * featDim + offset : -1;

                    DbgChName[c]   = schema.Channels[c].GetType().Name;
                    DbgChDim[c]    = dim;
                    DbgChWeight[c] = w;

                    float bestSq = 0f, currSq = 0f;
                    for (int j = 0; j < dim; j++)
                    {
                        float dbBest = db[bestBase + j];
                        float q      = (j < query.Length) ? query[offset + j] : 0f;
                        float dBest  = q - dbBest;
                        bestSq += dBest * dBest;

                        if (currBase >= 0)
                        {
                            float dCurr = q - db[currBase + j];
                            currSq += dCurr * dCurr;
                        }
                    }
                    DbgChBestCost[c] = bestSq * w;
                    DbgChCurrCost[c] = currSq * w;
                }
            }

            EmitSearchLog(decision);
        }

        /// <summary>
        /// 결정이 바뀔 때 즉시 + 3초마다 주기적으로 Console 로그를 출력한다.
        /// </summary>
        private void EmitSearchLog(string decision)
        {
            _logTimer -= Time.fixedDeltaTime;
            bool decisionChanged = decision != _prevLogDecision;
            if (!decisionChanged && _logTimer > 0f) return;

            _prevLogDecision = decision;
            _logTimer = 3f;

            // Trajectory 미래 샘플 로컬 위치 수집
            var trajSb = new StringBuilder();
            if (_trajectory != null)
            {
                var history = _trajectory.History;
                for (int i = 0; i < history.Length; i++)
                {
                    var s = history[i];
                    if (s.timeOffset >= 0f)   // 현재·미래만
                        trajSb.Append($"  t={s.timeOffset:+0.0;-0.0}: pos=({s.position.x:F2},{s.position.z:F2})\n");
                }
            }

            string prefix = decision switch
            {
                "SWITCHED" or "SWITCHED_NO_BIAS" or "INITIAL" => "<color=#44FF66>[MM] ✅ SWITCHED</color>",
                "BIAS_BLOCKED"                                 => "<color=#FF8800>[MM] ⚠ BIAS_BLOCKED</color>",
                "SAME_POSE"                                    => "<color=#AAAAAA>[MM] — SAME_POSE</color>",
                _                                              => $"[MM] {decision}"
            };

            string biasDetail = decision is "BIAS_BLOCKED" or "SWITCHED"
                ? $"  bestCost({DbgBestCost:F4}) vs currCost({DbgCurrCost:F4}) + bias({DbgAppliedBias:F3}) = threshold({DbgCurrCost + DbgAppliedBias:F4})\n"
                : "";

            // 채널별 DB 피처 차이 (Best 포즈 vs Curr 포즈)
            // ── DB 원본 피처 값 확인 ─────────────────────────────────────────────────
            if (_database != null && DbgBestPose >= 0 && _currentPoseIndex >= 0)
            {
                var dbArr   = _database.NativeFeatures;
                int featDim = _database.schema.TotalFeatureDimension;
                var rawSb   = new StringBuilder();
                int printLen = Math.Min(featDim, 16);

                rawSb.Append($"[Raw DB features — first {printLen} of {featDim} floats]\n");
                rawSb.Append($"  Pose[{_currentPoseIndex}] curr: ");
                for (int j = 0; j < printLen; j++)
                    rawSb.Append($"{dbArr[_currentPoseIndex * featDim + j]:F3} ");
                rawSb.Append($"\n  Pose[{DbgBestPose}] best: ");
                for (int j = 0; j < printLen; j++)
                    rawSb.Append($"{dbArr[DbgBestPose * featDim + j]:F3} ");

                Debug.Log(rawSb.ToString(), this);
            }

            var chanSb = new StringBuilder();
            if (_database != null && DbgBestPose >= 0 && _currentPoseIndex >= 0)
            {
                var schema  = _database.schema;
                var db      = _database.NativeFeatures;
                int featDim = schema.TotalFeatureDimension;

                for (int c = 0; c < schema.Channels.Count; c++)
                {
                    int   chOffset = schema.GetChannelOffset(c);
                    int   chDim    = schema.Channels[c].FeatureDimension;
                    float chW      = schema.GetNormalizedWeight(c);
                    int   bBase    = DbgBestPose       * featDim + chOffset;
                    int   cBase    = _currentPoseIndex * featDim + chOffset;

                    float diffSq = 0f;
                    for (int j = 0; j < chDim; j++)
                    {
                        float d = db[bBase + j] - db[cBase + j];
                        diffSq += d * d;
                    }
                    chanSb.Append(
                        $"  ch[{c}] {schema.Channels[c].GetType().Name,-22}" +
                        $"  dim={chDim,2}  w={chW:F3}  |best-curr|²={diffSq:F5}\n");
                }
            }

            // ── Bone Y 위치 (루트 로컬 스페이스) ────────────────────────────────────
            var boneSb = new StringBuilder();
            if (_boneState != null)
            {
                var bpos = _boneState.Positions;
                if (bpos.IsCreated)
                {
                    // 진단에 유용한 주요 본만 출력
                    (HumanBodyBones bone, string label)[] targets =
                    {
                        (HumanBodyBones.LeftFoot,       "LFoot "),
                        (HumanBodyBones.RightFoot,      "RFoot "),
                        (HumanBodyBones.LeftLowerLeg,   "LShank"),
                        (HumanBodyBones.RightLowerLeg,  "RShank"),
                        (HumanBodyBones.LeftUpperLeg,   "LThigh"),
                        (HumanBodyBones.RightUpperLeg,  "RThigh"),
                        (HumanBodyBones.Hips,           "Hips  "),
                    };
                    foreach (var (bone, label) in targets)
                    {
                        int idx = (int)bone;
                        if (idx < bpos.Length)
                            boneSb.Append($"  {label} Y={bpos[idx].y:F4}\n");
                    }
                }
            }

            Debug.Log(
                $"{prefix}  DesiredSpeed={DbgDesiredSpeed:F2}\n" +
                $"  Best : [{DbgBestPose}] {DbgBestClip}  cost={DbgBestCost:F4}\n" +
                $"  Curr : {DbgCurrClip}  cost={DbgCurrCost:F4}\n" +
                biasDetail +
                $"[Channel diff  Best vs Curr DB features]\n{chanSb}" +
                $"[Trajectory future samples (local)]\n{trajSb}" +
                $"[Bone Y — local space]\n{boneSb}",
                this);
        }

        // ── 화살표 유틸 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 현재 Gizmos.color로 시작점→끝점 화살표(촉 포함)를 그린다.
        /// </summary>
        private static void DrawArrow(Vector3 from, Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;

            Vector3 to = from + dir;
            Gizmos.DrawLine(from, to);

            // 화살촉: 진행 방향에 수직인 두 선
            Vector3 right = Vector3.Cross(dir.normalized, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            float headLen = Mathf.Min(dir.magnitude * 0.28f, 0.14f);
            Gizmos.DrawLine(to, to - dir.normalized * headLen + right * (headLen * 0.45f));
            Gizmos.DrawLine(to, to - dir.normalized * headLen - right * (headLen * 0.45f));
        }
#endif
    }
}

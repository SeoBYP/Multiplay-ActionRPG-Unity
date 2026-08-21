using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 손·발 IK — <b>애니메이션을 최대한 살리고 어긋난 축만 고친다</b>.
    /// Animator 가 붙은 GameObject 에 부착한다(<c>OnAnimatorIK</c> 는 거기서만 호출된다).
    ///
    /// <b>무엇을 고치고 무엇을 두는가</b>(사용자 피드백 반영):
    ///   · <b>좌우 간격은 클립 그대로 둔다</b> — 손 사이 폭을 고정 기둥 위치로 덮어쓰면 애니의 팔 간격이 무너진다.
    ///   · <b>깊이(사다리 면까지)와 높이(가장 가까운 발판)만</b> 보정한다 — 실제로 어긋나는 축은 이 둘뿐이다.
    ///     (클립은 제작 기준 간격을 가정하는데 이 사다리 발판 간격은 0.6m 라 높이가 어긋난다.)
    ///
    /// <b>언제 거는가</b>: "팔을 뻗어 사다리에 닿는 순간"만. 클립이 만든 손 위치가 보정 목표에서
    /// <see cref="m_contactRadius"/> 안으로 들어오면 가중치가 올라가고, 멀면(뻗는 중·당기는 중) 0 이다.
    /// 즉 <b>붙잡는 순간에만 살짝 스냅</b>하고 나머지 동작은 클립이 그대로 보인다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class LadderIK : MonoBehaviour
    {
        [Tooltip("사다리 IK 사용 여부. 끄면 클립 그대로 재생한다.")]
        [SerializeField] private bool m_enabled = true;

        [Tooltip("손이 사다리에 닿았다고 볼 거리(m). 클립 손 위치가 보정 목표에서 이 안이면 스냅한다.")]
        [SerializeField] private float m_contactRadius = 0.28f;

        [Tooltip("닿았을 때의 손 IK 최대 가중치.")]
        [Range(0f, 1f)][SerializeField] private float m_handWeight = 0.8f;

        [Tooltip("닿았을 때의 발 IK 최대 가중치.")]
        [Range(0f, 1f)][SerializeField] private float m_footWeight = 0.8f;

        [Tooltip("손이 기둥을 감싸는 깊이(m). 사다리 면(중심)에서 캐릭터 쪽으로 이만큼 앞에 손이 온다.")]
        [SerializeField] private float m_handGripDepth = 0.06f;

        [Tooltip("발이 발판을 딛는 깊이(m). 손보다 조금 더 앞(캐릭터 쪽)이 자연스럽다.")]
        [SerializeField] private float m_footGripDepth = 0.12f;

        [Tooltip("가중치 변화 속도(1/초). 스냅이 툭 걸리지 않게 한다.")]
        [SerializeField] private float m_weightBlendRate = 10f;

        private static readonly AvatarIKGoal[] Goals =
        {
            AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot,
        };

        private Animator _animator;
        private ClimbSensor _sensor;
        private Transform _root;
        private readonly float[] _weights = new float[4];

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _sensor = GetComponentInParent<ClimbSensor>();
            _root = transform.root;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!m_enabled || _animator == null || _sensor == null) return;

            var ladder = _sensor.Current;
            if (ladder == null)
            {
                for (int i = 0; i < Goals.Length; i++)
                {
                    _weights[i] = 0f;
                    _animator.SetIKPositionWeight(Goals[i], 0f);
                }
                return;
            }

            ladder.GetFaceAxes(out Vector3 faceNormal, out Vector3 sideAxis);
            // 캐릭터가 매달린 쪽이 +가 되도록 면 법선을 맞춘다(반대편 면에서도 부호가 뒤집히지 않게).
            faceNormal = ladder.GetApproachSide(_root.position);

            Vector3 center = ladder.CenterXZ;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            for (int i = 0; i < Goals.Length; i++)
            {
                bool isHand = i < 2;
                Vector3 clipPos = _animator.GetIKPosition(Goals[i]); // 클립이 만든 현재 손/발 위치

                float depth = isHand ? m_handGripDepth : m_footGripDepth;
                Vector3 target = ResolveGrip(ladder, clipPos, faceNormal, sideAxis, depth);

                // ── 접촉 판정: 클립 손이 목표 근처일 때만(=뻗어서 닿는 순간) 스냅한다.
                float contact = ContactWeight(Vector3.Distance(clipPos, target), m_contactRadius);
                _weights[i] = Mathf.MoveTowards(_weights[i], contact, m_weightBlendRate * dt);

                float weight = _weights[i] * (isHand ? m_handWeight : m_footWeight);
                _animator.SetIKPositionWeight(Goals[i], weight);
                if (weight > 0.001f)
                    _animator.SetIKPosition(Goals[i], target);
            }
        }

        /// <summary>
        /// 보정 목표 — <b>좌우(팔 간격)는 클립 값을 그대로 두고</b> 깊이(사다리 면)와 높이(가장 가까운 발판)만 바꾼다.
        /// 순수 계산이라 테스트로 고정한다.
        /// </summary>
        public static Vector3 ResolveGrip(Ladder ladder, Vector3 clipPos, Vector3 faceNormal, Vector3 sideAxis, float depth)
        {
            Vector3 center = ladder.CenterXZ;
            Vector3 fromCenter = clipPos - new Vector3(center.x, clipPos.y, center.z);
            float side = Vector3.Dot(fromCenter, sideAxis); // 애니의 팔 간격 → 유지
            float rungY = ladder.GetNearestRungY(clipPos.y);

            return new Vector3(center.x, rungY, center.z) + sideAxis * side + faceNormal * depth;
        }

        /// <summary>접촉 가중치 — 목표에서 <paramref name="radius"/> 밖이면 0(뻗는 중), 가까울수록 1.</summary>
        public static float ContactWeight(float distance, float radius)
            => 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, radius));
    }
}

using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 손·발 IK — 오르는 동안 <b>지금 붙잡고 있는 손·발만</b> 실제 발판/기둥에 스냅한다.
    /// Animator 가 붙은 GameObject 에 부착한다(<c>OnAnimatorIK</c> 는 거기서만 호출된다).
    ///
    /// <b>왜 필요한가</b>: 사다리 클립은 제작 기준 간격을 가정하는데 실제 발판 간격은 에셋마다 다르다(이 프로젝트 0.6m).
    /// 배속 보정으로 <i>속도</i>는 맞아도 <i>높이</i>는 어긋나 손발이 가로대 사이 허공을 짚는다.
    ///
    /// <b>왜 "항상"이 아니라 접지 구간만인가</b>: 오르는 동작은 한쪽 손발이 <b>붙잡고 있는 동안</b> 다른 쪽이 다음 칸으로 <b>뻗는다</b>.
    /// 네 팔다리를 늘 고정하면 뻗는 동작이 죽어 사다리를 기어오르는 게 아니라 매달려 미끄러지는 그림이 된다.
    /// → 클립을 그대로 관찰해서 판단한다: <b>붙잡은 팔다리는 월드에서 거의 멈춰 있고</b>(몸만 올라간다),
    ///   뻗는 팔다리는 몸보다 빠르게 위로 이동한다. 그 속도차로 팔다리별 가중치를 만든다(별도 커브 저작 불필요).
    ///
    /// <b>좌우</b>는 캐릭터 기준으로 정한다 — 사다리 고정 축을 쓰면 반대편 면에 붙었을 때 좌우가 뒤집힌다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class LadderIK : MonoBehaviour
    {
        [Tooltip("사다리 IK 사용 여부. 끄면 클립 그대로 재생한다.")]
        [SerializeField] private bool m_enabled = true;

        [Tooltip("붙잡고 있을 때의 손 IK 최대 가중치(0=클립 그대로, 1=완전 고정).")]
        [Range(0f, 1f)][SerializeField] private float m_handWeight = 0.8f;

        [Tooltip("딛고 있을 때의 발 IK 최대 가중치.")]
        [Range(0f, 1f)][SerializeField] private float m_footWeight = 0.8f;

        [Tooltip("손이 잡는 지점이 골반보다 얼마나 위인지(m).")]
        [SerializeField] private float m_handHeightBias = 0.55f;

        [Tooltip("발이 딛는 지점이 골반보다 얼마나 아래인지(m).")]
        [SerializeField] private float m_footHeightBias = -0.85f;

        [Tooltip("가중치 변화 속도(1/초). 접지↔뻗기 전환이 툭 끊기지 않게 한다.")]
        [SerializeField] private float m_weightBlendRate = 12f;

        [Tooltip("몸 속도 대비 이 배 이상 빠르게 움직이면 '뻗는 중'으로 보고 IK 를 푼다.")]
        [SerializeField] private float m_reachSpeedRatio = 1.4f;

        private static readonly AvatarIKGoal[] Goals =
        {
            AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot,
        };

        private Animator _animator;
        private ClimbSensor _sensor;
        private Transform _root;

        private readonly float[] _weights = new float[4];   // 현재 가중치(부드럽게 수렴)
        private readonly Vector3[] _lastLimbPos = new Vector3[4];
        private bool _tracking;
        private float _lastRootY;

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
                for (int i = 0; i < Goals.Length; i++) { _weights[i] = 0f; _animator.SetIKPositionWeight(Goals[i], 0f); }
                _tracking = false;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float bodySpeed = Mathf.Abs((_root.position.y - _lastRootY) / dt);
            _lastRootY = _root.position.y;

            Vector3 characterRight = _root.right;
            float pelvisY = _animator.bodyPosition.y;
            float handY = ladder.GetNearestRungY(pelvisY + m_handHeightBias);
            float footY = ladder.GetNearestRungY(pelvisY + m_footHeightBias);

            for (int i = 0; i < Goals.Length; i++)
            {
                bool isHand = i < 2;
                bool isRight = (i % 2) == 1;

                Vector3 limbPos = _animator.GetIKPosition(Goals[i]); // 클립이 만든 현재 손/발 위치
                float limbSpeed = _tracking ? Mathf.Abs((limbPos.y - _lastLimbPos[i].y) / dt) : 0f;
                _lastLimbPos[i] = limbPos;

                // 붙잡음 = 몸보다 느리게 움직임(월드에서 거의 정지). 뻗음 = 몸보다 빠르게 위/아래로 이동.
                float target = 1f;
                if (bodySpeed > 0.05f)
                {
                    float ratio = limbSpeed / Mathf.Max(0.01f, bodySpeed);
                    target = 1f - Mathf.Clamp01((ratio - 1f) / Mathf.Max(0.01f, m_reachSpeedRatio - 1f));
                }
                _weights[i] = Mathf.MoveTowards(_weights[i], target, m_weightBlendRate * dt);

                float max = isHand ? m_handWeight : m_footWeight;
                float weight = _weights[i] * max;
                _animator.SetIKPositionWeight(Goals[i], weight);
                if (weight > 0.001f)
                    _animator.SetIKPosition(Goals[i], ladder.GetGripPoint(isHand ? handY : footY, characterRight, isRight));
            }

            _tracking = true;
        }
    }
}

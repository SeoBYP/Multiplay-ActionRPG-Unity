using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 손·발 IK — 오르는 동안 손을 기둥에, 발을 <b>가장 가까운 발판</b>에 붙인다.
    /// Animator 가 붙은 GameObject 에 부착한다(<c>OnAnimatorIK</c> 는 거기서만 호출된다).
    ///
    /// <b>왜 필요한가</b>: 사다리 클립은 특정 간격(제작 기준)을 가정하고 만들어졌는데, 실제 사다리의 발판 간격은
    /// 에셋마다 다르다(이 프로젝트 실측 0.6m). 배속 보정으로 <i>속도</i>는 맞췄어도 <i>높이</i>는 어긋나
    /// 손발이 가로대 사이 허공을 짚는다. IK 로 실제 발판 위치에 스냅해 그 오차를 흡수한다.
    ///
    /// <b>가중치를 1 미만으로 두는 이유</b>: 완전히 덮어쓰면 클립의 몸통 리듬과 따로 놀아 뻣뻣해 보인다.
    /// 기본값은 애니를 살리면서 발판에 붙는 정도(0.7)로 잡았다 — 인스펙터에서 조절한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class LadderIK : MonoBehaviour
    {
        [Tooltip("사다리 IK 사용 여부. 끄면 클립 그대로 재생한다.")]
        [SerializeField] private bool m_enabled = true;

        [Tooltip("손 IK 가중치(0=클립 그대로, 1=완전히 기둥에 고정).")]
        [Range(0f, 1f)][SerializeField] private float m_handWeight = 0.7f;

        [Tooltip("발 IK 가중치(0=클립 그대로, 1=완전히 발판에 고정).")]
        [Range(0f, 1f)][SerializeField] private float m_footWeight = 0.7f;

        [Tooltip("손이 잡는 지점이 골반보다 얼마나 위인지(m). 오르는 자세라 손은 위쪽 가로대를 잡는다.")]
        [SerializeField] private float m_handHeightBias = 0.55f;

        [Tooltip("발이 딛는 지점이 골반보다 얼마나 아래인지(m).")]
        [SerializeField] private float m_footHeightBias = -0.85f;

        private Animator _animator;
        private ClimbSensor _sensor;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _sensor = GetComponentInParent<ClimbSensor>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!m_enabled || _animator == null || _sensor == null) return;

            var ladder = _sensor.Current;
            // 사다리에 붙어 있는 동안만. (Climbing 파라미터가 아니라 센서를 보는 이유: 파라미터명은 프리팹 배선이라
            //  여기서 다시 알 필요가 없고, 센서가 곧 "지금 이 사다리에 매달려 있다"는 사실 자체다.)
            if (ladder == null)
            {
                ClearWeights();
                return;
            }

            float pelvisY = _animator.bodyPosition.y;

            // 손: 골반보다 위쪽 가로대 높이에서 좌우 기둥을 잡는다.
            float handY = ladder.GetNearestRungY(pelvisY + m_handHeightBias);
            SetGoal(AvatarIKGoal.LeftHand, ladder.GetGripPoint(handY, right: false), m_handWeight);
            SetGoal(AvatarIKGoal.RightHand, ladder.GetGripPoint(handY, right: true), m_handWeight);

            // 발: 골반보다 아래 가로대. 좌우를 한 칸 어긋나게 두면 사다리 타는 자세가 자연스럽다.
            float footYBase = ladder.GetNearestRungY(pelvisY + m_footHeightBias);
            SetGoal(AvatarIKGoal.LeftFoot, ladder.GetGripPoint(footYBase, right: false), m_footWeight);
            SetGoal(AvatarIKGoal.RightFoot, ladder.GetGripPoint(footYBase, right: true), m_footWeight);
        }

        private void SetGoal(AvatarIKGoal goal, Vector3 position, float weight)
        {
            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKPosition(goal, position);
        }

        private void ClearWeights()
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
        }
    }
}

using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리(P6) — <b>대상이 행동을 소유</b>하는 상호작용물. 플레이어가 상호작용하면 자기 자신을
    /// <see cref="ClimbSensor"/> 에 넘겨 "이 사다리에 붙어라"라고 요청한다.
    ///
    /// <b>왜 IInteractable 인가</b>: 프로젝트 교리상 탐지는 <see cref="InteractionDetector"/> 가 전담하고
    /// 입력→대상 위임은 <see cref="IInteractable"/> 로 흐른다(`if (E) Climb()` 직결 금지).
    /// 덕분에 사다리는 트리거 볼륨·입력 처리를 스스로 갖지 않아도 된다.
    ///
    /// <b>권위</b>: 완전 로컬(P6 결정). 네트워크 동기 없음 — 원격에는 사다리 오르는 모습이 보이지 않는다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Ladder : MonoBehaviour, IInteractable
    {
        [Tooltip("상단 도달 시 올라설 지점의, 사다리 기준 수평 오프셋(m). 사다리 뒤쪽(= 플레이어 반대편)으로 민다.")]
        [SerializeField] private float m_topExitDistance = 0.6f;

        [Tooltip("사다리 면에서 몸이 떨어져 붙는 거리(m). 메시에 파묻히지 않게.")]
        [SerializeField] private float m_attachOffset = 0.35f;

        private Collider _collider;

        private Collider Body => _collider != null ? _collider : (_collider = GetComponent<Collider>());

        /// <summary>사다리 최하단 Y(월드). 여기까지 내려오면 발이 땅에 닿는다 → 이탈.</summary>
        public float BottomY => Body.bounds.min.y;

        /// <summary>사다리 최상단 Y(월드). 여기까지 올라오면 위로 올라선다 → 이탈.</summary>
        public float TopY => Body.bounds.max.y;

        /// <summary>사다리 중심의 수평 위치(월드). 부착 지점 계산 기준.</summary>
        public Vector3 CenterXZ
        {
            get
            {
                Vector3 c = Body.bounds.center;
                return new Vector3(c.x, 0f, c.z);
            }
        }

        /// <summary>HUD 안내 문구 — "[E] 오르기".</summary>
        public string InteractionPrompt => "오르기";

        /// <summary>상호작용 = 붙잡기 요청. 실제 전이는 Locomotion FSM(ClimbState)이 한다.</summary>
        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            var sensor = interactor.GetComponentInChildren<ClimbSensor>();
            if (sensor == null) return;
            sensor.RequestAttach(this);
        }

        /// <summary>
        /// 부착 자세 — 사다리 정면(플레이어가 서 있던 쪽)에서 <see cref="m_attachOffset"/> 만큼 떨어져 사다리를 바라본다.
        /// 진입 방향을 자동으로 쓰므로 사다리 회전/설치 방향을 인스펙터로 맞출 필요가 없다.
        /// </summary>
        public void GetAttachPose(Vector3 playerPosition, out Vector3 position, out Quaternion rotation)
        {
            Vector3 center = CenterXZ;
            Vector3 fromLadder = new Vector3(playerPosition.x - center.x, 0f, playerPosition.z - center.z);
            if (fromLadder.sqrMagnitude < 0.0001f)
                fromLadder = Vector3.forward; // 정확히 중심에 겹쳐 있으면 임의 방향
            fromLadder.Normalize();

            float clampedY = Mathf.Clamp(playerPosition.y, BottomY, TopY);
            position = new Vector3(center.x, clampedY, center.z) + fromLadder * m_attachOffset;
            rotation = Quaternion.LookRotation(-fromLadder); // 사다리를 마주본다
        }

        /// <summary>상단 이탈 지점 — 사다리 반대편(등지고 있던 쪽) 바닥으로 올라선다.</summary>
        public Vector3 GetTopExitPosition(Vector3 playerPosition)
        {
            Vector3 center = CenterXZ;
            Vector3 fromLadder = new Vector3(playerPosition.x - center.x, 0f, playerPosition.z - center.z);
            if (fromLadder.sqrMagnitude < 0.0001f)
                fromLadder = Vector3.forward;
            fromLadder.Normalize();

            return new Vector3(center.x, TopY, center.z) - fromLadder * m_topExitDistance;
        }

        private void OnDrawGizmosSelected()
        {
            var body = GetComponent<Collider>();
            if (body == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(body.bounds.center, body.bounds.size);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Input
{
    /// <summary>
    /// 플레이어 오브젝트에 붙이는 컴포넌트.
    /// 범위 안 IInteractable 후보를 관리하고
    /// 매 프레임 Score가 가장 높은 것을 InteractionSystem에 전달한다.
    ///
    /// Score = 거리 점수 × distanceWeight + 카메라 방향 점수 × angleWeight
    ///
    /// 설정:
    ///   - 이 오브젝트에 Is Trigger = true 인 SphereCollider 추가
    ///   - Layer Collision Matrix에서 플레이어 ↔ 인터랙터블 레이어 충돌 활성화
    /// </summary>
    public sealed class InteractionDetector : MonoBehaviour
    {
        [Inject] private InteractionSystem _interactionSystem;

        [Header("가중치 — 합이 1.0이 되도록")]
        [SerializeField, Range(0f, 1f)] private float distanceWeight = 0.4f;
        [SerializeField, Range(0f, 1f)] private float angleWeight    = 0.6f;

        private readonly List<IInteractable> _candidates = new List<IInteractable>();
        private Transform _cameraTransform;
        private float     _detectionRadius;

        private void Start()
        {
            // UnityEngine.Camera로 정규화 — 형제 네임스페이스 Game.Gameplay.Camera와 충돌 방지.
            _cameraTransform = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : transform;

            var col = GetComponent<SphereCollider>();
            _detectionRadius = col != null ? col.radius : 3f;
        }

        private void Update()
        {
            CleanDestroyed();
            _interactionSystem.SetCurrent(_candidates.Count == 0 ? null : GetBestCandidate());
        }

        private void OnTriggerEnter(Collider other)
        {
            var interactable = other.GetComponentInParent<IInteractable>();
            if (interactable != null && !_candidates.Contains(interactable))
                _candidates.Add(interactable);
        }

        private void OnTriggerExit(Collider other)
        {
            var interactable = other.GetComponentInParent<IInteractable>();
            if (interactable != null)
                _candidates.Remove(interactable);
        }

        // ── Score 계산 ────────────────────────────────

        private IInteractable GetBestCandidate()
        {
            IInteractable best      = null;
            float         bestScore = float.MinValue;

            var playerPos     = transform.position;
            var cameraForward = _cameraTransform.forward;

            foreach (var candidate in _candidates)
            {
                var toTarget  = candidate.Transform.position - playerPos;
                var distance  = toTarget.magnitude;

                // 0~1, 가까울수록 높음
                var distScore = 1f - Mathf.Clamp01(distance / _detectionRadius);

                // 0~1, 카메라 정면에 가까울수록 높음
                var angleScore = (Vector3.Dot(cameraForward, toTarget.normalized) + 1f) * 0.5f;

                var total = distScore * distanceWeight + angleScore * angleWeight;

                if (total > bestScore ||
                    (Mathf.Approximately(total, bestScore) &&
                     best != null &&
                     candidate.InteractionPriority > best.InteractionPriority))
                {
                    bestScore = total;
                    best      = candidate;
                }
            }

            return best;
        }

        /// <summary>파괴된 오브젝트가 후보에 남아있으면 제거.</summary>
        private void CleanDestroyed()
        {
            for (var i = _candidates.Count - 1; i >= 0; i--)
            {
                if (_candidates[i] is MonoBehaviour mb && mb == null)
                    _candidates.RemoveAt(i);
            }
        }
    }
}

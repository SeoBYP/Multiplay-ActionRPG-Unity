using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class HitDetector : MonoBehaviour
    {
        [SerializeField] private Transform _origin;
        [SerializeField] private Vector3 _boxCenterOffset = new(0f, 1f, 0.8f);
        [SerializeField] private Vector3 _boxSize = new(1.1f, 1.2f, 1.2f);
        [SerializeField] private LayerMask _targetLayerMask = ~0;
        [SerializeField] private int _maxHits = 16;
        [SerializeField] private bool _drawGizmos = true;

        private Collider[] _hits;

        private void Awake()
        {
            _origin ??= transform;
            _hits = new Collider[Mathf.Max(1, _maxHits)];
        }

        public IReadOnlyList<AbilitySystemComponent> PerformDetection()
        {
            if (_hits == null || _hits.Length != Mathf.Max(1, _maxHits))
            {
                _hits = new Collider[Mathf.Max(1, _maxHits)];
            }

            Vector3 center = GetWorldCenter();
            Quaternion rotation = _origin.rotation;
            Vector3 halfExtents = _boxSize * 0.5f;

            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _hits,
                rotation,
                _targetLayerMask,
                QueryTriggerInteraction.Ignore);

            List<AbilitySystemComponent> targets = new();
            HashSet<AbilitySystemComponent> uniqueTargets = new();

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _hits[i];
                if (hit == null || hit.transform.root == transform.root)
                    continue;

                AbilitySystemComponent target = hit.GetComponentInParent<AbilitySystemComponent>();
                if (target == null || target.transform.root == transform.root)
                    continue;

                if (uniqueTargets.Add(target))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private Vector3 GetWorldCenter()
        {
            Transform origin = _origin != null ? _origin : transform;
            return origin.TransformPoint(_boxCenterOffset);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmos)
                return;

            Transform origin = _origin != null ? _origin : transform;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(GetWorldCenter(), origin.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _boxSize);

            Gizmos.matrix = previousMatrix;
        }
    }
}

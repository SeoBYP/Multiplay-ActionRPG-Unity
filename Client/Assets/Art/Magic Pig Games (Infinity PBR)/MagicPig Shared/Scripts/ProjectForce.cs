using System;
using UnityEngine;

namespace MagicPigGames.Shared
{
    [Documentation(
        "This script applies a force to objects within a radius of the projectile. Useful for impact particles and " +
        "the end of line renderers, such as lasers.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/project-force")]
    [Serializable]
    public class ProjectForce : MagicPigMonoBehaviour
    {
        [Header("Force Setup")]
        [Tooltip("The type of force to apply. Impulse and VelocityChange can be multiplied by non-infinite duration, " +
                 "and are only applied once.")]
        public ForceMode forceMode = ForceMode.Force;

        public float force = 10f;

        [Tooltip("Only active when ForceMode.Impulse or VelocityChange is selected. When true, the force will be " +
                 "multiplied by duration, unless duration is infinite (<0).")]
        public bool multipleImpulseForceByDuration;

        [Header("Options")] public bool applyForceInDirection = true;

        public float areaRadius = 8f;
        public float delay;
        public float duration = -1f; // Add this. Default to -1 (infinite)

        [Header("Forward Options")] public float forwardMaxAngle = 30f;

        [Header("Debug Options")] public bool drawDebugLine;
        public Color lineColor = Color.magenta;
        protected float _activeTime; // Time since force started

        protected Collider[] _colliders = new Collider[10];

        protected float _delayTimer;

        public bool ForceActive { get; private set; } = true;

        protected virtual float ImpulseMultiplier => multipleImpulseForceByDuration ? Mathf.Abs(duration) : 1f;

        protected virtual void Update()
        {
            if (!DelayMet())
                return;

            // Only do these once
            if (forceMode is ForceMode.Impulse or ForceMode.VelocityChange && _activeTime > 0)
                return;

            DoForce();
            _activeTime += Time.deltaTime;
        }

        protected virtual void OnEnable()
        {
            _delayTimer = 0f;
            _activeTime = 0f;
        }

        public virtual float ForceThisFrame()
        {
            var baseForce = force;

            if (forceMode is ForceMode.Impulse or ForceMode.VelocityChange)
                baseForce *= ImpulseMultiplier;

            if (forceMode is ForceMode.Acceleration or ForceMode.Force)
                baseForce *= Time.deltaTime;

            return baseForce;
        }

        protected virtual bool DelayMet()
        {
            if (_delayTimer >= delay) return true;

            _delayTimer += Time.deltaTime;
            return false;
        }

        protected virtual void DoForce()
        {
            if (!ForceActive) return;
            if (_activeTime > duration && duration > 0) return; // If we have a duration and it's expired, return

            ApplyForce();
        }

        protected virtual void ApplyForce()
        {
            var numColliders = GatherColliders();
            ApplyForceDirectionally(numColliders);
            ApplyForceOutward(numColliders);
        }

        protected virtual int GatherColliders()
        {
            var numColliders = Physics.OverlapSphereNonAlloc(transform.position, areaRadius, _colliders);

            while (numColliders == _colliders.Length)
            {
                _colliders = new Collider[_colliders.Length * 2];
                numColliders = Physics.OverlapSphereNonAlloc(transform.position, areaRadius, _colliders);
            }

            return numColliders;
        }

        protected virtual void ApplyForceOutward(int numColliders)
        {
            if (applyForceInDirection) return;

            for (var i = 0; i < numColliders; i++)
            {
                if (_colliders[i].attachedRigidbody == null) continue;

                // Calculate force direction from the center to the object
                var forceDirection =
                    (_colliders[i].transform.position - transform.position)
                    .normalized; // Correct direction for pushing away
                _colliders[i].attachedRigidbody.AddForce(forceDirection * ForceThisFrame(), forceMode);

                DrawDebugLine(transform.position, _colliders[i].transform.position, lineColor);
            }
        }


        protected virtual void ApplyForceDirectionally(int numColliders)
        {
            if (!applyForceInDirection) return;

            var cosMaxAngle = Mathf.Cos(forwardMaxAngle * Mathf.Deg2Rad); // Convert the angle to radians

            for (var i = 0; i < numColliders; i++)
            {
                if (_colliders[i].attachedRigidbody == null) continue;

                // Calculate the direction from this transform to the collider
                var colliderDirection = (_colliders[i].transform.position - transform.position).normalized;

                // If the angle to the collider is within the max angle
                if (Vector3.Dot(transform.forward, colliderDirection) >= cosMaxAngle)
                    _colliders[i].attachedRigidbody.AddForce(transform.forward * ForceThisFrame(), forceMode);

                DrawDebugLine(transform.position, _colliders[i].transform.position, lineColor);
            }
        }

        protected virtual void DrawDebugLine(Vector3 transformPosition, Vector3 position, Color color)
        {
            if (!drawDebugLine) return;
            Debug.DrawLine(transformPosition, position, color);
        }


        protected virtual void SetForceActive(bool active)
        {
            ForceActive = active;
            if (active) _activeTime = 0f; // Reset active time when force is activated
        }
    }
}
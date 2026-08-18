using System;
using UnityEngine;

namespace MagicPigGames.Shared
{
    [Documentation(
        "This script applies a force to objects within a radius of the projectile. Useful for impact particles and " +
        "the end of line renderers, such as lasers. This version adds a curve to the force, allowing for more complex " +
        "force patterns. Note: Impulse and VelocityChange are not supported with curves.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/project-force")]
    [Serializable]
    public class ProjectForceWithCurve : ProjectForce
    {
        [Header("Curve Settings - Note: Don't use Impulse or VelocityChange")]
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (forceMode is not (ForceMode.Impulse or ForceMode.VelocityChange)) return;

            Debug.LogWarning("ProjectForceWithCurve: Impulse and VelocityChange are not supported with curves. " +
                             "Changing to Force.");
            forceMode = ForceMode.Force;
        }

        public override float ForceThisFrame()
        {
            var baseForce = force;
            baseForce *= curve.Evaluate(_activeTime / duration);
            baseForce *= Time.deltaTime;
            return baseForce;
        }
    }
}
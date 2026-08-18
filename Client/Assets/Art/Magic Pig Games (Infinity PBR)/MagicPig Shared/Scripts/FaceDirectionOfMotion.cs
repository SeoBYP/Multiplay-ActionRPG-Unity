using System;
using UnityEngine;

// Turn the object to face the direction of motion.

namespace MagicPigGames.Shared
{
    [Documentation("Use this script to make an object face the direction of motion.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/overview-and-quickstart")]
    [Serializable]
    public class FaceDirectionOfMotion : MagicPigMonoBehaviour
    {
        private Rigidbody _rigidbody;
        public Rigidbody Rigidbody => GetRigidBody();

        private void Start()
        {
            if (Rigidbody != null) return;

            Debug.LogWarning("There is no rigidbody on this object, so it can't face the direction of " +
                             "motion. Disabling script.");
            enabled = false;
        }

        // If the object is moving, face the direction of motion.
        private void Update()
        {
            if (GetComponent<Rigidbody>().linearVelocity == Vector3.zero) return;

            transform.forward = GetComponent<Rigidbody>().linearVelocity.normalized;
        }

        private Rigidbody GetRigidBody()
        {
            if (_rigidbody != null)
                return _rigidbody;

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            return _rigidbody;
        }
    }
}
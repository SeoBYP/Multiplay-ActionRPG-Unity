using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.Shared
{
    [Documentation(
        "Rotates the object around a specified axis at a random speed. The speed can change at random intervals.")]
    [Serializable]
    public class Rotate : MagicPigMonoBehaviour
    {
        [Header("Rotation Options")]
        [Tooltip("The transform to rotate. CAUTION: Rotating the parent of a projectile etc may cause unexpected " +
                 "movement.")]
        public Transform rotatingTransform;

        [Tooltip("The axis around which the projectile will rotate.")]
        public Vector3 rotationAxis = Vector3.up;

        [Tooltip("The speed at which the projectile will rotate, in angles per second")]
        public float rotationSpeedMin = 90f;

        [Tooltip("The speed at which the projectile will rotate, in angles per second.")]
        public float rotationSpeedMax = 90f;

        [Tooltip(
            "When true, each axis will rotate at a different speed. Otherwise, the speed will be the same for all axes.")]
        public bool separateAxisSpeeds;

        [Tooltip("The interval at which the speed of the projectile will change.")] [Min(0)]
        public float speedChangeIntervalMin;

        [Tooltip("The interval at which the speed of the projectile will change.")] [Min(0)]
        public float speedChangeIntervalMax;

        private float _nextSpeedChange;

        private float _speedChangeTimer;

        private float RandomSpeed => Random.Range(rotationSpeedMin, rotationSpeedMax);
        private float RandomSpeedChangeInterval => Random.Range(speedChangeIntervalMin, speedChangeIntervalMax);
        public Vector3 RotationSpeed { get; private set; }

        protected virtual void Update()
        {
            rotatingTransform.Rotate(RotationSpeed * Time.deltaTime);
        }

        protected virtual void OnEnable()
        {
            if (rotatingTransform == null)
                rotatingTransform = transform;
            _speedChangeTimer = 0f;
            RotationSpeed = GetRotationSpeed();
            if (speedChangeIntervalMax > 0)
            {
                SetNextSpeedChange();
                StartCoroutine(SpeedChange());
            }
        }

        protected virtual void OnValidate()
        {
            if (speedChangeIntervalMin > speedChangeIntervalMax)
            {
                speedChangeIntervalMax = speedChangeIntervalMin;
                Debug.Log("Min speed must be <= MaxSpeed. Setting MaxSpeed to MinSpeed.");
            }
            
            if (rotatingTransform == null)
                rotatingTransform = transform;
        }

        protected virtual void SetNextSpeedChange()
        {
            _speedChangeTimer = 0f;
            _nextSpeedChange = RandomSpeedChangeInterval;
        }

        protected virtual IEnumerator SpeedChange()
        {
            while (true)
            {
                if (_speedChangeTimer >= RandomSpeedChangeInterval)
                {
                    RotationSpeed = GetRotationSpeed();
                    SetNextSpeedChange();
                }
                else
                {
                    _speedChangeTimer += Time.deltaTime;
                }

                yield return null;
            }
        }

        public virtual Vector3 GetRotationSpeed()
        {
            var newRotation = rotationAxis;
            if (separateAxisSpeeds)
            {
                newRotation.x *= RandomSpeed;
                newRotation.y *= RandomSpeed;
                newRotation.z *= RandomSpeed;
            }
            else
            {
                newRotation *= RandomSpeed;
            }

            return newRotation;
        }
    }
}
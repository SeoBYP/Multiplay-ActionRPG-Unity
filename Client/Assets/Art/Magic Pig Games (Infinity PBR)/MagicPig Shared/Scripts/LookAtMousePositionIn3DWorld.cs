using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
 * This works best when the camera is BEHIND the object with this script on it.
 *
 * You can add TWO objects -- one that rotates horizontally and one that tilts vertically.
 */

namespace MagicPigGames.Shared
{
    [Serializable]
    public class LookAtMousePositionIn3DWorld : MagicPigMonoBehaviour
    {
        private const float NearlyFullRotation = 359.9f;

        [Header("Plumbing")] public Camera targetCamera;

        [Header("Transforms")] public Transform horizontalTransform;

        public Transform verticalTransform;

        [Header("Rotation Options")] [Range(-180f, 0f)]
        public float minHorizontalAngle = -180f; // Minimum horizontal rotation angle

        [Range(0f, 180f)] public float maxHorizontalAngle = 180f; // Maximum horizontal rotation angle

        public float horizontalRotationSpeed = 15f; // Speed of the smooth horizontal rotation

        [Range(-90f, 0f)] public float minVerticalAngle = -90f; // Minimum vertical rotation angle

        [Range(0f, 90f)] public float maxVerticalAngle = 90f; // Maximum vertical rotation angle

        public float verticalRotationSpeed = 15f; // Speed of the smooth vertical rotation

        [Header("Options")] public bool drawDebugLine;

        private bool _noHorizontalRotationLimits;

        private Vector3 _targetPosition;

        protected virtual void Start()
        {
            if (maxHorizontalAngle - minHorizontalAngle >= NearlyFullRotation)
                _noHorizontalRotationLimits = true;
        }

        protected virtual void Update()
        {
            SetTargetAtMouseLocation();
            HandleVerticalRotation();
            HandleHorizontalRotation();
            DrawDebugLine();
        }

        public void SetTarget(Vector3 value)
        {
            _targetPosition = value;
        }

        protected virtual void HandleHorizontalRotation()
        {
            if (!horizontalTransform) return;

            var targetY = _targetPosition;
            targetY.y = horizontalTransform.position.y;

            var targetRotationY =
                Quaternion.LookRotation(targetY - horizontalTransform.position, horizontalTransform.up);

            horizontalTransform.rotation = Quaternion.Slerp(horizontalTransform.rotation, targetRotationY,
                verticalRotationSpeed * Time.deltaTime);
            horizontalTransform.localEulerAngles = new Vector3(0f, horizontalTransform.localEulerAngles.y, 0f);

            ClampHorizontalRotation();
        }

        protected virtual void ClampHorizontalRotation()
        {
            if (_noHorizontalRotationLimits) return;
            var eulerY = horizontalTransform.localEulerAngles.y;
            if (eulerY >= 180f && eulerY < 360f - maxHorizontalAngle)
                horizontalTransform.localEulerAngles = new Vector3(0f, 360f - maxHorizontalAngle, 0f);
            else if (eulerY < 180f && eulerY > -minHorizontalAngle)
                horizontalTransform.localEulerAngles = new Vector3(0f, -minHorizontalAngle, 0f);
        }

        protected virtual void HandleVerticalRotation()
        {
            if (!verticalTransform) return;

            var targetX = _targetPosition - verticalTransform.position;
            var targetRotationX = Quaternion.LookRotation(targetX, verticalTransform.up);

            verticalTransform.rotation = Quaternion.Slerp(verticalTransform.rotation, targetRotationX,
                horizontalRotationSpeed * Time.deltaTime);

            var localEulerAngles = verticalTransform.localEulerAngles;
            localEulerAngles = new Vector3(localEulerAngles.x, 0f, 0f);

            var eulerX = localEulerAngles.x;

            if (eulerX >= 180f && eulerX < 360f - maxVerticalAngle)
                localEulerAngles = new Vector3(360f - maxVerticalAngle, 0f, 0f);
            else if (eulerX < 180f && eulerX > -minVerticalAngle)
                localEulerAngles = new Vector3(-minVerticalAngle, 0f, 0f);

            verticalTransform.localEulerAngles = localEulerAngles;
        }

        protected virtual void DrawDebugLine()
        {
            if (!drawDebugLine) return;

            var startPosition = horizontalTransform.position;
            var endPosition = startPosition +
                              horizontalTransform.forward * Vector3.Distance(startPosition, _targetPosition);
            Debug.DrawLine(startPosition, endPosition, Color.red);
        }

        protected virtual void SetTargetAtMouseLocation()
        {
            Vector3 mousePosition;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            mousePosition = mouse.position.ReadValue();
#else
            mousePosition = Input.mousePosition;
#endif
            var cameraRay = targetCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(cameraRay, out var hitInfo, 500f))
                SetTarget(hitInfo.point);
        }
    }
}
using System;
using UnityEngine;

namespace MagicPigGames.Shared
{
    [Serializable]
    public class MoveBetweenPoints : MagicPigMonoBehaviour
    {
        public Vector3[] localPoints = Array.Empty<Vector3>();
        public float maxSpeed = 1f;
        public float waitTime = 0.3f;

        [Header("Gizmo Settings")] public bool onlyShowWhenSelected = true;

        public float gizmoSize = 0.15f;

        public Color editTimeColor = Color.red;
        public Color playNextColor = Color.green;
        public Color playPreviousColor = Color.red;
        public Color playOtherColor = Color.grey;

        // Add smooth time variable and initialize the current velocity
        public float smoothTime = 0.5f;

        private int _currentPoint;
        private float _currentSpeed;
        private bool _isMoving = true;
        private float _waitTimer;
        private Vector3[] _worldPoints = Array.Empty<Vector3>();

        private void Start()
        {
            _worldPoints = new Vector3[localPoints.Length];
            for (var i = 0; i < localPoints.Length; i++) _worldPoints[i] = transform.TransformPoint(localPoints[i]);
        }

        private void Update()
        {
            if (_worldPoints.Length == 0 || !_isMoving)
                return;

            if (_waitTimer > 0)
            {
                _waitTimer -= Time.deltaTime;
                _currentSpeed = 0; // Reset speed when waiting
                return;
            }

            var distanceToTarget = Vector3.Distance(transform.position, _worldPoints[_currentPoint]);
            var targetSpeed = Mathf.Lerp(0, maxSpeed, Mathf.InverseLerp(0, smoothTime, distanceToTarget));
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, maxSpeed * Time.deltaTime);
            if (_currentSpeed > maxSpeed)
                _currentSpeed = maxSpeed;

            transform.position = Vector3.MoveTowards(transform.position, _worldPoints[_currentPoint],
                _currentSpeed * Time.deltaTime);

            if (distanceToTarget < 0.01f)
            {
                _waitTimer = waitTime;
                _currentPoint++;
                if (_currentPoint >= _worldPoints.Length)
                    _currentPoint = 0;
            }
        }

        private void OnDrawGizmos()
        {
            if (onlyShowWhenSelected) return;

            DrawGizmosCode();
        }

        private void OnDrawGizmosSelected()
        {
            if (!onlyShowWhenSelected) return;

            DrawGizmosCode();
        }

        public void GoToNextPoint()
        {
            _currentPoint++;
            if (_currentPoint >= _worldPoints.Length)
                _currentPoint = 0;

            // Move to the next point immediately
            transform.position = _worldPoints[_currentPoint];
        }

        public void ToggleMoving()
        {
            _isMoving = !_isMoving;
        }

        public void SetMaxSpeed(float value)
        {
            maxSpeed = value;
        }

        private void DrawGizmosCode()
        {
            if (localPoints.Length == 0)
                return;

            var defaultColor = Application.isPlaying ? playOtherColor : editTimeColor;
            Gizmos.color = defaultColor;

            if (Application.isPlaying)
                for (var i = 0; i < _worldPoints.Length; i++)
                {
                    // If this is the nextPoint
                    if (i == _currentPoint)
                        Gizmos.color = playNextColor;
                    else if (i == (_currentPoint - 1) % _worldPoints.Length)
                        Gizmos.color = playPreviousColor;
                    else
                        Gizmos.color = playOtherColor;

                    Gizmos.DrawSphere(_worldPoints[i], gizmoSize);
                    var nextPoint = _worldPoints[(i + 1) % _worldPoints.Length];

                    if (i == (_currentPoint - 1) % _worldPoints.Length)
                        Gizmos.color = playNextColor;
                    else
                        Gizmos.color = playOtherColor;
                    Gizmos.DrawLine(_worldPoints[i], nextPoint);
                }
            else
                for (var i = 0; i < localPoints.Length; i++)
                {
                    var worldPoint = transform.TransformPoint(localPoints[i]);
                    Gizmos.DrawSphere(worldPoint, gizmoSize);
                    var nextPoint = transform.TransformPoint(localPoints[(i + 1) % localPoints.Length]);
                    Gizmos.DrawLine(worldPoint, nextPoint);
                }
        }
    }
}
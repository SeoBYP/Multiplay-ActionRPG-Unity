using System.Collections;
using UnityEngine;

namespace MagicPigGames.Shared
{
    public class MoveObjectRandomly : MagicPigMonoBehaviour
    {
        [Header("Options")] 
        [Tooltip("If true, the object will move when enabled")]
        public bool moveOnEnable = true;
        [Tooltip("If true, the object will stay above the terrain")]
        public bool stayAboveTerrain = true;
        [Tooltip("The time it takes to move to the new position")]
        public Vector2 moveTime = new Vector2(1, 2);
        [Tooltip("The minimum distance the object will move")]
        public Vector3 moveDistanceMin = new Vector3(-1, -1, -1);
        [Tooltip("The maximum distance the object will move")]
        public Vector3 moveDistanceMax = new Vector3(1, 1, 1);
        [Tooltip("If -1, will repeat indefinitely")]
        public int timesToRepeat = -1;

        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private float _timeToMove;
        private float _timeStarted;
        private bool _moving;
        private int _currentRepeatCount;

        private void OnEnable()
        {
            if (moveOnEnable)
                StartCoroutine(MoveRepeatedly());
        }

        private void Update()
        {
            if (!_moving)
                return;

            var timeSinceStarted = Time.time - _timeStarted;
            var percentageComplete = timeSinceStarted / _timeToMove;

            transform.position = Vector3.Lerp(_startPosition, _targetPosition, percentageComplete);

            if (percentageComplete >= 1.0f)
                _moving = false;
        }

        private IEnumerator MoveRepeatedly()
        {
            _currentRepeatCount = 0;
            while (timesToRepeat == -1 || _currentRepeatCount < timesToRepeat)
            {
                Move();
                yield return new WaitUntil(() => !_moving);
                _currentRepeatCount++;
            }
        }

        public void Move()
        {
            _startPosition = transform.position;
            _targetPosition = GetNewPosition();
            _timeToMove = Random.Range(moveTime.x, moveTime.y);
            _timeStarted = Time.time;
            _moving = true;
        }

        private Vector3 GetNewPosition()
        {
            var randomDistance = new Vector3(
                Random.Range(moveDistanceMin.x, moveDistanceMax.x),
                Random.Range(moveDistanceMin.y, moveDistanceMax.y),
                Random.Range(moveDistanceMin.z, moveDistanceMax.z)
            );
            var newPosition = transform.position + randomDistance;

            if (stayAboveTerrain && Terrain.activeTerrain != null)
            {
                newPosition.y = Mathf.Max(
                    newPosition.y,
                    Terrain.activeTerrain.SampleHeight(newPosition) + Terrain.activeTerrain.transform.position.y
                );
            }

            return newPosition;
        }
    }
}

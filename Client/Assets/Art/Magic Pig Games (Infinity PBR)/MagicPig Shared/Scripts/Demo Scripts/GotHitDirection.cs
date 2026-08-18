using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace InfinityPBR
{
    public class GotHitDirection : MonoBehaviour
    {
        [Header("Required")] 
        public Animator animator;
        public string keyTrigger = "GotHit";
        public string keyGotHitX = "GotHitX";
        public string keyGotHitY = "GotHitY";

        private float _time;
        private GameObject _sphere;
        private Vector3 _fromPosition;
        private Vector3 _direction;
        
        /*
        public void TriggerHit(Vector3 fromPosition)
        {
            var direction = Direction(transform.position, fromPosition); // Parameter 1 = Target, Parameter 2 = Thing doing the hitting
            Debug.Log($"Trigger Hit from {fromPosition} to {transform.position}, direction is {direction}");
            animator.SetFloat(keyGotHitX, direction.x);
            animator.SetFloat(keyGotHitY, direction.z);
            animator.SetTrigger(keyTrigger);
        }
        */
        
        public void TriggerHit(Vector3 fromPosition)
        {
            var directionToHitInWorld = Direction(transform.position, fromPosition); // Parameter 1 = Target, Parameter 2 = Thing doing the hitting
            var directionToHitInLocalSpace = transform.InverseTransformDirection(directionToHitInWorld);
            animator.SetFloat(keyGotHitX, directionToHitInLocalSpace.x);
            animator.SetFloat(keyGotHitY, directionToHitInLocalSpace.z);
            animator.SetTrigger(keyTrigger);
        }

        
        /*
         * EVERYTHING BELOW IS FOR THE DEMO SCENE
         */
        IEnumerator HitInTime()
        {
            while (_time > 0)
            {
                _time -= Time.deltaTime;
                yield return null;
            }

            TriggerHit(_fromPosition);
        }

        void Update()
        {
            if (_sphere == null) return;

            MoveSphere();
        }

        private void MoveSphere()
        {
            _sphere.transform.localPosition += _sphere.transform.forward * Time.deltaTime * 20;
        }

        public void DemoHit()
        {
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.transform.localScale = Vector3.one * 0.33f;
            _sphere.AddComponent<Rigidbody>();
            var eulerAngles = _sphere.transform.localEulerAngles;
            eulerAngles.y = Random.Range(0, 360);
            _sphere.transform.localEulerAngles = eulerAngles;
            var characterPosition = transform.position;
            _sphere.transform.position = characterPosition + Vector3.up;
            _sphere.transform.position += _sphere.transform.forward * 4;
            _fromPosition = _sphere.transform.position;
            _sphere.transform.LookAt(characterPosition + Vector3.up);
            _sphere.GetComponent<SphereCollider>().enabled = false;
            _time = 0.2f;
            StartCoroutine(nameof(HitInTime));
            Destroy(_sphere, 0.2f);
            _direction = Direction(characterPosition, _sphere.transform.position);
        }
        
        //public Vector3 Direction(Vector3 fromPosition, Vector3 toPosition) => (toPosition - fromPosition) / Distance(toPosition, fromPosition);
        public Vector3 Direction(Vector3 fromPosition, Vector3 toPosition) => (toPosition - fromPosition).normalized;
        public float Distance(Vector3 position1, Vector3 position2) => (position1 - position2).magnitude;

    }
}

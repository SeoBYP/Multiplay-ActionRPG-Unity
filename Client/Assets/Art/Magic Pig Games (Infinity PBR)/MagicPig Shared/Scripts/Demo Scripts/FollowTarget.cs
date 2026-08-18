using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InfinityPBR.Demo
{
    public class FollowTarget : MonoBehaviour
    {
        [Header("Required")]
        public GameObject target;
        public bool position = true;
        public bool rotation = true;

        void LateUpdate()
        {
            if (target == null) return;
            
            if (position) FollowPosition();
            if (rotation) FollowRotation();
        }

        private void FollowPosition() => transform.position = target.transform.position;
        
        private void FollowRotation() => transform.rotation = target.transform.rotation;
    }
}

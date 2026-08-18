using UnityEngine;

namespace MagicPigGames.Shared
{
    public class SimpleLookAt : MagicPigMonoBehaviour
    {
        public Transform target;
        public bool lockX;
        public bool lockY;
        public bool lockZ;

        private void Update()
        {
            if (target == null)
            {
                Debug.LogWarning($"Simple look at on object {gameObject.name} had no target. Disabling.");
                enabled = false;
                return;
            }

            var targetDirection = target.position - transform.position;
            var rotationToTarget = Quaternion.LookRotation(targetDirection);

            // Obtain the target rotation in Euler angles
            var newRotation = rotationToTarget.eulerAngles;

            // Apply locking
            if (lockX) newRotation.x = transform.rotation.eulerAngles.x;
            if (lockY) newRotation.y = transform.rotation.eulerAngles.y;
            if (lockZ) newRotation.z = transform.rotation.eulerAngles.z;

            // Set the new rotation
            transform.rotation = Quaternion.Euler(newRotation);
        }
    }
}
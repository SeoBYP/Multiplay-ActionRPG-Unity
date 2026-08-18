using UnityEngine;

namespace MagicPigGames.Shared
{
    public class ResetPositionWhenBelowHeight : MagicPigMonoBehaviour
    {
        public float resetHeight = -10;
        public bool useStartPosition = true;
        public Vector3 resetPosition = new(0, 0, 0);

        private void Start()
        {
            if (useStartPosition)
                resetPosition = transform.position;
        }

        private void Update()
        {
            if (resetHeight >= resetPosition.y)
            {
                Debug.LogWarning("ResetHeight is higher than resetPosition.y, disabling");
                enabled = false;
                return;
            }

            if (transform.position.y < resetHeight)
                transform.position = resetPosition;
        }
    }
}
using Game.Gameplay.Character.Input;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Camera
{
    public class CharacterCameraFollow : MonoBehaviour
    {
        private ICharacterInputSource _inputSource;
        
        [Header("Camera")]
        //YOU NEED TO set this target or the camera will not respond to the input
        public GameObject CinemachineCameraTarget;
        public float TopCameraLimit = 70.0f;
        public float BottomCameraLimit = -30.0f;

        private GameObject _mainCamera;
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private const float _cameraRotationThreshold = 0.01f;
        
        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
            _inputSource = GetComponent<ICharacterInputSource>();
        }
        
        
        private void LateUpdate()
        {
            CameraRotation();
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_inputSource.Current.Look.sqrMagnitude >= _cameraRotationThreshold)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = 1.0f;

                _cinemachineTargetYaw += _inputSource.Current.Look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _inputSource.Current.Look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomCameraLimit, TopCameraLimit);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch,
                _cinemachineTargetYaw, 0.0f);
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}
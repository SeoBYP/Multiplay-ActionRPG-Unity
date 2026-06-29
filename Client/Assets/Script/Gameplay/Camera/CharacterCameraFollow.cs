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

        [Tooltip("락온 중 카메라가 타겟 방향으로 수렴하는 속도(클수록 빠름).")]
        [SerializeField] private float lockOnTurnSpeed = 12f;

        /// <summary>
        /// 락온(2.6.3) 대상. 세팅되면 마우스 Look 대신 이 대상을 향하도록 카메라 피벗을 회전시킨다.
        /// null 이면 기존 마우스 조작. <see cref="LockOnDriver"/> 가 매 프레임 세팅/해제한다.
        /// 락온 중에도 yaw/pitch 캐시를 갱신해 두므로 해제 시 마우스가 현재 각도에서 자연스럽게 이어진다.
        /// </summary>
        public Transform LockTarget { get; set; }

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
            if (LockTarget != null)
                LockOnRotation();
            else if (_inputSource.Current.Look.sqrMagnitude >= _cameraRotationThreshold)
            {
                // if there is an input and camera position is not fixed
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

        /// <summary>락온 중: 피벗→타겟 방향으로 yaw/pitch 를 부드럽게 수렴(캐시 갱신 → 해제 시 마우스 연속).</summary>
        private void LockOnRotation()
        {
            Vector3 toTarget = LockTarget.position - CinemachineCameraTarget.transform.position;
            float horizDist = new Vector2(toTarget.x, toTarget.z).magnitude;
            if (horizDist < 0.0001f)
                return; // 타겟이 바로 위/아래 — yaw 정의 불가, 직전 각도 유지

            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            // 타겟이 보통 지면 → 약간 내려보기. 수직 각도를 카메라 제한범위로 클램프.
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(toTarget.y, horizDist) * Mathf.Rad2Deg,
                BottomCameraLimit, TopCameraLimit);

            float t = Time.deltaTime * lockOnTurnSpeed;
            _cinemachineTargetYaw = Mathf.LerpAngle(_cinemachineTargetYaw, targetYaw, t);
            _cinemachineTargetPitch = Mathf.Lerp(_cinemachineTargetPitch, targetPitch, t);
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}
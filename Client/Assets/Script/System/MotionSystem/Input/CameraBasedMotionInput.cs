using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// QuanticBrains의 CameraBasedInput과 같은 역할을 하는 프로젝트용 입력 변환기입니다.
    /// 2D 이동 입력을 카메라 forward/right 기준의 월드 이동 벡터로 바꿔서
    /// Motion Matching의 미래 trajectory 예측에 사용할 수 있게 합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "MotionMatching/CustomInputs/Project Camera Based Input")]
    public class CameraBasedMotionInput : InputCustomizable
    {
        public override float3 HandleCustomInput(Vector2 input, Transform transform)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Vector3 fallback = transform.TransformDirection(new Vector3(input.x, 0f, input.y));
                return new float3(fallback.x, 0f, fallback.z);
            }

            Vector3 forward = mainCamera.transform.forward;
            Vector3 right = mainCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            Vector3 move = forward * input.y + right * input.x;
            return new float3(move.x, 0f, move.z);
        }
    }
}

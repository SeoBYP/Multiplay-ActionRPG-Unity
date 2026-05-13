using Unity.Mathematics;
using UnityEngine;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// 프로젝트별 입력 값을 Motion Matching이 사용할 이동 벡터로 변환하는 확장 지점입니다.
    /// Player 입력, AI 입력, 네트워크 입력이 달라도 최종적으로 이 메서드 결과만 Motion Matching에 전달됩니다.
    /// </summary>
    public abstract class InputCustomizable : ScriptableObject
    {
        /// <summary>
        /// raw input과 기준 Transform을 받아 Motion Matching용 이동 intent를 반환합니다.
        /// </summary>
        public abstract float3 HandleCustomInput(Vector2 input, Transform transform);
    }
}

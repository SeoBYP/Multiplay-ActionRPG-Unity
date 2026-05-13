using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// float3에서 자주 사용하는 방향 상수와 안전 처리 유틸리티입니다.
    /// Unity.Mathematics 기반 job 코드에서 Vector3 대신 사용할 수 있도록 제공합니다.
    /// </summary>
    public static class Float3Extensions
    {
        /// <summary>
        /// local forward 방향입니다.
        /// </summary>
        public static readonly float3 Forward = new(0, 0, 1);

        /// <summary>
        /// local up 방향입니다.
        /// </summary>
        public static readonly float3 Up = new(0, 1, 0);

        /// <summary>
        /// 벡터가 거의 0에 가까운지 확인합니다.
        /// 방향 계산 전에 zero vector 예외를 피할 때 사용합니다.
        /// </summary>
        public static bool NearZero(this float3 value)
        {
            return math.abs(value.x) < 0.001 && math.abs(value.y) < 0.001 && math.abs(value.z) < 0.001;
        }

        /// <summary>
        /// NaN 또는 Infinity 성분을 0으로 정리합니다.
        /// trajectory나 direction 계산 중 비정상 값이 pose search로 전파되는 것을 막습니다.
        /// </summary>
        public static float3 Sanitize(this float3 value)
        {
            if (math.isnan(value.x) || math.isinf(value.x))
                value.x = 0.0f;
            if (math.isnan(value.y) || math.isinf(value.y))
                value.y = 0.0f;
            if (math.isnan(value.z) || math.isinf(value.z))
                value.z = 0.0f;
            return value;
        }
    }
}

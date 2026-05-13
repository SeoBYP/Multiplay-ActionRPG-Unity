using Unity.Mathematics;

namespace Game.System.MotionSystem
{
    /// <summary>
    /// Motion Matching에서 좌표계 변환, 회전 속도 계산, 방향 계산에 사용하는 수학 유틸리티입니다.
    /// Pose search는 world-space 값과 character-local-space 값을 자주 오가므로,
    /// 이 클래스가 root transform 기준 변환을 한 곳에 모읍니다.
    /// </summary>
    public static class MathUtils
    {
        /// <summary>
        /// 2D 좌표 두 점 사이의 거리를 계산합니다.
        /// </summary>
        public static float Magnitude(float2 a, float2 b)
        {
            float2 c = b - a;
            return math.sqrt(c.x * c.x + c.y * c.y);
        }

        /// <summary>
        /// quaternion의 부호를 양의 w 기준으로 정규화합니다.
        /// q와 -q는 같은 회전을 의미하지만 보간/로그 계산에서는 부호 차이가 결과를 흔들 수 있습니다.
        /// </summary>
        public static quaternion Abs(quaternion q)
        {
            return q.value.w < 0.0f ? new quaternion(-q.value.x, -q.value.y, -q.value.z, -q.value.w) : q;
        }

        /// <summary>
        /// quaternion을 scaled angle-axis 벡터로 변환합니다.
        /// angular velocity 계산처럼 회전 차이를 벡터값으로 다뤄야 할 때 사용합니다.
        /// </summary>
        public static float3 ToScaledAngleAxis(quaternion q1, float eps = 1e-8f)
        {
            return 2.0f * Log(q1, eps);
        }

        /// <summary>
        /// scaled angle-axis 벡터를 quaternion으로 되돌립니다.
        /// </summary>
        public static quaternion FromScaledAngleAxis(float3 angleAxis, float eps = 1e-8f)
        {
            return Exp(angleAxis * 0.5f, eps);
        }

        /// <summary>
        /// quaternion logarithm을 계산해 회전 차이를 벡터 공간으로 옮깁니다.
        /// </summary>
        private static float3 Log(quaternion q, float epsilon)
        {
            float qLength = math.sqrt(q.value.x * q.value.x + q.value.y * q.value.y
                                                            + q.value.z * q.value.z);

            if (qLength < epsilon) return new float3(q.value.x, q.value.y, q.value.z);

            float theta = math.acos(math.clamp(q.value.w, -1f, 1f));
            return theta * (new float3(q.value.x, q.value.y, q.value.z) / qLength);
        }

        /// <summary>
        /// quaternion exponential을 계산해 벡터 공간의 회전 표현을 quaternion으로 되돌립니다.
        /// </summary>
        private static quaternion Exp(float3 angleAxis, float epsilon)
        {
            float theta = math.sqrt(angleAxis.x * angleAxis.x + angleAxis.y * angleAxis.y + angleAxis.z * angleAxis.z);

            if (theta < epsilon) return new quaternion(angleAxis.x, angleAxis.y, angleAxis.z, 1.0f);

            float c = math.cos(theta);
            float s = math.sin(theta);
            float3 unitV = angleAxis / theta;

            return new quaternion(s * unitV.x, s * unitV.y, s * unitV.z, c);
        }

        /// <summary>
        /// quaternion 길이로 나누어 정규화합니다.
        /// 현재 코드에서는 직접 호출되지 않지만, 회전 누적 계산 안정화에 사용할 수 있습니다.
        /// </summary>
        private static quaternion QuaternionNormalized(quaternion q, float epsilon)
        {
            float divisor = QuaternionLength(q) + epsilon;
            return new quaternion(q.value.x / divisor, q.value.y / divisor, q.value.z / divisor, q.value.w / divisor);
        }

        /// <summary>
        /// quaternion의 길이를 계산합니다.
        /// </summary>
        private static float QuaternionLength(quaternion q)
        {
            return math.sqrt(q.value.w * q.value.w + q.value.x * q.value.x + q.value.y * q.value.y +
                             q.value.z * q.value.z);
        }

        /// <summary>
        /// current에서 next로 변하는 회전 속도를 scaled angle-axis 형태로 계산합니다.
        /// BoneData.angularVelocity bake 또는 현재 pose velocity 계산에 사용합니다.
        /// </summary>
        public static float3 AngularVelocity(quaternion current, quaternion next, float deltaTime)
        {
            return ToScaledAngleAxis(Abs(math.mul(next, math.inverse(current)))) / deltaTime;
        }

        /// <summary>
        /// world position을 character root 기준 local position으로 변환합니다.
        /// </summary>
        public static float3 TranslateToLocal(float3 position, quaternion rotation, float3 globalPosition)
        {
            return math.transform(CreateInverseModel(position, rotation), globalPosition);
        }

        /// <summary>
        /// 미리 계산된 inverse model matrix를 사용해 world position을 local position으로 변환합니다.
        /// </summary>
        public static float3 TranslateToLocal(float4x4 modelInverse, float3 globalPosition)
        {
            return math.transform(modelInverse, globalPosition);
        }

        /// <summary>
        /// position/rotation 기준 transform matrix의 inverse를 생성합니다.
        /// MotionMatching은 이 값을 캐시해 root 기준 local-space feature를 계산합니다.
        /// </summary>
        public static float4x4 CreateInverseModel(float3 position, quaternion rotation)
        {
            return math.inverse(CreateModel(position, rotation));
        }

        /// <summary>
        /// position/rotation 기준 transform matrix를 생성합니다.
        /// scale은 Motion Matching root 변환에서는 보통 1로 고정합니다.
        /// </summary>
        public static float4x4 CreateModel(float3 position, quaternion rotation)
        {
            return float4x4.TRS(position, rotation, new float3(1));
        }

        /// <summary>
        /// character root 기준 local position을 world position으로 변환합니다.
        /// </summary>
        public static float3 TranslateToGlobal(float3 position, quaternion rotation, float3 localPosition)
        {
            var model4X4 = float4x4.TRS(position, rotation, new float3(1));
            return math.transform(model4X4, localPosition);
        }

        /// <summary>
        /// matrix를 사용해 local direction을 world direction으로 변환합니다.
        /// 방향 벡터이므로 translation 성분은 무시합니다.
        /// </summary>
        public static float3 TransformDirection(float4x4 model, float3 localDirection)
        {
            return math.normalize(math.mul(model, new float4(localDirection, 0.0f)).xyz);
        }

        /// <summary>
        /// position/rotation 기준으로 local direction을 world direction으로 변환합니다.
        /// </summary>
        public static float3 TransformDirection(float3 position, quaternion rotation, float3 localDirection)
        {
            return TransformDirection(CreateModel(position, rotation), localDirection);
        }

        /// <summary>
        /// inverse model matrix를 사용해 world direction을 local direction으로 변환합니다.
        /// </summary>
        public static float3 InverseTransformDirection(float4x4 inverseModel, float3 globalDirection)
        {
            return TransformDirection(inverseModel, globalDirection);
        }

        /// <summary>
        /// position/rotation 기준으로 world direction을 local direction으로 변환합니다.
        /// </summary>
        public static float3 InverseTransformDirection(float3 position, quaternion rotation, float3 globalDirection)
        {
            return TransformDirection(CreateInverseModel(position, rotation), globalDirection);
        }

        /// <summary>
        /// 방향 벡터를 바라보는 quaternion을 생성합니다.
        /// 방향이 불안정할 수 있으므로 LookRotationSafe를 사용합니다.
        /// </summary>
        public static quaternion DirectionToQuaternion(float3 direction)
        {
            return quaternion.LookRotationSafe(direction, Float3Extensions.Up);
        }

        /// <summary>
        /// 평균과 표준편차를 기준으로 feature component를 정규화합니다.
        /// 표준편차가 0이면 distance 계산을 안정화하기 위해 0을 반환합니다.
        /// </summary>
        public static float NormalizeHelper(float component, float meanComponent, float stdComponent)
        {
            if (stdComponent == 0)
            {
                return 0;
            }

            return (component - meanComponent) / stdComponent;
        }

        /// <summary>
        /// axis 기준 signed angle을 degree 단위로 계산합니다.
        /// 방향 전환 판단이나 facing 차이 계산에 사용할 수 있습니다.
        /// </summary>
        public static float SignedAngle(float3 from, float3 to, float3 axis)
        {
            float num1 = Angle(from, to);
            float num2 = from.y * to.z - from.z * to.y;
            float num3 = from.z * to.x - from.x * to.z;
            float num4 = from.x * to.y - from.y * to.x;
            float num5 = math.sign(axis.x * num2 + axis.y * num3 + axis.z * num4);
            return num1 * num5;
        }

        /// <summary>
        /// 두 방향 벡터 사이의 unsigned angle을 degree 단위로 계산합니다.
        /// </summary>
        public static float Angle(float3 from, float3 to)
        {
            float num = math.sqrt(math.lengthsq(from) * math.lengthsq(to));
            return num < 1.00000000362749E-15
                ? 0.0f
                : math.acos(math.clamp(math.dot(from, to) / num, -1f, 1f)) * 57.29578f;
        }
    }
}

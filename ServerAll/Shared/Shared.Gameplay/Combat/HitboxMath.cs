using System;
using System.Numerics;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// hitbox 겹침 판정의 순수 함수(System.Numerics, 엔진 비의존). 서버·클라 동일 함수로 동일 판정.
    /// 시전자 yaw로 월드 타겟을 시전자 로컬 프레임으로 변환한 뒤, 로컬 hitbox(offset 기준)와 구(타겟 반경)의 겹침을 본다.
    ///
    /// yaw 규약: 0 = 정면 +Z. yaw도(度)만큼 Y축 회전한 것이 시전자 정면.
    /// </summary>
    public static class HitboxMath
    {
        public static bool Overlaps(
            Vector3 attackerPos, float attackerYawDeg, HitboxSpec hitbox,
            Vector3 targetPos, float targetRadius)
        {
            // 월드 타겟을 시전자 로컬로 변환. Unity 좌표(왼손, Y-up)에서 yaw θ 의 forward=(sinθ,0,cosθ) 이고,
            // 로컬→월드 회전의 역행렬(월드→로컬)은 아래와 같다. (과거 -yaw 를 써서 X/Z 교차항 부호가 뒤집혀
            //  정북/정남(yaw 0/180, sin0)에서만 맞고 측면(90/270 등)에서 히트박스가 반대쪽으로 가던 버그를 교정.)
            double yawRad = attackerYawDeg * Math.PI / 180.0;
            float cos = (float)Math.Cos(yawRad);
            float sin = (float)Math.Sin(yawRad);

            Vector3 rel = targetPos - attackerPos;
            Vector3 local = new Vector3(
                rel.X * cos - rel.Z * sin,
                rel.Y,
                rel.X * sin + rel.Z * cos);

            // hitbox 중심(로컬) = Offset. 그 기준 타겟까지의 거리.
            Vector3 d = local - hitbox.Offset;

            if (hitbox.Shape == EHitboxShape.Sphere)
            {
                float r = hitbox.HalfExtents.X + targetRadius;
                return d.LengthSquared() <= r * r;
            }

            // Box(AABB, 로컬) vs 구(targetRadius): 박스 표면까지 최소거리.
            float dx = Math.Max(0f, Math.Abs(d.X) - hitbox.HalfExtents.X);
            float dy = Math.Max(0f, Math.Abs(d.Y) - hitbox.HalfExtents.Y);
            float dz = Math.Max(0f, Math.Abs(d.Z) - hitbox.HalfExtents.Z);
            return (dx * dx + dy * dy + dz * dz) <= targetRadius * targetRadius;
        }
    }
}

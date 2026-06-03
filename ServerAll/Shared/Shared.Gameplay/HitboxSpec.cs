using System.Numerics;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// 시전자 로컬 기준 hitbox 정의 (순수 데이터). active window 동안 이 박스/구를 타겟과 겹쳐 평가.
    /// Box: HalfExtents = 반-크기. Sphere: HalfExtents.X = 반경.
    /// </summary>
    public readonly struct HitboxSpec
    {
        public EHitboxShape Shape { get; }
        public Vector3 Offset { get; }       // 시전자 로컬 오프셋(정면 +Z 기준)
        public Vector3 HalfExtents { get; }  // Box 반-크기 / Sphere는 X=반경

        public HitboxSpec(EHitboxShape shape, Vector3 offset, Vector3 halfExtents)
        {
            Shape = shape;
            Offset = offset;
            HalfExtents = halfExtents;
        }
    }
}

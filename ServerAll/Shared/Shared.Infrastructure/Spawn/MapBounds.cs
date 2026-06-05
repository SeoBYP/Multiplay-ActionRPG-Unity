namespace Shared.Infrastructure.Spawn;

/// <summary>
/// 맵 경계(XZ 평면 사각형). 서버 권위 몬스터 이동이 매 틱 이 경계로 위치를 clamp 해
/// 몬스터가 맵을 벗어나지 못하게 한다. Y축은 평지 전제라 다루지 않는다.
///
/// Size 가 0 이하인 맵(경계 미저작)은 "무경계"로 간주 — Clamp/Contains 가 무동작이라
/// 몬스터를 원점에 가두는 사고를 막는다.
/// </summary>
public sealed record MapBounds(float CenterX, float CenterZ, float SizeX, float SizeZ)
{
    /// <summary>경계 미저작(무경계) 기본값.</summary>
    public static readonly MapBounds Unbounded = new(0f, 0f, 0f, 0f);

    public bool HasArea => SizeX > 0f && SizeZ > 0f;

    public float MinX => CenterX - SizeX / 2f;
    public float MaxX => CenterX + SizeX / 2f;
    public float MinZ => CenterZ - SizeZ / 2f;
    public float MaxZ => CenterZ + SizeZ / 2f;

    /// <summary>(x,z)가 경계 안인가. 무경계면 항상 true.</summary>
    public bool Contains(float x, float z)
        => !HasArea || (x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ);

    /// <summary>(x,z)를 경계 안으로 clamp. 무경계면 그대로 반환.</summary>
    public (float X, float Z) Clamp(float x, float z)
        => HasArea
            ? (System.Math.Clamp(x, MinX, MaxX), System.Math.Clamp(z, MinZ, MaxZ))
            : (x, z);
}

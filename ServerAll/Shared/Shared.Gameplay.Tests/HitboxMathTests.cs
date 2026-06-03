using System.Numerics;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// CA-2: hitbox 겹침 판정(순수 기하). 서버·클라가 같은 함수로 같은 판정을 내야 한다.
/// 시전자 yaw 기준 로컬 hitbox를 월드 타겟(구)과 겹치는지 평가.
/// </summary>
public class HitboxMathTests
{
    // 시전자 정면(+Z) 1유닛 앞, 반경 0.5 박스
    private static readonly HitboxSpec FrontBox =
        new HitboxSpec(EHitboxShape.Box, new Vector3(0, 0, 1), new Vector3(0.5f, 0.5f, 0.5f));

    [Fact]
    public void 정면_박스_안의_타겟은_적중이다()
    {
        bool hit = HitboxMath.Overlaps(Vector3.Zero, 0f, FrontBox, new Vector3(0, 0, 1), 0.5f);
        Assert.True(hit);
    }

    [Fact]
    public void 뒤에_있는_타겟은_빗나간다()
    {
        bool hit = HitboxMath.Overlaps(Vector3.Zero, 0f, FrontBox, new Vector3(0, 0, -1), 0.5f);
        Assert.False(hit);
    }

    [Fact]
    public void 너무_먼_타겟은_빗나간다()
    {
        bool hit = HitboxMath.Overlaps(Vector3.Zero, 0f, FrontBox, new Vector3(0, 0, 3), 0.5f);
        Assert.False(hit);
    }

    [Fact]
    public void yaw_180도면_뒤의_타겟이_정면이_되어_적중이다()
    {
        bool hit = HitboxMath.Overlaps(Vector3.Zero, 180f, FrontBox, new Vector3(0, 0, -1), 0.5f);
        Assert.True(hit);
    }

    [Fact]
    public void 구형_hitbox는_반경합_거리로_판정한다()
    {
        var sphere = new HitboxSpec(EHitboxShape.Sphere, new Vector3(0, 0, 1), new Vector3(1f, 0, 0));
        Assert.True(HitboxMath.Overlaps(Vector3.Zero, 0f, sphere, new Vector3(0, 0, 1), 0.5f));   // 중심
        Assert.False(HitboxMath.Overlaps(Vector3.Zero, 0f, sphere, new Vector3(0, 0, 3), 0.5f));  // dist 2 > 1+0.5
    }
}

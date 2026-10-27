using Server.Actors;

namespace Server.Tests.Room;

/// <summary>
/// S3 이동 상한 — <c>MoveValidation.ClampStep(fromX, fromZ, toX, toZ, elapsedMs)</c>.
/// elapsedMs = 서버 수신 시각 기준 직전 수용 이후 경과. 정상 이동(스프린트 5.335·회피 8 m/s)은 깎이면 안 된다.
/// </summary>
public class MoveValidationTests
{
    private const float Precision = 0.001f;

    private static float Dist(float x, float z) => MathF.Sqrt(x * x + z * z);

    [Fact]
    public void 스프린트_속도_이동은_그대로_수용된다()
    {
        // 5.335 m/s × 50ms ≈ 0.27m
        var (x, z) = MoveValidation.ClampStep(0f, 0f, 0.27f, 0f, elapsedMs: 50);

        Assert.Equal(0.27f, x, Precision);
        Assert.Equal(0f, z, Precision);
    }

    [Fact]
    public void 회피_대시_속도_이동은_그대로_수용된다()
    {
        // 8 m/s × 50ms = 0.4m
        var (x, z) = MoveValidation.ClampStep(10f, 10f, 10f, 10.4f, elapsedMs: 50);

        Assert.Equal(10f, x, Precision);
        Assert.Equal(10.4f, z, Precision);
    }

    [Fact]
    public void 패킷이_몰려_도착해_경과시간이_거의_0이어도_정상_이동은_깎이지_않는다()
    {
        var (x, z) = MoveValidation.ClampStep(0f, 0f, 0.4f, 0f, elapsedMs: 1);

        Assert.Equal(0.4f, x, Precision);
        Assert.Equal(0f, z, Precision);
    }

    [Fact]
    public void 렉으로_1초_공백_뒤_최대속도로_몰아_온_이동도_수용된다()
    {
        var (x, z) = MoveValidation.ClampStep(0f, 0f, 8f, 0f, elapsedMs: 1000);

        Assert.Equal(8f, x, Precision);
        Assert.Equal(0f, z, Precision);
    }

    [Fact]
    public void 제자리_패킷은_그대로다()
    {
        var (x, z) = MoveValidation.ClampStep(3f, 4f, 3f, 4f, elapsedMs: 50);

        Assert.Equal(3f, x, Precision);
        Assert.Equal(4f, z, Precision);
    }

    [Fact]
    public void 한계를_넘는_이동은_목표_방향으로_잘린다()
    {
        // (300, 400) = 500m 떨어진 순간이동. 방향(3:4)은 유지하고 거리만 잘려야 한다.
        var (x, z) = MoveValidation.ClampStep(0f, 0f, 300f, 400f, elapsedMs: 50);

        float moved = Dist(x, z);
        Assert.InRange(moved, 0.1f, 12f);
        Assert.Equal(0.75f, x / z, 0.01f);
    }

    [Fact]
    public void 오래_멈춰_있다가_보낸_순간이동도_잘린다()
    {
        // 서 있으면 C_Move 를 안 보낸다 → 경과시간이 계속 커진다. 경과시간에 상한이 없으면
        // 60초 대기 = 480m 허용이 돼 "가만히 있다가 순간이동"이 뚫린다.
        var (x, z) = MoveValidation.ClampStep(0f, 0f, 300f, 400f, elapsedMs: 60_000);

        Assert.InRange(Dist(x, z), 0.1f, 12f);
    }
}

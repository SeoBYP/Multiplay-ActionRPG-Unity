namespace Server;

public static class MoveValidation
{
    public static (float, float) ClampStep(float fromX, float fromZ, float toX, float toZ, long elapsedMs)
    {
        long t = Math.Clamp(elapsedMs, 0, 1000); // 1초로 제한
        float allowed = 8f * (t + 200) / 1000f + 1f;
        
        float dx = toX - fromX;
        float dz = toZ - fromZ;
        float distSq = dx * dx + dz * dz;
        if(distSq <= allowed * allowed)
            return (toX, toZ);
        
        float s = allowed / MathF.Sqrt(distSq);
        return (fromX + dx * s , fromZ + dz * s);
    }
}
namespace GameServer.Infrastructure.Domains;

public class RedisSettings
{
    public static readonly TimeSpan RedisCacheTtl = TimeSpan.FromMinutes(30);
}
namespace GameServer.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Secret { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    
    public TimeSpan AccessTokenExpiration => TimeSpan.FromMinutes(AccessTokenMinutes);
    public DateTime GetExpirationTime() => DateTime.UtcNow.Add(AccessTokenExpiration);
}
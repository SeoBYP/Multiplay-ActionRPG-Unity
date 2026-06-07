using System.Security.Claims;
using GameServer.API.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.Tests.API;

/// <summary>
/// JWT 클레임 → userId 해석 검증. 핵심: ASP.NET JWT 미들웨어가 'sub'를 ClaimTypes.NameIdentifier로
/// 리매핑하는 경우에도 userId를 찾아야 한다(GetInventory가 UNAUTHORIZED 나던 버그).
/// </summary>
public class ServerCallContextExtensionTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
        => new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void NameIdentifier로_리매핑된_sub도_userId로_해석한다()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "42"));

        Assert.Equal(42L, ServerCallContextExtension.ResolveUserId(user));
    }

    [Fact]
    public void 리매핑되지_않은_sub도_userId로_해석한다()
    {
        var user = PrincipalWith(new Claim(JwtRegisteredClaimNames.Sub, "7"));

        Assert.Equal(7L, ServerCallContextExtension.ResolveUserId(user));
    }

    [Fact]
    public void 식별_클레임이_없으면_null()
    {
        Assert.Null(ServerCallContextExtension.ResolveUserId(PrincipalWith()));
    }

    [Fact]
    public void userId가_숫자가_아니면_null()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "not-a-number"));

        Assert.Null(ServerCallContextExtension.ResolveUserId(user));
    }
}

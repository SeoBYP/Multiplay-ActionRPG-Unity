# JWT in C# (ASP.NET Core) - Obsidian Notes

Goal: Implement JWT authentication in the current GameServer project with Microsoft.IdentityModel.JsonWebTokens.

---

## 1) What JWT is (one line)
JWT is a signed string that contains claims about a user and can be verified by the server.

---

## 2) Key terms
- Claim: A piece of user info inside the token (ex: user id).
- Issuer: Who issued the token (server).
- Audience: Who should accept the token (client or API).
- Secret: Server-only key to sign/verify the token.
- Access Token: Short-lived token used for requests.

---

## 3) Minimal settings (appsettings.json)
```json
"Jwt": {
  "Issuer": "GameServer",
  "Audience": "GameClient",
  "Secret": "your-secret-key-min-32-characters",
  "AccessTokenMinutes": 15
}
```

---

## 4) JwtOptions class
```csharp
namespace GameServer.Infrastructure.Security;

public class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Secret { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
}
```

---

## 5) IJwtTokenGenerator contract
```csharp
using System.Security.Claims;

namespace GameServer.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(long userId, string userName, string email);
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
}
```

---

## 6) Claims in the token (what they mean)
```csharp
var claims = new List<Claim> // list to hold all claims
{
    new(JwtRegisteredClaimNames.Sub, userId.ToString()),   // subject = user id
    new(JwtRegisteredClaimNames.UniqueName, userName),     // display name / username
    new(JwtRegisteredClaimNames.Email, email),             // email
    new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // token id
};
```
Why: These claims let the server identify the user from the token.

---

## 7) Token creation flow (line-by-line)
```csharp
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret)); // build signing key from secret
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); // select HMAC-SHA256 signature

var descriptor = new SecurityTokenDescriptor // bundle all token info
{
    Issuer = _jwtOptions.Issuer, // who issued the token
    Audience = _jwtOptions.Audience, // who should accept the token
    Subject = new ClaimsIdentity(claims), // attach claims to token
    Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes), // set expiry time
    SigningCredentials = creds // attach signing info
};

return _jwtHandler.CreateToken(descriptor); // create JWT string
```

---

## 8) JwtTokenGenerator (core implementation, line-by-line)
```csharp
using System.Security.Claims;
using System.Text;
using GameServer.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options; // resolved settings
    private readonly JsonWebTokenHandler _handler = new(); // creates and validates JWTs

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value; // bind config values
    }

    public string GenerateAccessToken(long userId, string userName, string email)
    {
        var claims = new List<Claim> // claims list
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)); // build signing key
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256); // choose algorithm

        var descriptor = new SecurityTokenDescriptor // define token content
        {
            Issuer = _options.Issuer, // issuer
            Audience = _options.Audience, // audience
            Subject = new ClaimsIdentity(claims), // claims
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes), // expiry
            SigningCredentials = creds // signature info
        };

        return _handler.CreateToken(descriptor); // generate JWT string
    }

    public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)); // signing key for validation
        var parameters = new TokenValidationParameters // rules for validation
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        var result = _handler.ValidateToken(token, parameters); // verify token signature/claims
        return result.IsValid && result.ClaimsIdentity != null // if valid, return principal
            ? new ClaimsPrincipal(result.ClaimsIdentity)
            : null;
    }
}
```

---

## 9) Wire-up in Program.cs (line-by-line)
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GameServer.Infrastructure.Security;

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt")); // bind config to JwtOptions
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>(); // register generator

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt"); // read Jwt section
        options.TokenValidationParameters = new TokenValidationParameters // validation rules
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Secret"]!)
            )
        };
    });

app.UseAuthentication(); // enable auth middleware
app.UseAuthorization(); // enable authorization
```

---

## 10) Use token in AuthService (login, line-by-line)
```csharp
var accessToken = jwtTokenGenerator.GenerateAccessToken(
    user.UserId, user.UserName, user.Email // claims source
);

return Result<LoginResponse>.Success(
    new LoginResponse(user.UserId, user.UserName, user.Email, accessToken) // include token
);
```

---

## 11) Protect APIs
```csharp
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class PlayerController : ControllerBase
{
}
```

---

## 12) Client usage (HTTP header)
```
Authorization: Bearer {access_token}
```

---

## 13) Quick checklist
- [ ] appsettings.json has Jwt section
- [ ] JwtOptions class exists
- [ ] IJwtTokenGenerator + JwtTokenGenerator implemented
- [ ] Program.cs has AddAuthentication + UseAuthentication
- [ ] Login returns AccessToken
- [ ] Protected controllers use [Authorize]

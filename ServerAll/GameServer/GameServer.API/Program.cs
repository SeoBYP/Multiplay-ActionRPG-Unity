using System.Text;
using GameServer.Infrastructure.Security;
using GameServer.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using DotNetEnv;
using GameServer.API.Middleware;
using GameServer.Application.Services.Auth;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Domain.Interfaces.DungeonRoom;
using GameServer.Domain.Interfaces.User;
using GameServer.Infrastructure.Repositories.DungeonRoom;
using GameServer.Infrastructure.Repositories.User;
using Microsoft.OpenApi;

var envPaths = new[]
{
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\..\\..\\Infra\\.env")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\Infra\\.env")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\Infra\\.env"))
};

foreach (var path in envPaths)
{
    if (File.Exists(path))
    {
        Env.Load(path);
        break;
    }
}
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Swagger에서 JWT 토큰 인증 단계 추가
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "GameServer.API", 
        Version = "v1",
        Description = "멀티플레이 액션 RPG 게임 서버 API"
    });
    
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",           // 소문자 권장
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Enter only the token (without 'Bearer ')."
    });
    
    // 모든 API에 JWT 인증 요구
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var redisConnection = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"
);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!))
        };
    });

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IUserSessionRepository, RedisUserSessionRepository>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

// DungeonLobby
builder.Services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
builder.Services.AddScoped<IDungeonRoomRepository, DungeonRoomRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

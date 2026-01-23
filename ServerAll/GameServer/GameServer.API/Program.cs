using System.Text;
using GameServer.Application.Services;
using GameServer.Application.Services.Interfaces;
using GameServer.Domain.Interfaces;
using GameServer.Infrastructure.Repositories;
using GameServer.Infrastructure.Security;
using GameServer.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using DotNetEnv;
using GameServer.API.Middleware;

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
builder.Services.AddSwaggerGen();

var redisConnection = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"
);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);


// applicationSetting.json???�는 Jwt ?�션??가?��???JwtOption??초기????
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
builder.Services.AddSingleton<ISessionRepository, RedisSessionRepository>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

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

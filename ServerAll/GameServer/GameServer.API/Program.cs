using System.Text;
using GameServer.Infrastructure.Security;
using GameServer.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using DotNetEnv;
using GameServer.API.Hubs;
using GameServer.API.Interceptors;
using GameServer.API.Middleware;
using GameServer.API.Services;
using GameServer.Application.Services.Auth;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Domain.Interfaces.DungeonRoom;
using GameServer.Domain.Interfaces.User;
using GameServer.Infrastructure.Repositories.DungeonRoom;
using GameServer.Infrastructure.Repositories.User;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

builder.WebHost.ConfigureKestrel(options =>
{
    // REST API, SignalR, Swagger용 (HTTP/1.1)
    options.ListenLocalhost(5131, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    });
    
    // gRPC 전용 (HTTP/2)
    options.ListenLocalhost(5132, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

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

builder.Services.AddSignalR();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

// GRPC 추가
builder.Services.AddScoped<AuthInterceptor>();
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;  // 개발 중에는 true
    // Interceptor는 나중에 추가
    
    options.Interceptors.Add<AuthInterceptor>();
});
builder.Services.AddGrpcReflection();

builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IUserSessionRepository, RedisUserSessionRepository>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

// DungeonLobby
builder.Services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
builder.Services.AddScoped<IDungeonRoomRepository, DungeonRoomRepository>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5131")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");  // ✅ 추가
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // ✅ gRPC Reflection 추가 (Postman이 서비스 목록을 볼 수 있게 함)
    app.MapGrpcReflectionService();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// app.UseHttpsRedirection();
app.UseRouting();              // 1. Routing 먼저!
app.UseCors();                 // 2. CORS
app.UseAuthentication();       // 3. 인증
app.UseAuthorization();        // 4. 권한

app.MapControllers();
app.MapGrpcService<AuthGrpcService>();

app.MapHub<ChatHub>("/chathub");

app.Run();

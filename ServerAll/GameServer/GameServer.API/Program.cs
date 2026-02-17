using System.Text;
using DotNetEnv;
using GameServer.API.Interceptors;
using GameServer.API.Middleware;
using GameServer.API.Services;
using GameServer.Application.Services.Auth;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Application.Services.Chat;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Infrastructure.Interfaces;
using GameServer.Infrastructure.Interfaces.Chat;
using GameServer.Infrastructure.Interfaces.DungeonRoom;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Infrastructure.Repositories.Chat;
using GameServer.Infrastructure.Repositories.DungeonRoom;
using GameServer.Infrastructure.Repositories.User;
using GameServer.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;

LoadEnv();

var builder = WebApplication.CreateBuilder(args);

// 1) Kestrel endpoints / protocols
ConfigureKestrel(builder);

// 2) Services
ConfigureServices(builder);

var app = builder.Build();

// 3) Middleware pipeline
ConfigurePipeline(app);

app.Run();


// ----------------------
// Setup helpers
// ----------------------

static void LoadEnv()
{
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
}

static void ConfigureKestrel(WebApplicationBuilder builder)
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        // REST/Swagger/SignalR: HTTP/1.1
        options.ListenLocalhost(5131, listen => listen.Protocols = HttpProtocols.Http1);

        // gRPC: HTTP/2 (plaintext)
        options.ListenLocalhost(5132, listen => listen.Protocols = HttpProtocols.Http2);
    });
}

static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;
    var config = builder.Configuration;

    // MVC / Swagger
    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "GameServer.API",
            Version = "v1",
            Description = "멀티플레이 액션 RPG 게임 서버 API"
        });

        // JWT bearer in Swagger UI
        o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter only the token (without 'Bearer ')."
        });

        // Require JWT globally
        o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", doc)] = { }
        });
    });

    // Redis
    var redisConnStr = config.GetConnectionString("Redis") ?? "localhost:6379";
    var redis = ConnectionMultiplexer.Connect(redisConnStr);
    services.AddSingleton<IConnectionMultiplexer>(redis);

    // JWT Auth
    services.Configure<JwtOptions>(config.GetSection("Jwt"));
    services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            var jwt = config.GetSection("Jwt");
            o.TokenValidationParameters = new TokenValidationParameters
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
    services.AddAuthorization();

    // SignalR
    services.AddSignalR();

    // gRPC (+ interceptor + reflection)
    services.AddScoped<AuthInterceptor>();
    services.AddGrpc(o =>
    {
        o.EnableDetailedErrors = true;
        o.Interceptors.Add<AuthInterceptor>();
    });
    services.AddGrpcReflection();

    // Response Compression (Binary 포함)
    services.AddResponseCompression(o =>
    {
        o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/octet-stream" });
    });

    // CORS (브라우저 기반 클라이언트/SignalR용)
    // ※ gRPC(Postman)는 CORS 영향 없음. 브라우저 gRPC-Web이면 별도 설정 필요.
    services.AddCors(o =>
    {
        o.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5131")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders(
                    "Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
        });
    });

    // DI registrations
    services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    services.AddSingleton<IUserSessionRepository, UserSessionRepository>();

    services.AddScoped<IPasswordHasher, PasswordHasher>();
    services.AddScoped<IAuthService, AuthService>();

    services.AddScoped<IDungeonLobbyService, DungeonLobbyService>();
    services.AddScoped<IDungeonRoomRepository, DungeonRoomRepository>();
    
    builder.Services.AddScoped<IChatService, ChatService>();
    builder.Services.AddScoped<IChatSubscriptionService, ChatSubscriptionService>();
    builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
}

static void ConfigurePipeline(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        // gRPC reflection for tooling (Postman, grpcurl, etc.)
        app.MapGrpcReflectionService();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseResponseCompression();

    app.UseRouting();

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    // endpoints
    app.MapControllers();
    
    // gRPC services
    app.MapGrpcService<AuthGrpcService>();
    app.MapGrpcService<DungeonLobbyGrpcService>();
    app.MapGrpcService<ChatGrpcService>();
}

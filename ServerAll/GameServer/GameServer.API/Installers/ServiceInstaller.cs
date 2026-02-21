using System.Text;
using GameServer.API.Installers.Domain;
using GameServer.API.Interceptors;
using GameServer.Infrastructure.Interfaces;
using GameServer.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace GameServer.API.Installers;

public class ServiceInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        // MVC / Swagger
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Redis
        var redisConnStr = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var redis = ConnectionMultiplexer.Connect(redisConnStr);
        services.AddSingleton<IConnectionMultiplexer>(redis);

        // JWT Auth
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                var jwt = configuration.GetSection("Jwt");
                var issuer = jwt["Issuer"] ?? "GameServer";
                var audience = jwt["Audience"] ?? "GameClient";
                var secret = jwt["Secret"];

                if (string.IsNullOrEmpty(secret))
                {
                    throw new InvalidOperationException("JWT Secret is not configured. Please check your .env file or appsettings.json (Jwt:Secret).");
                }

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                };
            });
        services.AddAuthorization();

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

        // CORS
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
    }
}

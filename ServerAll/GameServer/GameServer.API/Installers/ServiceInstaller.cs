using System.Text;
using GameServer.API.Interceptors;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
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
        var options = ConfigurationOptions.Parse(redisConnStr);
        options.AllowAdmin = true;
        
        var redis = ConnectionMultiplexer.Connect(options);
        
        // Clear Redis on start
        foreach (var endpoint in redis.GetEndPoints())
        {
            var server = redis.GetServer(endpoint);
            try
            {
                server.FlushAllDatabases();
                Console.WriteLine($"[Redis] Flushed all databases on {endpoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Redis] Failed to flush databases on {endpoint}: {ex.Message}");
            }
        }
        
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

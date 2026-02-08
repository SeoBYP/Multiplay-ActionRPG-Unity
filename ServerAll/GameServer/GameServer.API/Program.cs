using System.Text;
using GameServer.Infrastructure.Security;
using GameServer.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using DotNetEnv;
using GameServer.API.Hubs;
using GameServer.API.Middleware;
using GameServer.Application.Services.Auth;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using GameServer.Domain.Interfaces.DungeonRoom;
using GameServer.Domain.Interfaces.User;
using GameServer.Infrastructure.Repositories.DungeonRoom;
using GameServer.Infrastructure.Repositories.User;
using Microsoft.AspNetCore.ResponseCompression;
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

builder.Services.AddSignalR();

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5131")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
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
app.UseCors();                  // 1. CORS 먼저
app.UseDefaultFiles();          // 2. 기본 파일 (index.html 등)
app.UseStaticFiles();           // 3. 정적 파일 서빙
app.UseResponseCompression();   // 4. 압축
app.UseAuthentication();        // 5. 인증
app.UseAuthorization();         // 6. 권한

app.MapControllers();
// ✅ 이거 추가!
app.MapGet("/", () => Results.Content("""
<!DOCTYPE html>
<html>
<head>
    <title>SignalR Chat Test</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
</head>
<body>
<h2>SignalR Chat Test</h2>

<div>
    <label>Username:</label>
    <input type="text" id="userInput" value="TestUser" />
</div>

<div>
    <label>Message:</label>
    <input type="text" id="messageInput" />
    <button onclick="sendMessage()">Send</button>
</div>

<div>
    <button onclick="connect()">Connect</button>
    <button onclick="disconnect()">Disconnect</button>
</div>

<hr />
<ul id="messagesList"></ul>

<script>
    let connection = null;

    async function connect() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connection.on("ReceiveMessage", (user, message) => {
            const li = document.createElement("li");
            li.textContent = `${user}: ${message}`;
            document.getElementById("messagesList").appendChild(li);
        });

        try {
            await connection.start();
            console.log("SignalR Connected!");
            alert("Connected!");
        } catch (err) {
            console.error(err);
            alert("Connection failed: " + err);
        }
    }

    async function disconnect() {
        if (connection) {
            await connection.stop();
            console.log("Disconnected");
        }
    }

    async function sendMessage() {
        const user = document.getElementById("userInput").value;
        const message = document.getElementById("messageInput").value;

        try {
            await connection.invoke("SendMessage", user, message);
            document.getElementById("messageInput").value = "";
        } catch (err) {
            console.error(err);
            alert("Send failed: " + err);
        }
    }
</script>
</body>
</html>
""", "text/html"));

app.MapHub<ChatHub>("/chathub");

app.Run();

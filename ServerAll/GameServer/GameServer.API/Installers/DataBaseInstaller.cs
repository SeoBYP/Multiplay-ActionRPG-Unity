using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameServer.API.Installers;

public class DataBaseInstaller : IInstaller
{
    public void Install(WebApplicationBuilder app)
    {
        var connectionString = app.Configuration.GetConnectionString("GameDb");
        
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:GameDb is missing.");
        
        app.Services.AddDbContext<GameServerDbContext>(options => 
            options.UseNpgsql(connectionString));
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.API.Hubs;

[Authorize]
public class GameHub(ILogger<GameHub> logger) : Hub
{
    private readonly ILogger<GameHub> _logger = logger;

    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
        
    }
}
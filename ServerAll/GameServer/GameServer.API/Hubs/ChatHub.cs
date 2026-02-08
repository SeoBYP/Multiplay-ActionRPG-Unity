using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Hubs;

public class ChatHub(ILogger<ChatHub> logger) : Hub
{
    private readonly ILogger<ChatHub> _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }
    
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string user, string message)
    {
        _logger.LogInformation($"Message from {user}: {message}");
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
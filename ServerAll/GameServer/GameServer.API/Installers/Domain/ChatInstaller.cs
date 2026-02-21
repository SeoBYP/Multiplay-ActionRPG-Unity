using GameServer.Application.Services.Chat;
using GameServer.Application.Services.Chat.Interfaces;
using GameServer.Infrastructure.Interfaces.Chat;
using GameServer.Infrastructure.Repositories.Chat;

namespace GameServer.API.Installers.Domain;

public class ChatInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatSubscriptionService, ChatSubscriptionService>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
    }
}

using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Infrastructure.Domains.Chat;

namespace GameServer.API.Installers.Domain;

public class ChatInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddSingleton<IChatSubscriptionService, ChatSubscriptionService>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
    }
}

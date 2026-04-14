using GameServer.Application.Domains.Chat;
using GameServer.Application.Domains.Chat.Interfaces;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.Domains.Chat;
using Shared.Infrastructure.MessageQueue;

namespace GameServer.API.Installers.Domain;

public class ChatInstaller : IServiceInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatSubscriptionService, ChatSubscriptionService>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddSingleton<IChatEventStream, ChatEventStream>();
        services.AddSingleton<IBroadcastChannel<ChatMessage>, ChatBroadcastChannel>();
    }
}

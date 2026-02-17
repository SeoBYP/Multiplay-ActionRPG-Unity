namespace GameServer.API.Extension;

public static class ChatTypeExtensions
{
    public static Domain.Entities.Chat.ChatType ToDomain(this Grpc.Chat.ChatType grpcType) => grpcType switch {
        Grpc.Chat.ChatType.Global  => Domain.Entities.Chat.ChatType.Global,
        Grpc.Chat.ChatType.Room    => Domain.Entities.Chat.ChatType.Room,
        Grpc.Chat.ChatType.Whisper => Domain.Entities.Chat.ChatType.Whisper,
        _ => throw new ArgumentException()
    };
    
    public static Grpc.Chat.ChatType ToGrpc(this Domain.Entities.Chat.ChatType domainType) => domainType switch
    {
        Domain.Entities.Chat.ChatType.Global  => Grpc.Chat.ChatType.Global,
        Domain.Entities.Chat.ChatType.Room    => Grpc.Chat.ChatType.Room,
        Domain.Entities.Chat.ChatType.Whisper => Grpc.Chat.ChatType.Whisper,
        _ => throw new ArgumentException()
    };
}
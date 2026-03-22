namespace Shared.Infrastructure.Messages;

public class GameStartMessage
{
    public long RoomId { get; set; }
    public List<long> PlayerIds { get; set; } = [];
    
    public string TraceId { get; set; } = "";
}
namespace GameServer.Domain.Entities.Outbox;

public class OutboxMessage
{
    public long MessageId { get; private set; }
    
    public string Topic { get; private set; }
    
    public string Payload { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ProcessedAt { get; private set; }
    
    private OutboxMessage() { }
    
    public static OutboxMessage Create(string topic, string payload)
    {
        return new OutboxMessage
        {
            Topic = topic,
            Payload = payload,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }
}
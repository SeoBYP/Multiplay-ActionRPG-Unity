using System.Text.Json;
using GameServer.Application.Domains.Outbox;
using GameServer.Domain.Entities.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Domains.Outbox;

public class OutboxPublisherService(IServiceScopeFactory scopeFactory,
    IMessageQueue<GameStartRequestedMessage> gameStartMessageQueue,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisherService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                await ProcessBatchAsync(outboxRepository, stoppingToken);
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox messages.");
                try
                {
                    await Task.Delay(PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        logger.LogInformation("OutboxPublisherService is stopping.");
    }

    private async Task ProcessBatchAsync(IOutboxRepository outboxRepository, CancellationToken ct)
    {
        var messages = await outboxRepository.GetUnprocessedAsync(BatchSize, ct);
        if (messages.Count == 0) return;

        logger.LogInformation("Processing {Count} outbox messages.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(message, ct);
                await outboxRepository.MarkProcessedAsync(message.MessageId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}.", message.MessageId);
            }
        }
    }

    private async Task PublishMessageAsync(OutboxMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case OutboxTopics.GameStartRequested:
                var gameStartMessage = JsonSerializer.Deserialize<GameStartRequestedMessage>(message.Payload)
                    ?? throw new InvalidOperationException($"Failed to deserialize payload for topic {message.Topic}. MessageId: {message.MessageId}");
                
                await gameStartMessageQueue.EnqueueAsync(gameStartMessage);
                break;
            
            default:
                logger.LogWarning("Unknown outbox topic: {Topic}", message.Topic);
                break;
        }
    }
}
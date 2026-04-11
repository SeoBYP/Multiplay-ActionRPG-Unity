using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Room;
using Shared.Infrastructure.Messages;

namespace Server.Infrastructure;

public class TestRoomService(
    RoomManager roomManager,
    ILogger<TestRoomService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TestRoomService Started. Type 'testroom {roomId} {userId1} {userId2} ...' to create a room.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var input = await Task.Run(() => Console.ReadLine(), stoppingToken);
                if (string.IsNullOrWhiteSpace(input)) continue;

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2 && parts[0].Equals("testroom", StringComparison.OrdinalIgnoreCase))
                {
                    if (long.TryParse(parts[1], out long roomId))
                    {
                        var playerInfos = new List<PlayerInfo>();
                        for (int i = 2; i < parts.Length; i++)
                        {
                            if (long.TryParse(parts[i], out long userId))
                            {
                                playerInfos.Add(new PlayerInfo
                                {
                                    UserId = userId,
                                    Nickname = $"TestUser{userId}",
                                    SpawnIndex = i - 2
                                });
                            }
                        }

                        if (playerInfos.Count > 0)
                        {
                            var message = new GameStartRequestedMessage
                            {
                                RoomId = roomId,
                                PlayerInfos = playerInfos,
                                TraceId = Guid.NewGuid().ToString()
                            };

                            var room = roomManager.CreateRoom(roomId, playerInfos, message);
                            if (room != null)
                            {
                                logger.LogInformation("Manual Test Room {RoomId} created with {PlayerCount} players.", roomId, playerInfos.Count);
                            }
                            else
                            {
                                logger.LogWarning("Failed to create Manual Test Room {RoomId}. (Maybe already exists?)", roomId);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TestRoomService console loop");
            }
        }
    }
}

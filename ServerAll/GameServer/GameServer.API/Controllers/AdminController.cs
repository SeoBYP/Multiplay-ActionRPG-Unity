using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace GameServer.API.Controllers;

#if DEBUG
[ApiController]
[Route("api/admin")]
public class AdminController(IConnectionMultiplexer redis) : ControllerBase
{
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAll()
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());

        var keys = server.Keys(pattern: "*").ToArray();
        
        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
        }
        
        return Ok(new { message = $"Deleted {keys.Length} keys" });
    }
    
    // 세션은 유지하고 game:* 만 삭제
    [HttpDelete("clear-rooms")]
    public async Task<IActionResult> ClearRooms()
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());
        
        var keys = server.Keys(pattern: "game:*").ToArray();
        
        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
        }
        
        return Ok(new { message = $"Deleted {keys.Length} room keys" });
    }
    
    [HttpDelete("clear-sessions")]
    public async Task<IActionResult> ClearSessions()
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());
        
        var sessionKeys = server.Keys(pattern: "session:*").ToArray();
        var userSessionKeys = server.Keys(pattern: "user:*:session").ToArray();
        
        var allKeys = sessionKeys.Concat(userSessionKeys).ToArray();
        
        if (allKeys.Length > 0)
        {
            await db.KeyDeleteAsync(allKeys);
        }
        
        return Ok(new { message = $"Deleted {allKeys.Length} session keys" });
    }
    
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        
        var allKeys = server.Keys(pattern: "*").ToArray();
        var roomKeys = server.Keys(pattern: "game:*").ToArray();
        var sessionKeys = server.Keys(pattern: "session:*").ToArray();
        
        return Ok(new 
        { 
            totalKeys = allKeys.Length,
            roomKeys = roomKeys.Length,
            sessionKeys = sessionKeys.Length,
            redisConnected = redis.IsConnected
        });
    }
}
#endif
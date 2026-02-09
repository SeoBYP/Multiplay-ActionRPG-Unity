using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace GameServer.API.Controllers;

#if DEBUG
[ApiController]
[Route("api/admin")]
public class AdminController(IConnectionMultiplexer redis) : ControllerBase
{
    /// <summary>
    /// 모든 Redis 데이터 삭제 (개발 전용)
    /// </summary>
    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAll()
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints().First());
        
        // ✅ FlushDatabaseAsync 대신 Keys + Delete 사용
        var keys = server.Keys(pattern: "*").ToArray();
        
        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
        }
        
        return Ok(new { message = $"Deleted {keys.Length} keys" });
    }
    
    /// <summary>
    /// Room 데이터만 삭제 (세션 유지)
    /// </summary>
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
    
    /// <summary>
    /// 세션 데이터만 삭제
    /// </summary>
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
    
    /// <summary>
    /// Redis 상태 확인
    /// </summary>
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
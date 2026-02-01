using GameServer.Application.DTOs.DungeonRoom;
using GameServer.Application.DTOs.DungeonRoom.CreateRoom;
using GameServer.Application.DTOs.DungeonRoom.JoinRoom;
using GameServer.Application.DTOs.DungeonRoom.LeaveRoom;
using GameServer.Application.DTOs.DungeonRoom.StartRoom;
using GameServer.Application.Services.DungeonLobby;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DungeonLobbyController(IDungeonLobbyService dungeonLobbyService) : ControllerBase
{
    [HttpPost("room")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var result = await dungeonLobbyService.CreateDungeonRoomAsync(
            request.UserId, 
            request.RoomName, 
            request.MaxPlayers);
        
        if (!result.IsSuccess) 
            return BadRequest(result.Message);
        
        var response = result.Value!.ToCreateRoomResponse();  // ! 로 null이 아님 보장
        return Ok(response);
    }
    
    [HttpGet("rooms")]
    public async Task<IActionResult> GetActiveRooms()
    {
        var result = await dungeonLobbyService.GetActiveDungeonRoomsAsync();
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        if (result.Value != null)
        {
            var response = result.Value.ToGetRoomsResponse();
            return Ok(response);
        }
        return Ok();
    }
    
    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetRoom(long roomId)
    {
        var result = await dungeonLobbyService.GetDungeonRoomAsync(roomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        var response = result.Value!.ToRoomInfoDto();
        return Ok(response);
    }
    
    [HttpPost("room/{roomId}/join")]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequest request)
    {
        var result = await dungeonLobbyService.JoinRoomAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);

        var roomInfo = result.Value!.ToRoomInfoDto();
        var response = new JoinRoomResponse(roomInfo);
        return Ok(response);
    }
    
    [HttpPost("room/{roomId}/leave")]
    public async Task<IActionResult> LeaveRoom([FromBody] LeaveRoomRequest request)
    {
        var result = await dungeonLobbyService.LeaveRoomAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        
        return Ok();
    }
    
    [HttpPost("room/{roomId}/start")]
    public async Task<IActionResult> StartGame([FromBody] StartRoomRequest request)
    {
        var result = await dungeonLobbyService.StartGameAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        
        return Ok();
    }
}
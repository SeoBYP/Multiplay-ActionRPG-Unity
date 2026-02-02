using GameServer.Application.DTOs.DungeonRoom;
using GameServer.Application.DTOs.DungeonRoom.CreateRoom;
using GameServer.Application.DTOs.DungeonRoom.JoinRoom;
using GameServer.Application.DTOs.DungeonRoom.LeaveRoom;
using GameServer.Application.DTOs.DungeonRoom.Room;
using GameServer.Application.DTOs.DungeonRoom.Rooms;
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
        var response = result.Value!.ToGetRoomsResponse();
        return Ok(response);
    }
    
    [HttpPost("getRoom")]
    public async Task<IActionResult> GetRoom([FromBody] GetRoomRequest request)
    {
        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        var dto = result.Value!.ToRoomInfoDto();
        var response = new GetRoomResponse(dto);
        return Ok(response);
    }
    
    [HttpPost("joinRoom")]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequest request)
    {
        var result = await dungeonLobbyService.JoinRoomAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);

        var roomInfo = result.Value!.ToRoomInfoDto();
        var response = new JoinRoomResponse(roomInfo);
        return Ok(response);
    }
    
    [HttpPost("leaveRoom")]
    public async Task<IActionResult> LeaveRoom([FromBody] LeaveRoomRequest request)
    {
        var result = await dungeonLobbyService.LeaveRoomAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        var response = new LeaveRoomResponse(result.IsSuccess);
        return Ok(response);
    }
    
    [HttpPost("startRoom")]
    public async Task<IActionResult> StartGame([FromBody] StartRoomRequest request)
    {
        var result = await dungeonLobbyService.StartGameAsync(request.UserId, request.RoomId);
        if(!result.IsSuccess)
            return BadRequest(result.Message);
        var roomInfo = result.Value!.ToRoomInfoDto();
        var response = new StartRoomResponse(roomInfo);
        return Ok(response);
    }
}
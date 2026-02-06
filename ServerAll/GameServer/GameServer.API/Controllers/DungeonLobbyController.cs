using System.Security.Claims;
using GameServer.Application.DTOs.DungeonRoom;
using GameServer.Application.DTOs.DungeonRoom.CreateRoom;
using GameServer.Application.DTOs.DungeonRoom.JoinRoom;
using GameServer.Application.DTOs.DungeonRoom.LeaveRoom;
using GameServer.Application.DTOs.DungeonRoom.Room;
using GameServer.Application.DTOs.DungeonRoom.StartRoom;
using GameServer.Application.DTOs.DungeonRoom.UpdateRoom;
using GameServer.Application.Services.DungeonLobby.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DungeonLobbyController(IDungeonLobbyService dungeonLobbyService) : ControllerBase
{
    [HttpPost("room")]
    [Authorize]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.CreateDungeonRoomAsync(
            sessionId,
            request.RoomName,
            request.MaxPlayers);

        if (!result.IsSuccess)
            return BadRequest(result.Message);

        var response = result.Value!.ToCreateRoomResponse(); // ! 로 null이 아님 보장
        return Ok(response);
    }

    [HttpGet("rooms")]
    [Authorize]
    public async Task<IActionResult> GetActiveRooms()
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.GetActiveDungeonRoomsAsync();
        if (!result.IsSuccess)
            return BadRequest(result.Message);
        var response = result.Value!.ToGetRoomsResponse();
        return Ok(response);
    }

    [HttpPost("getRoom")]
    [Authorize]
    public async Task<IActionResult> GetRoom([FromBody] GetRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.GetDungeonRoomAsync(request.RoomId);
        if (!result.IsSuccess)
            return BadRequest(result.Message);
        var dto = result.Value!.ToRoomInfoDto();
        var response = new GetRoomResponse(dto);
        return Ok(response);
    }

    [HttpPatch("updateRoom")]
    [Authorize]
    public async Task<IActionResult> UpdateRoom([FromBody] UpdateRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.UpdateRoomSettingsAsync(sessionId, request.RoomId,
            request.RoomName, request.MaxPlayers);
        if (!result.IsSuccess)
            return BadRequest(result.Message);

        var dto = result.Value!.ToRoomInfoDto();
        var response = new UpdateRoomResponse(result.IsSuccess, dto);
        return Ok(response);
    }

    [HttpPost("joinRoom")]
    [Authorize]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.JoinRoomAsync(sessionId, request.RoomId);
        if (!result.IsSuccess)
            return BadRequest(result.Message);

        var roomInfo = result.Value!.ToRoomInfoDto();
        var response = new JoinRoomResponse(roomInfo);
        return Ok(response);
    }

    [HttpPost("leaveRoom")]
    [Authorize]
    public async Task<IActionResult> LeaveRoom([FromBody] LeaveRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.LeaveRoomAsync(sessionId, request.RoomId);
        if (!result.IsSuccess)
            return BadRequest(result.Message);
        var response = new LeaveRoomResponse(result.IsSuccess);
        return Ok(response);
    }

    [HttpPost("startRoom")]
    [Authorize]
    public async Task<IActionResult> StartGame([FromBody] StartRoomRequest request)
    {
        var sessionId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return Unauthorized();
        
        var result = await dungeonLobbyService.StartGameAsync(sessionId, request.RoomId);
        if (!result.IsSuccess)
            return BadRequest(result.Message);
        var roomInfo = result.Value!.ToRoomInfoDto();
        var response = new StartRoomResponse(roomInfo);
        return Ok(response);
    }
}
namespace GameServer.Application.DTOs.Requests;

public record LoginRequest(
    string UserName,
    string Password
);
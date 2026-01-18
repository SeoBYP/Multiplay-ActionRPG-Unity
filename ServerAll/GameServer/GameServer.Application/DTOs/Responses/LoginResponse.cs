namespace GameServer.Application.DTOs.Responses;

public record LoginResponse(
    long UserId,
    string UserName,
    string Email
);
namespace GameServer.Application.DTOs.Responses;

public record RegisterResponse(
    long UserId,
    string UserName,
    string Email,
    DateTime CreatedAt
);
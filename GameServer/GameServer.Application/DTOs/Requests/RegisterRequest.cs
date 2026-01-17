namespace GameServer.Application.DTOs.Requests;

public record RegisterRequest(
    string UserName, 
    string Password,
    string Email
);
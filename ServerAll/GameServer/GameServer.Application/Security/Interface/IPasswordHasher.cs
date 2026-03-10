namespace GameServer.Application.Security.Interface;

public interface IPasswordHasher
{
    public string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}
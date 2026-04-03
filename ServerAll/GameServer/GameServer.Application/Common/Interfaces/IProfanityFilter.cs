namespace GameServer.Application.Common.Interfaces;

public interface IProfanityFilter
{
    string Filter(string message);
    
    bool IsProfane(string message);
}
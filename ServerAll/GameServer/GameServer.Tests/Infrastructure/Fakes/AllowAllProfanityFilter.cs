using GameServer.Application.Common.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes;

public sealed class AllowAllProfanityFilter : IProfanityFilter
{
    public string Filter(string message) => message;

    public bool IsProfane(string message) => false;
}

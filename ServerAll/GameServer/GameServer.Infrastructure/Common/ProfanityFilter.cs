using GameServer.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Infrastructure.Common;

public class ProfanityFilter(ILogger<ProfanityFilter> logger) : IProfanityFilter
{
    public string Filter(string message)
    {
        logger.LogWarning("Profanity detected: {Message}", message);
        return message;
    }

    public bool IsProfane(string message)
    {
        logger.LogDebug("Checking for profanity in message: {Message}", message);
        return true;
    }
}
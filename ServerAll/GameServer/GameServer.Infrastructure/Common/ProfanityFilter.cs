using System;
using System.Linq;
using System.Text.RegularExpressions;
using GameServer.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Infrastructure.Common;

public class ProfanityFilter(ILogger<ProfanityFilter> logger) : IProfanityFilter
{
    private static readonly string[] BlockedTerms =
    [
        "badword",
        "fuck",
        "shit",
        "bitch",
        "asshole"
    ];

    public string Filter(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message;

        var filtered = message;
        foreach (var term in BlockedTerms)
        {
            filtered = Regex.Replace(
                filtered,
                Regex.Escape(term),
                new string('*', term.Length),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        if (!string.Equals(filtered, message, StringComparison.Ordinal))
            logger.LogWarning("Profanity filtered from message");

        return filtered;
    }

    public bool IsProfane(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var isProfane = BlockedTerms.Any(term =>
            message.Contains(term, StringComparison.OrdinalIgnoreCase));

        logger.LogDebug("Checking for profanity in message: {HasProfanity}", isProfane);
        return isProfane;
    }
}

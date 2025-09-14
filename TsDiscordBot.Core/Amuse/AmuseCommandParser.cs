using System;

namespace TsDiscordBot.Core.Amuse;

public class AmuseCommandParser : IAmuseCommandParser
{
    public IAmuseService? Parse(string content)
    {
        content = content.Trim();
        if (!content.StartsWith("tmg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[1].Equals("bj", StringComparison.OrdinalIgnoreCase))
        {
            int bet = 0;
            if (parts.Length >= 3 && int.TryParse(parts[2], out var parsed))
            {
                bet = parsed;
            }
            return new PlayBlackJackService(bet);
        }

        return null;
    }
}

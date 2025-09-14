using System;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class AmuseCommandParser : IAmuseCommandParser
{
    private readonly DatabaseService _databaseService;

    public AmuseCommandParser(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public IAmuseService? Parse(string content)
    {
        content = content.Trim();
        if (!content.StartsWith("tmg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            if (parts[1].Equals("bj", StringComparison.OrdinalIgnoreCase))
            {
                int bet = 0;
                if (parts.Length >= 3 && int.TryParse(parts[2], out var parsed))
                {
                    bet = parsed;
                }
                return new PlayBlackJackService(bet);
            }

            if (parts[1].Equals("cash", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("money", StringComparison.OrdinalIgnoreCase))
            {
                return new ShowCashService(_databaseService);
            }
        }

        return null;
    }
}

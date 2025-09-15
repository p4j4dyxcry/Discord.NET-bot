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
                var bet = ParseBet(parts);
                return new PlayBlackJackService(bet, _databaseService);
            }

            if (parts[1].Equals("dice", StringComparison.OrdinalIgnoreCase))
            {
                var bet = ParseBet(parts);
                return new PlayDiceService(bet, _databaseService);
            }

            if (parts[1].Equals("lw", StringComparison.OrdinalIgnoreCase))
            {
                var bet = ParseBet(parts);
                return new PlayHighLowService(bet, _databaseService);
            }

            if (parts[1].Equals("cash", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("money", StringComparison.OrdinalIgnoreCase))
            {
                return new ShowCashService(_databaseService);
            }

            if (parts[1].Equals("daily", StringComparison.OrdinalIgnoreCase))
            {
                return new DailyBonusService(_databaseService);
            }

            if (parts[1].Equals("top", StringComparison.OrdinalIgnoreCase))
            {
                return new ShowTopCashService(_databaseService);
            }

            if (parts[1].Equals("rank", StringComparison.OrdinalIgnoreCase))
            {
                return new ShowRankService(_databaseService);
            }
        }

        return null;
    }

    private static int ParseBet(string[] parts)
    {
        if (parts.Length >= 3)
        {
            if (parts[2].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return int.MaxValue;
            }

            if (int.TryParse(parts[2], out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}

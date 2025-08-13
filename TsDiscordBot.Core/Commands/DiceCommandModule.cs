using System.Text;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class DiceCommandModule: InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;
    private readonly Random _rand = new();

    public DiceCommandModule(ILogger<DiceCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("dice", "Roll a dice")]
    public async Task RollDice()
    {
        StringBuilder builder = new();
        string message = string.Empty;

        int result = _rand.Next(6);

        string[] dice_characters =
        {
            "\u2680",
            "\u2681",
            "\u2682",
            "\u2683",
            "\u2684",
            "\u2685"
        };

        message = $"# {dice_characters[result]}[{result + 1}]";

        await RespondAsync(message);
    }
}

using System.Linq;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class BeRealCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public BeRealCommandModule(ILogger<BeRealCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("enable-be-real", "画像を投稿すると過去の履歴が見られるようにします")]
    public async Task EnableBeReal()
    {
        var guildId = Context.Guild.Id;
        var channelId = Context.Channel.Id;

        var exists = _databaseService
            .FindAll<BeRealChannel>(BeRealChannel.TableName)
            .Any(x => x.ChannelId == channelId);

        if (!exists)
        {
            var data = new BeRealChannel
            {
                GuildId = guildId,
                ChannelId = channelId,
            };
            _databaseService.Insert(BeRealChannel.TableName, data);
        }

        await RespondAsync("BeRealをこのチャンネルで有効にしたよ！");
    }
}


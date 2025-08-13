using System;
using System.Linq;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class AutoMessageCommandModule: InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public AutoMessageCommandModule(ILogger<AutoMessageCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("auto-message", "AIで会話を促す自動メッセージを設定します。")]
    public async Task RegisterAutoMessage([Summary("t", "メッセージを送信する間隔(時間)")] int t = 1)
    {
        var channelId = Context.Channel.Id;
        var guildId = Context.Guild.Id;

        var existing = _databaseService.FindAll<AutoMessageChannel>(AutoMessageChannel.TableName)
            .FirstOrDefault(x => x.ChannelId == channelId && x.GuildId == guildId);

        if (existing is not null)
        {
            _databaseService.Delete(AutoMessageChannel.TableName, existing.Id);
        }

        var data = new AutoMessageChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            IntervalHours = t,
            LastPostedUtc = DateTime.UtcNow
        };

        _databaseService.Insert(AutoMessageChannel.TableName, data);

        await RespondAsync($"このチャンネルで{t}時間ごとにメッセージを送信するように設定したよ！");
    }
}

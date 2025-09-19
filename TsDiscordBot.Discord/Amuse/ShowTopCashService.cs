using System.Globalization;
using System.Text;
using Discord.WebSocket;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class ShowTopCashService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private readonly DiscordSocketClient _discordSocketClient;

    public ShowTopCashService(DatabaseService databaseService, DiscordSocketClient discordSocketClient)
    {
        _databaseService = databaseService;
        _discordSocketClient = discordSocketClient;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var guild = _discordSocketClient.GetGuild(message.GuildId);

        var users = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName);

        if (guild is not null)
        {
            users = users.Where(x => guild.GetUser(x.UserId) is not null);
        }

        var topUsers = users
            .OrderByDescending(x => x.Cash)
            .Take(10)
            .ToArray();

        if (topUsers.Length == 0)
        {
            return message.ReplyMessageAsync("まだ誰もGAL円を持っていないよ！");
        }

        var sb = new StringBuilder();
        for (var i = 0; i < topUsers.Length; i++)
        {
            var rank = i + 1;
            var formattedCash = topUsers[i].Cash.ToString("N0", CultureInfo.InvariantCulture);
            sb.AppendLine($"{rank}. <@{topUsers[i].UserId}>　`{formattedCash}` GAL円");
        }

        var options = new MessageSendOptions
        {
            Embed = new MessageEmbed
            {
                Title = "💰 所持金ランキング TOP10",
                Description = sb.ToString().TrimEnd(),
                Color = MessageColor.FromHex(0xFFD700),
            },
            MentionHandling = MentionHandling.SuppressAll,
        };

        return message.ReplyMessageAsync(options);
    }
}

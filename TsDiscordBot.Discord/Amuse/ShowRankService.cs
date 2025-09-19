using System.Text;
using Discord.WebSocket;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class ShowRankService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private readonly DiscordSocketClient _discordSocketClient;

    public ShowRankService(DatabaseService databaseService, DiscordSocketClient discordSocketClient)
    {
        _databaseService = databaseService;
        _discordSocketClient = discordSocketClient;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var guild = _discordSocketClient.GetGuild(message.GuildId);

        var usersQuery = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName);

        if (guild is not null)
        {
            usersQuery = usersQuery.Where(x => guild.GetUser(x.UserId) is not null);
        }

        var users = usersQuery
            .OrderByDescending(x => x.Cash)
            .ToArray();

        if (users.Length == 0)
        {
            return message.ReplyMessageAsync("まだ誰もGAL円を持っていないよ！");
        }

        var index = Array.FindIndex(users, x => x.UserId == message.AuthorId);
        if (index < 0)
        {
            return message.ReplyMessageAsync("あなたはまだGAL円を持っていないよ！");
        }

        var rank = index + 1;
        var sb = new StringBuilder();

        var top = users[0];
        var topLine = $"1. <@{top.UserId}>　{top.Cash}GAL円";

        sb.AppendLine(topLine);

        if (index > 1)
        {
            var above = users[index - 1];
            sb.AppendLine($"{rank - 1}. <@{above.UserId}>　{above.Cash}GAL円");
        }

        if (index > 0)
        {
            var self = users[index];
            sb.AppendLine($"{rank}. <@{self.UserId}>　{self.Cash}GAL円");
        }

        if (index < users.Length - 1)
        {
            var below = users[index + 1];
            sb.AppendLine($"{rank + 1}. <@{below.UserId}>　{below.Cash}GAL円");
        }

        var options = new MessageSendOptions
        {
            Embed = new MessageEmbed
            {
                Title = "🏆 現在のランキング",
                Description = sb.ToString().TrimEnd(),
                Color = MessageColor.FromHex(0xFFD700),
            },
            MentionHandling = MentionHandling.SuppressAll,
        };

        return message.ReplyMessageAsync(options);
    }
}

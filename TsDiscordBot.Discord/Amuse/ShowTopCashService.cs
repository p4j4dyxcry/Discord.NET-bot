using System.Text;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class ShowTopCashService : IAmuseService
{
    private readonly DatabaseService _databaseService;

    public ShowTopCashService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var topUsers = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
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
            sb.AppendLine($"{rank}. <@{topUsers[i].UserId}>　{topUsers[i].Cash}GAL円");
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

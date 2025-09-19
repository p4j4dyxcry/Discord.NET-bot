using System.Text;
using Discord.WebSocket;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class ShowTopWinRateService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly string _gameKind;
    private readonly string _gameName;

    public ShowTopWinRateService(string gameKind, string gameName, DatabaseService databaseService, DiscordSocketClient discordSocketClient)
    {
        _gameKind = gameKind;
        _gameName = gameName;
        _databaseService = databaseService;
        _discordSocketClient = discordSocketClient;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var guild = _discordSocketClient.GetGuild(message.GuildId);

        var recordsQuery = _databaseService
            .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
            .Where(x => x.GameKind == _gameKind && x.TotalPlays > 0);

        if (guild is not null)
        {
            recordsQuery = recordsQuery.Where(x => guild.GetUser(x.UserId) is not null);
        }

        var records = recordsQuery
            .OrderByDescending(x => (double)x.WinCount / x.TotalPlays)
            .ThenByDescending(x => x.TotalPlays)
            .Take(10)
            .ToArray();

        if (records.Length == 0)
        {
            return message.ReplyMessageAsync($"まだ誰も{_gameName}をプレイしていないよ！");
        }

        var sb = new StringBuilder();
        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            var rate = r.WinCount * 100.0 / r.TotalPlays;
            var rank = i + 1;
            sb.AppendLine($"{rank}. <@{r.UserId}>　{rate:0.##}% ({r.WinCount}/{r.TotalPlays})");
        }

        var options = new MessageSendOptions
        {
            Embed = new MessageEmbed
            {
                Title = $"🎮 {_gameName} 勝率 TOP10",
                Description = sb.ToString().TrimEnd(),
                Color = MessageColor.FromHex(0x0000FF),
            },
            MentionHandling = MentionHandling.SuppressAll,
        };

        return message.ReplyMessageAsync(options);
    }
}


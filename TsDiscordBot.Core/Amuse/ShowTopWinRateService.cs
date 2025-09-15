using System.Linq;
using System.Text;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class ShowTopWinRateService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private readonly string _gameKind;
    private readonly string _gameName;

    public ShowTopWinRateService(string gameKind, string gameName, DatabaseService databaseService)
    {
        _gameKind = gameKind;
        _gameName = gameName;
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var records = _databaseService
            .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
            .Where(x => x.GameKind == _gameKind && x.TotalPlays > 0)
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

        return message.ReplyMessageAsync(sb.ToString().TrimEnd());
    }
}


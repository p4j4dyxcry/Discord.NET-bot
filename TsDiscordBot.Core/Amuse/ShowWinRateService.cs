using System.Linq;
using System.Text;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class ShowWinRateService : IAmuseService
{
    private readonly DatabaseService _databaseService;

    public ShowWinRateService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var records = _databaseService
            .FindAll<AmuseGameRecord>(AmuseGameRecord.TableName)
            .Where(x => x.UserId == message.AuthorId)
            .ToArray();

        if (records.Length == 0)
        {
            return message.ReplyMessageAsync("あなたはまだゲームをプレイしていないよ！");
        }

        var sb = new StringBuilder();
        foreach (var record in records)
        {
            var rate = record.TotalPlays > 0
                ? record.WinCount * 100.0 / record.TotalPlays
                : 0;
            var gameName = record.GameKind switch
            {
                "BJ" => "ブラックジャック",
                "DI" => "サイコロゲーム",
                _ => record.GameKind
            };
            sb.AppendLine($"{gameName}: {rate:0.##}% ({record.WinCount}/{record.TotalPlays})");
        }

        return message.ReplyMessageAsync(sb.ToString().TrimEnd());
    }
}


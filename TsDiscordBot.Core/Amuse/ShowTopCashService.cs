using System.Linq;
using System.Text;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

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

        return message.ReplyMessageAsync(sb.ToString().TrimEnd());
    }
}

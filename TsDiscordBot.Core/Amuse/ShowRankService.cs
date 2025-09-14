using System.Linq;
using System.Text;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class ShowRankService : IAmuseService
{
    private readonly DatabaseService _databaseService;

    public ShowRankService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var users = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
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
        var topLine = $"1. <@{top.UserId}>さん　{top.Cash}GAL円　←１位";
        if (index == 0)
        {
            topLine += " ←あなた";
        }
        else if (index == 1)
        {
            topLine += " ←あなたの1個上";
        }
        sb.AppendLine(topLine);

        if (index > 1)
        {
            var above = users[index - 1];
            sb.AppendLine($"{rank - 1}. <@{above.UserId}>さん　{above.Cash}GAL円　←あなたの1個上");
        }

        if (index > 0)
        {
            var self = users[index];
            sb.AppendLine($"{rank}. <@{self.UserId}>さん　{self.Cash}GAL円　←あなた");
        }

        if (index < users.Length - 1)
        {
            var below = users[index + 1];
            sb.AppendLine($"{rank + 1}. <@{below.UserId}>さん　{below.Cash}GAL円　←あなたの1個下");
        }

        return message.ReplyMessageAsync(sb.ToString().TrimEnd());
    }
}

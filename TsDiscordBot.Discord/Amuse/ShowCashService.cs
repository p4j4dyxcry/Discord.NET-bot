using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class ShowCashService : IAmuseService
{
    private readonly DatabaseService _databaseService;

    public ShowCashService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == message.AuthorId);

        var amount = cash?.Cash ?? 0;
        return message.ReplyMessageAsync($"{message.AuthorMention}さんは現在{amount}GAL円を保持しています。");
    }
}


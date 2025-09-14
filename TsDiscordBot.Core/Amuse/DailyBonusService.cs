using System;
using System.Linq;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Amuse;

public class DailyBonusService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private const long BonusAmount = 1000;

    public DailyBonusService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var utcNow = DateTime.UtcNow;
        var jstNow = utcNow.AddHours(9);

        var cash = _databaseService
            .FindAll<AmuseCash>(AmuseCash.TableName)
            .FirstOrDefault(x => x.UserId == message.AuthorId);

        if (cash is not null && cash.LastEarnedAtUtc.HasValue)
        {
            var lastJst = cash.LastEarnedAtUtc.Value.AddHours(9);
            if (lastJst.Date == jstNow.Date)
            {
                var nextReset = jstNow.Date.AddDays(1);
                var remaining = nextReset - jstNow;
                return message.ReplyMessageAsync($"今日は既にデイリーボーナスを取得済みだよ！{(int)remaining.TotalHours}時間{remaining.Minutes}分後に取得できます。");
            }
        }

        if (cash is null)
        {
            cash = new AmuseCash
            {
                UserId = message.AuthorId,
                Cash = BonusAmount,
                LastEarnedAtUtc = utcNow,
                LastUpdatedAtUtc = utcNow
            };
            _databaseService.Insert(AmuseCash.TableName, cash);
        }
        else
        {
            cash.Cash += BonusAmount;
            cash.LastEarnedAtUtc = utcNow;
            cash.LastUpdatedAtUtc = utcNow;
            _databaseService.Update(AmuseCash.TableName, cash);
        }

        return message.ReplyMessageAsync($"{message.AuthorMention}さんにデイリーボーナス{BonusAmount}GALを付与したよ！");
    }
}


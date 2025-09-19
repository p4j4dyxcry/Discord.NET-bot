using System.Globalization;
using TsDiscordBot.Core.Messaging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class DailyBonusService : IAmuseService
{
    private readonly DatabaseService _databaseService;
    private const long DefaultBonusAmount = 1000;

    public DailyBonusService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task ExecuteAsync(IMessageData message)
    {
        var utcNow = DateTime.UtcNow;
        var jstNow = utcNow.AddHours(9);

        var (bonusAmount, replyMessage) = GetBonus(jstNow, message.AuthorMention);

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
                Cash = bonusAmount,
                LastEarnedAtUtc = utcNow,
                LastUpdatedAtUtc = utcNow
            };
            _databaseService.Insert(AmuseCash.TableName, cash);
        }
        else
        {
            cash.Cash += bonusAmount;
            cash.LastEarnedAtUtc = utcNow;
            cash.LastUpdatedAtUtc = utcNow;
            _databaseService.Update(AmuseCash.TableName, cash);
        }

        return message.ReplyMessageAsync(replyMessage);
    }

    private static (long Amount, string Message) GetBonus(DateTime jstNow, string mention)
    {
        if (jstNow.Month == 2 && jstNow.Day == 14)
        {
            const long amount = 5000;
            return (amount, $"{mention}さん、今日はバレンタインね！チョコ代わりにどうぞ！{FormatAmount(amount)}だよ～！");
        }

        if (jstNow.Month == 12 && (jstNow.Day == 24 || jstNow.Day == 25))
        {
            const long amount = 5000;
            return (amount, $"{mention}さん、メリークリスマス！プレゼントに{FormatAmount(amount)}どうぞ！");
        }

        if (jstNow.Month == 12 && jstNow.Day == 31)
        {
            const long amount = 3000;
            return (amount, $"{mention}さん、今日は大晦日ね！特別{FormatAmount(amount)}だよ～！");
        }

        if (jstNow.Month == 1 && jstNow.Day == 1)
        {
            const long amount = 3000;
            return (amount, $"{mention}さん、あけましておめでとう！お年玉として{FormatAmount(amount)}だよ～！");
        }

        if (jstNow.Day == 7 || jstNow.Day == 17 || jstNow.Day == 27)
        {
            const long amount = 3000;
            return (amount, $"{mention}さん、今日は7のつく日だから特別{FormatAmount(amount)}ね！");
        }

        return (DefaultBonusAmount, $"{mention}さん、今日もきてくれてありがとう！はい{FormatAmount(DefaultBonusAmount)}だよ～！");
    }

    private static string FormatAmount(long amount)
    {
        var formatted = amount.ToString("N0", CultureInfo.InvariantCulture);
        return $"`{formatted}` GAL円";
    }
}


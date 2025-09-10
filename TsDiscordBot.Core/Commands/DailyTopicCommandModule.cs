using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class DailyTopicCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _databaseService;

    public DailyTopicCommandModule(ILogger<DailyTopicCommandModule> logger, DatabaseService databaseService)
    {
        _ = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("enable-daily-topic", "rand_topicsから日替わりトピックを投稿します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task EnableDailyTopic([Summary("time", "投稿する時刻 (HH:mm, 日本時間 GMT+9:00)")] string? time = null)
    {
        var guildId = Context.Guild.Id;
        var channelId = Context.Channel.Id;

        TimeSpan postAt;
        if (string.IsNullOrWhiteSpace(time))
        {
            postAt = new TimeSpan(7, 0, 0);
        }
        else if (!TimeSpan.TryParse(time, out postAt))
        {
            await RespondAsync("時刻の形式が正しくないよ！（例: 07:00）");
            return;
        }

        TimeZoneInfo jst;
        try
        {
            jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        }
        catch (TimeZoneNotFoundException)
        {
            jst = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }

        var nowJst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
        var lastPostedUtc = DateTime.UtcNow;
        if (nowJst.TimeOfDay < postAt)
        {
            lastPostedUtc = lastPostedUtc.AddDays(-1);
        }

        var existing = _databaseService
            .FindAll<DailyTopicChannel>(DailyTopicChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is not null)
        {
            _databaseService.Delete(DailyTopicChannel.TableName, existing.Id);
        }

        var data = new DailyTopicChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            PostAtJst = postAt,
            LastPostedUtc = lastPostedUtc
        };

        _databaseService.Insert(DailyTopicChannel.TableName, data);

        await RespondAsync($"毎日 {postAt:hh\\:mm} にこのチャンネルでトピックを投稿するように設定したよ！");
    }

    [SlashCommand("disable-daily-topic", "日替わりトピックの投稿を停止します。")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task DisableDailyTopic()
    {
        var guildId = Context.Guild.Id;

        var existing = _databaseService
            .FindAll<DailyTopicChannel>(DailyTopicChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is null)
        {
            await RespondAsync("このサーバーではdaily-topicは設定されていないよ！");
            return;
        }

        _databaseService.Delete(DailyTopicChannel.TableName, existing.Id);
        await RespondAsync("daily-topicを解除したよ！");
    }
}

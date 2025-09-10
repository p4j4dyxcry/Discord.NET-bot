using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class DailyTopicCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DatabaseService _databaseService;
    private readonly RandTopicService _randTopicService;

    public DailyTopicCommandModule(ILogger<DailyTopicCommandModule> logger, DatabaseService databaseService, RandTopicService randTopicService)
    {
        _ = logger;
        _databaseService = databaseService;
        _randTopicService = randTopicService;
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

    [SlashCommand("debug-daily-topic", "今日の日付に基づくdaily-topicを今すぐ実行します。(DBの更新無し)")]
    public async Task DebugDailyTopic()
    {
        var guildId = Context.Guild.Id;

        var config = _databaseService
            .FindAll<DailyTopicChannel>(DailyTopicChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (config is null)
        {
            await RespondAsync("このサーバーではdaily-topicは設定されていないよ！");
            return;
        }

        SocketTextChannel? channel = Context.Client.GetChannel(config.ChannelId) as SocketTextChannel
            ?? Context.Guild.GetTextChannel(config.ChannelId);

        if (channel is null)
        {
            await RespondAsync("設定されているチャンネルが見つからないよ！");
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
        var topic = _randTopicService.GetTopic(nowJst);

        if (string.IsNullOrEmpty(topic))
        {
            await RespondAsync("今日のトピックが見つからないよ！");
            return;
        }

        await channel.SendMessageAsync(topic);

        await RespondAsync($"チャンネル<#{config.ChannelId}>でdaily-topicを送信したよ！");
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

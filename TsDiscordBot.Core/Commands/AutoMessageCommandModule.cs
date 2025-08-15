using System;
using System.Linq;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class AutoMessageCommandModule: InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public AutoMessageCommandModule(ILogger<AutoMessageCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("auto-message", "AIで会話を促す自動メッセージを設定します。")]
    public async Task RegisterAutoMessage(
        [Summary("t", "メッセージを送信する間隔(時間)")] int t = 1,
        [Summary("c", "メッセージを送信するチャンネル")] SocketTextChannel? channel = null,
        [Summary("s", "最初のメッセージを送信する時刻 (HH:mm, 日本時間 GMT+9:00)")] string? startTime = null)
    {
        var channelId = channel?.Id ?? Context.Channel.Id;
        var guildId = Context.Guild.Id;

        var existing = _databaseService
            .FindAll<AutoMessageChannel>(AutoMessageChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is not null && existing.ChannelId != channelId)
        {
            await RespondAsync($"このサーバーでは既に自動メッセージが<#{existing.ChannelId}>に設定されているよ！まずは`/remove-auto-message`で解除してね。");
            return;
        }

        if (existing is not null)
        {
            _databaseService.Delete(AutoMessageChannel.TableName, existing.Id);
        }

        DateTime lastPostedUtc;
        string startMessage;

        if (!string.IsNullOrWhiteSpace(startTime))
        {
            if (TimeSpan.TryParse(startTime, out var time))
            {
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
                var startLocal = new DateTime(nowJst.Year, nowJst.Month, nowJst.Day, time.Hours, time.Minutes, 0);
                if (startLocal <= nowJst)
                {
                    startLocal = startLocal.AddDays(1);
                }

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, jst);
                lastPostedUtc = startUtc.AddHours(-t);
                startMessage = $"{startLocal:HH:mm}から";
            }
            else
            {
                await RespondAsync("開始時刻の形式が正しくないよ！（例: 09:00）");
                return;
            }
        }
        else
        {
            lastPostedUtc = DateTime.UtcNow;
            startMessage = "今";
        }

        var data = new AutoMessageChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            IntervalHours = t,
            LastPostedUtc = lastPostedUtc
        };

        _databaseService.Insert(AutoMessageChannel.TableName, data);

        await RespondAsync($"チャンネル<#{channelId}>で{startMessage}{t}時間ごとにメッセージを送信するように設定したよ！");
    }

    [SlashCommand("show-auto-message", "AIで会話を促す自動メッセージの現在の設定を表示します。")]
    public async Task ShowAutoMessage()
    {
        var guildId = Context.Guild.Id;

        var existing = _databaseService
            .FindAll<AutoMessageChannel>(AutoMessageChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is null)
        {
            await RespondAsync("このサーバーでは自動メッセージは設定されていないよ！");
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

        var nextUtc = existing.LastPostedUtc;
        if (nextUtc.Kind != DateTimeKind.Utc)
        {
            nextUtc = DateTime.SpecifyKind(nextUtc, DateTimeKind.Utc);
        }

        var nextLocal = TimeZoneInfo.ConvertTimeFromUtc(nextUtc.AddHours(existing.IntervalHours), jst);
        await RespondAsync($"チャンネル<#{existing.ChannelId}>で{existing.IntervalHours}時間ごとにメッセージを送信するよう設定されているよ！次の送信予定時刻は{nextLocal:yyyy/MM/dd HH:mm}だよ。");
    }

    [SlashCommand("overwrite-auto-message", "AIで会話を促す自動メッセージの設定を上書きします。")]
    public async Task OverwriteAutoMessage(
        [Summary("t", "メッセージを送信する間隔(時間)")] int t = 1,
        [Summary("c", "メッセージを送信するチャンネル")] SocketTextChannel? channel = null,
        [Summary("s", "最初のメッセージを送信する時刻 (HH:mm, 日本時間 GMT+9:00)")] string? startTime = null)
    {
        var channelId = channel?.Id ?? Context.Channel.Id;
        var guildId = Context.Guild.Id;

        var existing = _databaseService
            .FindAll<AutoMessageChannel>(AutoMessageChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is not null)
        {
            _databaseService.Delete(AutoMessageChannel.TableName, existing.Id);
        }

        DateTime lastPostedUtc;
        string startMessage;

        if (!string.IsNullOrWhiteSpace(startTime))
        {
            if (TimeSpan.TryParse(startTime, out var time))
            {
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
                var startLocal = new DateTime(nowJst.Year, nowJst.Month, nowJst.Day, time.Hours, time.Minutes, 0);
                if (startLocal <= nowJst)
                {
                    startLocal = startLocal.AddDays(1);
                }

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, jst);
                lastPostedUtc = startUtc.AddHours(-t);
                startMessage = $"{startLocal:HH:mm}から";
            }
            else
            {
                await RespondAsync("開始時刻の形式が正しくないよ！（例: 09:00）");
                return;
            }
        }
        else
        {
            lastPostedUtc = DateTime.UtcNow;
            startMessage = "今";
        }

        var data = new AutoMessageChannel
        {
            GuildId = guildId,
            ChannelId = channelId,
            IntervalHours = t,
            LastPostedUtc = lastPostedUtc
        };

        _databaseService.Insert(AutoMessageChannel.TableName, data);

        await RespondAsync($"チャンネル<#{channelId}>で{startMessage}{t}時間ごとにメッセージを送信するように上書き設定したよ！");
    }

    [SlashCommand("remove-auto-message", "AIで会話を促す自動メッセージの設定を解除します。")]
    public async Task UnregisterAutoMessage()
    {
        var guildId = Context.Guild.Id;

        var existing = _databaseService
            .FindAll<AutoMessageChannel>(AutoMessageChannel.TableName)
            .FirstOrDefault(x => x.GuildId == guildId);

        if (existing is null)
        {
            await RespondAsync("このサーバーでは自動メッセージは設定されていないよ！");
            return;
        }

        _databaseService.Delete(AutoMessageChannel.TableName, existing.Id);

        await RespondAsync($"チャンネル<#{existing.ChannelId}>での自動メッセージ設定を解除したよ！");
    }
}

using System;
using System.Linq;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.Commands;

public class ReminderCommandModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger _logger;
    private readonly DatabaseService _databaseService;

    public ReminderCommandModule(ILogger<ReminderCommandModule> logger, DatabaseService databaseService)
    {
        _logger = logger;
        _databaseService = databaseService;
    }

    [SlashCommand("remind", "指定した時刻にリマインドを設定します。")]
    public async Task RegisterReminder(
        [Summary("time", "リマインドする時刻 (yyyy/MM/dd HH:mm JST)")] string time,
        [Summary("message", "リマインド内容")] string message)
    {
        if (!DateTime.TryParse(time, out var localTime))
        {
            await RespondAsync("時刻の形式が正しくないよ！（例: 2024/01/01 09:00）");
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

        var data = new Reminder
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            UserId = Context.User.Id,
            RemindAtUtc = TimeZoneInfo.ConvertTimeToUtc(localTime, jst),
            Message = message
        };

        _databaseService.Insert(Reminder.TableName, data);

        var confirmLocal = TimeZoneInfo.ConvertTimeFromUtc(data.RemindAtUtc, jst);
        await RespondAsync($"{confirmLocal:yyyy/MM/dd HH:mm}にリマインドするね！");
    }

    [SlashCommand("remind-list", "あなたのリマインドを一覧表示します。")]
    public async Task ListReminder()
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        var reminders = _databaseService
            .FindAll<Reminder>(Reminder.TableName)
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .ToArray();

        if (reminders.Length == 0)
        {
            await RespondAsync("リマインドは登録されていないよ！");
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

        var lines = reminders
            .Select(x => TimeZoneInfo.ConvertTimeFromUtc(x.RemindAtUtc, jst))
            .Select(time => $"- {time:yyyy/MM/dd HH:mm}");

        await RespondAsync("登録されているリマインドは以下の通りだよ:\n" + string.Join('\n', lines));
    }

    [SlashCommand("remind-remove", "あなたのリマインドを全て削除します。")]
    public async Task RemoveReminder()
    {
        var guildId = Context.Guild.Id;
        var userId = Context.User.Id;

        var reminders = _databaseService
            .FindAll<Reminder>(Reminder.TableName)
            .Where(x => x.GuildId == guildId && x.UserId == userId)
            .ToArray();

        foreach (var reminder in reminders)
        {
            _databaseService.Delete(Reminder.TableName, reminder.Id);
        }

        await RespondAsync($"{reminders.Length}件のリマインドを削除したよ！");
    }
}

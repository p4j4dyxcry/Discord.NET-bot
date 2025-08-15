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
        [Summary("time", "リマインドする時刻 (yyyy/MM/dd HH:mm)")] string time,
        [Summary("message", "リマインド内容")] string message)
    {
        if (!DateTime.TryParse(time, out var localTime))
        {
            await RespondAsync("時刻の形式が正しくないよ！（例: 2024/01/01 09:00）");
            return;
        }

        var data = new Reminder
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            UserId = Context.User.Id,
            RemindAtUtc = localTime.ToUniversalTime(),
            Message = message
        };

        _databaseService.Insert(Reminder.TableName, data);

        await RespondAsync($"{localTime:yyyy/MM/dd HH:mm}にリマインドするね！");
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

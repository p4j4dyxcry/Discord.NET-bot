using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class ReminderService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ReminderService> _logger;
    private readonly DatabaseService _databaseService;

    public ReminderService(DiscordSocketClient client, ILogger<ReminderService> logger, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var reminders = _databaseService.FindAll<Reminder>(Reminder.TableName).ToArray();

                foreach (var reminder in reminders)
                {
                    var remindAtUtc = DateTime.SpecifyKind(reminder.RemindAtUtc, DateTimeKind.Utc);

                    if (DateTime.UtcNow >= remindAtUtc)
                    {
                        if (_client.GetChannel(reminder.ChannelId) is ISocketMessageChannel channel)
                        {
                            await channel.SendMessageAsync($"<@{reminder.UserId}> {reminder.Message}");
                        }

                        _databaseService.Delete(Reminder.TableName, reminder.Id);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process reminders");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

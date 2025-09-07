using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class AutoDeleteService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AutoDeleteService> _logger;
    private readonly DatabaseService _databaseService;

    public AutoDeleteService(DiscordSocketClient client, ILogger<AutoDeleteService> logger, DatabaseService databaseService)
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
                var configs = _databaseService.FindAll<AutoDeleteChannel>(AutoDeleteChannel.TableName).ToArray();

                foreach (var config in configs)
                {
                    if (config.EnabledAtUtc == default || config.LastMessageId == default)
                    {
                        var now = DateTime.UtcNow;
                        config.EnabledAtUtc = now;
                        config.LastMessageId = SnowflakeUtils.ToSnowflake(now);
                        _databaseService.Update(AutoDeleteChannel.TableName, config);
                    }

                    if (_client.GetChannel(config.ChannelId) is ITextChannel channel)
                    {
                        var messages = await channel.GetMessagesAsync(config.LastMessageId, Direction.After, 100).FlattenAsync();
                        var targets = messages
                            .Where(x => !x.IsPinned)
                            .Where(x => !(x.Author.IsBot && x.Content.EndsWith("分後にメッセージを自動削除するよう設定したよ！")))
                            .OrderBy(x => x.Id)
                            .ToArray();

                        if (targets.Length > 0)
                        {
                            foreach (var msg in targets)
                            {
                                var data = new AutoDeleteMessage
                                {
                                    ChannelId = config.ChannelId,
                                    MessageId = msg.Id,
                                    DeleteAtUtc = msg.Timestamp.UtcDateTime.AddMinutes(config.DelayMinutes)
                                };

                                _databaseService.Insert(AutoDeleteMessage.TableName, data);
                            }

                            config.LastMessageId = targets.Last().Id;
                            _databaseService.Update(AutoDeleteChannel.TableName, config);
                        }
                    }
                }

                var entries = _databaseService.FindAll<AutoDeleteMessage>(AutoDeleteMessage.TableName).ToArray();

                foreach (var entry in entries.Where(x => DateTime.UtcNow >= DateTime.SpecifyKind(x.DeleteAtUtc, DateTimeKind.Utc)))
                {
                    if (_client.GetChannel(entry.ChannelId) is ISocketMessageChannel channel)
                    {
                        var msg = await channel.GetMessageAsync(entry.MessageId);
                        if (msg is null || msg.IsPinned)
                        {
                            _databaseService.Delete(AutoDeleteMessage.TableName, entry.Id);
                            continue;
                        }

                        await msg.DeleteAsync();
                        _databaseService.Delete(AutoDeleteMessage.TableName, entry.Id);
                    }
                    else
                    {
                        _databaseService.Delete(AutoDeleteMessage.TableName, entry.Id);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process auto delete messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

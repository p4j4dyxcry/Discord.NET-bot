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

public class AutoDeleteService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AutoDeleteService> _logger;
    private readonly DatabaseService _databaseService;

    private AutoDeleteChannel[] _cache = [];
    private DateTime _lastFetchTime = DateTime.MinValue;
    private readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public AutoDeleteService(DiscordSocketClient client, ILogger<AutoDeleteService> logger, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived += OnMessageReceivedAsync;
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived -= OnMessageReceivedAsync;
        return base.StopAsync(cancellationToken);
    }

    private Task OnMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            if (message.Author.IsBot || message.Channel is not SocketGuildChannel)
                return Task.CompletedTask;

            if ((DateTime.UtcNow - _lastFetchTime) > CacheDuration)
            {
                var list = _databaseService.FindAll<AutoDeleteChannel>(AutoDeleteChannel.TableName);
                _cache = list.ToArray();
                _lastFetchTime = DateTime.UtcNow;
            }

            var config = _cache.FirstOrDefault(x => x.ChannelId == message.Channel.Id);
            if (config is null)
                return Task.CompletedTask;

            var data = new AutoDeleteMessage
            {
                ChannelId = message.Channel.Id,
                MessageId = message.Id,
                DeleteAtUtc = DateTime.UtcNow.AddMinutes(config.DelayMinutes)
            };

            _databaseService.Insert(AutoDeleteMessage.TableName, data);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle message for auto delete");
        }

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = _databaseService.FindAll<AutoDeleteMessage>(AutoDeleteMessage.TableName).ToArray();

                foreach (var entry in entries.Where(x => DateTime.UtcNow >= DateTime.SpecifyKind(x.DeleteAtUtc, DateTimeKind.Utc)))
                {
                    if (_client.GetChannel(entry.ChannelId) is ISocketMessageChannel channel)
                    {
                        var msg = await channel.GetMessageAsync(entry.MessageId);
                        if (msg is null || msg.Author.IsBot || msg.IsPinned)
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

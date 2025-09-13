using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class TriggerReactionService : IHostedService
{
    private readonly IMessageReceiver _client;
    private readonly ILogger<TriggerReactionService> _logger;
    private readonly DatabaseService _databaseService;
    private TriggerReactionPost[] _cache = Array.Empty<TriggerReactionPost>();
    private DateTime _lastExecuteDate = DateTime.Now;

    private readonly TimeSpan QuerySpan = TimeSpan.FromSeconds(5);
    private IDisposable? _subscription;

    public TriggerReactionService(IMessageReceiver client, ILogger<TriggerReactionService> logger, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _client.OnReceivedSubscribe(OnMessageReceivedAsync, nameof(TriggerReactionService), ServicePriority.Low);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(IMessageData message)
    {
        try
        {
            TimeSpan timeSpan = DateTime.Now - _lastExecuteDate;

            // Should be reduced calling, because the API is need to access the DB.
            if (timeSpan > QuerySpan)
            {
                IEnumerable<TriggerReactionPost> collection = await _databaseService
                    .FindAllAsync<TriggerReactionPost>(TriggerReactionPost.TableName);

                _cache = collection.ToArray();
                _lastExecuteDate = DateTime.Now;
            }

            IEnumerable<TriggerReactionPost> settings = _cache
                .Where(x => x.GuildId == message.GuildId)
                .Where(x => !string.IsNullOrWhiteSpace(x.TriggerWord));

            foreach (var config in settings)
            {
                if (!message.Content.Contains(config.TriggerWord, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await message.TryAddReactionAsync(config.Reaction);
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e,"Failed to reaction");
        }
    }
}
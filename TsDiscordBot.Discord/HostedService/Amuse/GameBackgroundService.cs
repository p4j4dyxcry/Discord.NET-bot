using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Amuse;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.HostedService.Amuse;

public class GameBackgroundService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<GameBackgroundService> _logger;
    private readonly DatabaseService _databaseService;
    private readonly AmuseGameManager _amuseGameManager;

    public GameBackgroundService(DiscordSocketClient client, ILogger<GameBackgroundService> logger, DatabaseService databaseService,EmoteDatabase emoteDatabase)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
        _amuseGameManager = new AmuseGameManager(client, databaseService, emoteDatabase);
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.ButtonExecuted += OnButtonExecuted;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    var plays = _databaseService
                        .FindAll<AmusePlay>(AmusePlay.TableName)
                        .ToArray();

                    await _amuseGameManager.ProcessAsync(plays);
                }, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to execute amuse background service");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        _client.ButtonExecuted -= OnButtonExecuted;
    }

    private async Task OnButtonExecuted(SocketMessageComponent component)
    {
        try
        {
            await _amuseGameManager.OnUpdateMessageAsync(component);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process amuse button execution");
        }
    }

    private async Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken stoppingToken)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await operation();
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Attempt {Attempt} to process amuse games failed. Retrying in {Delay}.", attempt, delay);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 8));
            }
        }
    }
}


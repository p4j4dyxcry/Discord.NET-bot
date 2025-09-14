using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Amuse;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService.Amuse;

public class GameBackgroundService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<GameBackgroundService> _logger;
    private readonly DatabaseService _databaseService;

    private readonly IAmuseBackgroundLogic[] _amuseBackgroundLogics;

    public GameBackgroundService(DiscordSocketClient client, ILogger<GameBackgroundService> logger, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;

        _amuseBackgroundLogics =
        [
            new BlackJackBackgroundLogic(databaseService,logger,client),
            new DiceBackgroundLogic(databaseService,logger,client)
        ];
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.ButtonExecuted += OnButtonExecuted;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var plays = _databaseService
                    .FindAll<AmusePlay>(AmusePlay.TableName)
                    .ToArray();

                foreach (var logic in _amuseBackgroundLogics)
                {
                    try
                    {
                        await logic.ProcessAsync(plays);
                    }
                    catch(Exception e)
                    {
                        _logger.LogError(e, $"Failed to {logic.GetType()}.ProcessAsync");
                    }

                }

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
        foreach (var logic in _amuseBackgroundLogics)
        {
            try
            {
                await logic.OnButtonExecutedAsync(component);
            }
            catch(Exception e)
            {
                _logger.LogError(e, $"Failed to {logic.GetType()}.OnButtonExecutedAsync");
            }
        }
    }
}


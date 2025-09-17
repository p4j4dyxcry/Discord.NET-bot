using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Utility;

namespace TsDiscordBot.Discord.HostedService;

public class InteractionHandlingService : IHostedService
{
    private readonly DiscordSocketClient _discord;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<InteractionService> _logger;

    public InteractionHandlingService(
        DiscordSocketClient discord,
        InteractionService interactions,
        IServiceProvider services,
        IConfiguration config,
        ILogger<InteractionService> logger)
    {
        _discord = discord;
        _interactions = interactions;
        _services = services;
        _config = config;
        _logger = logger;

        _interactions.Log += msg => LogHelper.OnLogAsync(logger, msg);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discord.Ready += () => _interactions.RegisterCommandsGloballyAsync(true);
        _discord.InteractionCreated += OnInteractionAsync;

        //Registered commands modules used by reflection.
        var modules = await _interactions.AddModulesAsync(Assembly.GetAssembly(typeof(InteractionHandlingService)), _services);
        foreach (ModuleInfo module in modules.Where(x=> x is not null))
        {
            _logger.LogInformation($"Registered module:{module.Name}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _interactions.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            if (interaction is SocketMessageComponent smc &&
                smc.Data?.CustomId?.StartsWith("empty_") == true)
            {
                return;
            }

            var context = new SocketInteractionContext(_discord, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ToString());
        }
        catch
        {
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(msg => msg.Result.DeleteAsync());
            }
        }
    }
}
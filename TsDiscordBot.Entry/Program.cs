using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TsDiscordBot.Core;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;

// logging
Envs.LogEnvironmentVariables();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddYamlFile("_config.yml", true);
    })
    .ConfigureServices(services =>
    {
        // Add singletons
        services.AddSingleton<DiscordSocketClient>(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.All,
            AlwaysDownloadUsers = true,
            MessageCacheSize = 100,
        }));
        services.AddSingleton<InteractionService>(provider =>
        {
            var client = provider.GetRequiredService<DiscordSocketClient>();
            return new InteractionService(client);
        });
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<OpenAIService>();
        services.AddSingleton<IUserCommandLimitService, UserCommandLimitService>();
        services.AddSingleton<IOpenAIImageService>(_ =>
        {
            var opts = new OpenAIImageOptions
            {
                ApiKey = Envs.OPENAI_API_KEY,
            };
            return OpenAIImageService.Create(opts);
        });

        services.AddLavalink();
        services.ConfigureLavalink(o =>
        {
            o.Label = "Lavalink";
            o.BaseAddress = new Uri(Envs.LAVALINK_BASE_ADDRESS);
            o.WebSocketUri = new Uri(Envs.LAVALINK_WS);
            o.Passphrase = Envs.LAVALINK_SERVER_PASSWORD;
            o.ResumptionOptions = new LavalinkSessionResumptionOptions(TimeSpan.FromSeconds(15));
            o.ReadyTimeout = TimeSpan.FromSeconds(15);
        });
        services.AddMemoryCache();

        // Add hosted services
        services.AddHostedService<InteractionHandlingService>();
        services.AddHostedService<DiscordStartupService>();
        services.AddHostedService<TriggerReactionService>();
        services.AddHostedService<BannedMessageCheckerService>();
        services.AddHostedService<NauAriService>();
        services.AddHostedService<TsumugiService>();
        services.AddHostedService<AutoMessageService>();
        services.AddHostedService<ReminderService>();
        services.AddHostedService<ImageReviseService>();
    })
    .Build();

await host.RunAsync();
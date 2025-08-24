using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Lavalink4NET.Discord.NET;
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
        services.AddSingleton<IDiscordClientWrapper>(provider =>
        {
            var client = provider.GetRequiredService<DiscordSocketClient>();
            return new DiscordClientWrapper(client);
        });
        services.AddSingleton<IAudioService, LavalinkNode>();
        services.AddSingleton(new LavalinkNodeOptions
        {
            RestUri = new Uri("http://localhost:2333"),
            WebSocketUri = new Uri("ws://localhost:2333"),
            Password = "youshallnotpass",
        });

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
        services.AddHostedService<LavalinkHostedService>();
    })
    .Build();

await host.RunAsync();
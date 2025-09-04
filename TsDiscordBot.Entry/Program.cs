using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
        services.AddSingleton<IDiscordBotClient>(provider => new DiscordSocketClientAdapter(provider.GetRequiredService<DiscordSocketClient>()));
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

        // Add hosted services
        services.AddHostedService<InteractionHandlingService>();
        services.AddHostedService<DiscordStartupService>();
        // Message delete related services should run before reaction handling
        services.AddHostedService<BannedMessageCheckerService>();
        services.AddHostedService<OverseaRelayService>();
        services.AddHostedService<AnonymousRelayService>();
        services.AddHostedService<TriggerReactionService>();
        services.AddHostedService<NauAriService>();
        services.AddHostedService<TsumugiService>();
        services.AddHostedService<AutoMessageService>();
        services.AddHostedService<ReminderService>();
        services.AddHostedService<ImageReviseService>();
        services.AddHostedService<AutoDeleteService>();
    })
    .Build();

await host.RunAsync();
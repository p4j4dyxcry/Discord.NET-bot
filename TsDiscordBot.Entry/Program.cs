using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TsDiscordBot.Core;
using TsDiscordBot.Core.Framework;
using TsDiscordBot.Core.HostedService;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Amuse;

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
        services.AddSingleton<RandTopicService>();
        services.AddSingleton<IOpenAIImageService>(_ =>
        {
            var opts = new OpenAIImageOptions
            {
                ApiKey = Envs.OPENAI_API_KEY,
            };
            return OpenAIImageService.Create(opts);
        });
        services.AddSingleton<IWebHookService, WebHookService>();
        services.AddSingleton<IMessageReceiver, MessageReceiverHub>();
        services.AddSingleton<MessageReceiverHub>();
        services.AddSingleton<IMessageReceiver>(sp => sp.GetRequiredService<MessageReceiverHub>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MessageReceiverHub>());
        services.AddSingleton<IAmuseCommandParser, AmuseCommandParser>();
        // Add hosted services
        services.AddHostedService<InteractionHandlingService>();
        services.AddHostedService<DiscordStartupService>();
        services.AddHostedService<BannedMessageCheckerService>();
        services.AddHostedService<OverseaRelayService>();
        services.AddHostedService<AnonymousRelayService>();
        services.AddHostedService<TriggerReactionService>();
        services.AddHostedService<NauAriService>();
        services.AddHostedService<TsumugiService>();
        services.AddHostedService<AutoMessageService>();
        services.AddHostedService<DailyTopicService>();
        services.AddHostedService<ReminderService>();
        services.AddHostedService<ImageReviseService>();
        services.AddHostedService<AutoDeleteService>();
        services.AddHostedService<BeRealService>();
        services.AddHostedService<AmuseMessageService>();
        services.AddHostedService<BlackJackBackgroundService>();
        services.AddHostedService<DiceBackgroundService>();
        services.AddHostedService<InviteTrackingService>();
    })
    .Build();

await host.RunAsync();
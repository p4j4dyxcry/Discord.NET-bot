using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;

namespace TsDiscordBot.Discord.Amuse;

public class AmuseMessageService : IHostedService
{
    private readonly IMessageReceiver _client;
    private readonly ILogger<AmuseMessageService> _logger;
    private readonly IAmuseCommandParser _parser;
    private readonly DatabaseService _databaseService;
    private IDisposable? _subscription;

    public AmuseMessageService(IMessageReceiver client, ILogger<AmuseMessageService> logger, IAmuseCommandParser parser, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _parser = parser;
        _databaseService = databaseService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = _client.OnReceivedSubscribe(
            OnMessageReceivedAsync,
            MessageConditions.NotFromBot.And(MessageConditions.NotDeleted),
            nameof(AmuseMessageService));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(IMessageData message, CancellationToken token)
    {
        var enabled = _databaseService
            .FindAll<AmuseChannel>(AmuseChannel.TableName)
            .Any(x => x.GuildId == message.GuildId && x.ChannelId == message.ChannelId);

        if (!enabled)
        {
            return;
        }

        var service = _parser.Parse(message.Content);
        if (service is null)
        {
            return;
        }

        await service.ExecuteAsync(message);
    }
}

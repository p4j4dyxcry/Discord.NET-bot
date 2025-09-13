using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class TriggerReactionService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<TriggerReactionService> _logger;
    private readonly DatabaseService _databaseService;
    private TriggerReactionPost[] _cache = Array.Empty<TriggerReactionPost>();
    private DateTime _lastExecuteDate = DateTime.Now;

    private readonly TimeSpan QuerySpan = TimeSpan.FromSeconds(5);

    public TriggerReactionService(DiscordSocketClient client, ILogger<TriggerReactionService> logger, DatabaseService databaseService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived += OnMessageReceivedAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived -= OnMessageReceivedAsync;
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            // Allow bot messages to trigger reactions; only skip non-guild channels
            if (message.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            ulong guildId = guildChannel.Guild.Id;

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
                .Where(x => x.GuildId == guildId)
                .Where(x => !string.IsNullOrWhiteSpace(x.TriggerWord));

            foreach (var config in settings)
            {
                if (!message.Content.Contains(config.TriggerWord, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Emote.TryParse(config.Reaction, out var emote))
                {
                    await message.AddReactionAsync(emote);
                }
                else if(Emoji.TryParse(config.Reaction,out var emoji))
                {
                    await message.AddReactionAsync(emoji,new RequestOptions());
                }
            }
        }
        catch(Exception e)
        {
            _logger.LogError(e,"Failed to reaction");
        }
    }
}
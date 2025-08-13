using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class AutoMessageService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AutoMessageService> _logger;
    private readonly DatabaseService _databaseService;
    private readonly OpenAIService _openAiService;

    public AutoMessageService(DiscordSocketClient client, ILogger<AutoMessageService> logger,
        DatabaseService databaseService, OpenAIService openAiService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
        _openAiService = openAiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var configs = _databaseService.FindAll<AutoMessageChannel>(AutoMessageChannel.TableName).ToArray();

                foreach (var config in configs)
                {
                    var next = config.LastPostedUtc.AddHours(config.IntervalHours);
                    if (DateTime.UtcNow >= next)
                    {
                        var guildId = config.GuildId;
                        SocketTextChannel? channel = _client.GetChannel(config.ChannelId) as SocketTextChannel
                            ?? _client.GetGuild(guildId)?.GetTextChannel(config.ChannelId);

                        if (channel is not null)
                        {
                            var previousMessages = (await channel.GetMessagesAsync(30).FlattenAsync())
                                .Where(x => !x.Author.IsBot)
                                .Select(ConvertFromDiscord)
                                .OrderBy(x => x.Date)
                                .ToArray();

                            var prompt = new OpenAIService.Message("会話を促す短いメッセージを作って", "system", DateTimeOffset.Now);
                            var message = await _openAiService.GetResponse(guildId, prompt, previousMessages);
                            await channel.SendMessageAsync(message);

                            config.LastPostedUtc = DateTime.UtcNow;
                            _databaseService.Update(AutoMessageChannel.TableName, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send auto message");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private OpenAIService.Message ConvertFromDiscord(IMessage message)
    {
        string author = message.Author is SocketGuildUser guildUser
            ? guildUser.Nickname ?? guildUser.Username
            : message.Author.Username;

        return new(message.Content, author, message.CreatedAt.ToLocalTime());
    }
}

using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Discord.Data;
using TsDiscordBot.Discord.Framework;
using TsDiscordBot.Discord.Services;
using TsDiscordBot.Discord.Utility;

namespace TsDiscordBot.Discord.HostedService;

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
                            var previousMessagesTasks = (await channel.GetMessagesAsync(20).FlattenAsync())
                                .Select(async x=> await MessageData.FromIMessageAsync(x))
                                .Select(async x => DiscordToOpenAIMessageConverter.ConvertFromDiscord(await x));

                            var previousMessages = await Task.WhenAll(previousMessagesTasks);

                            previousMessages = previousMessages
                                .OrderBy(x => x.Date)
                                .Where(x=>!x.FromTsumugi)
                                .Where(x=>!x.FromSystem)
                                .ToArray();

                            var prompt = new ConvertedMessage("会話を促す短いメッセージを独り言として作成してください。", "system", DateTimeOffset.Now,false,true);
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
}

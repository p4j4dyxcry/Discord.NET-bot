using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class DailyTopicService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DailyTopicService> _logger;
    private readonly DatabaseService _databaseService;
    private readonly RandTopicService _randTopicService;

    public DailyTopicService(DiscordSocketClient client, ILogger<DailyTopicService> logger,
        DatabaseService databaseService, RandTopicService randTopicService)
    {
        _client = client;
        _logger = logger;
        _databaseService = databaseService;
        _randTopicService = randTopicService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeZoneInfo jst;
        try
        {
            jst = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        }
        catch (TimeZoneNotFoundException)
        {
            jst = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var configs = _databaseService.FindAll<DailyTopicChannel>(DailyTopicChannel.TableName).ToArray();

                foreach (var config in configs)
                {
                    var nowJst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
                    var lastPostedJst = TimeZoneInfo.ConvertTimeFromUtc(config.LastPostedUtc, jst);
                    if (nowJst.Date > lastPostedJst.Date && nowJst.TimeOfDay >= config.PostAtJst)
                    {
                        var guildId = config.GuildId;
                        SocketTextChannel? channel = _client.GetChannel(config.ChannelId) as SocketTextChannel
                            ?? _client.GetGuild(guildId)?.GetTextChannel(config.ChannelId);

                        if (channel is not null)
                        {
                            var topic = _randTopicService.GetTopic(nowJst);
                            if (!string.IsNullOrEmpty(topic))
                            {
                                await channel.SendMessageAsync(topic);
                            }

                            config.LastPostedUtc = DateTime.UtcNow;
                            _databaseService.Update(DailyTopicChannel.TableName, config);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to send daily topic");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

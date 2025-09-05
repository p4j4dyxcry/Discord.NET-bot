using System;
using System.Linq;
using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class BeRealService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BeRealService> _logger;
    private readonly DatabaseService _databaseService;

    public BeRealService(DiscordSocketClient client, ILogger<BeRealService> logger, DatabaseService databaseService)
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
            if (message.Author.IsBot || message.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            var attachments = message.Attachments
                .Where(a => a.ContentType != null && a.ContentType.StartsWith("image/"))
                .ToArray();

            if (attachments.Length == 0)
            {
                return;
            }

            var channels = _databaseService.FindAll<BeRealChannel>(BeRealChannel.TableName);
            if (!channels.Any(x => x.ChannelId == guildChannel.Id))
            {
                return;
            }

            var previous = _databaseService.FindAll<BeRealPost>(BeRealPost.TableName)
                .Where(x => x.ChannelId == guildChannel.Id && x.UserId == message.Author.Id)
                .OrderBy(x => x.PostedAtUtc)
                .ToArray();

            if (previous.Length > 0)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"{message.Author.Username}さんの過去の投稿:");
                foreach (var p in previous)
                {
                    builder.AppendLine(p.ImageUrl);
                }
                await message.Channel.SendMessageAsync(builder.ToString());
            }

            foreach (var att in attachments)
            {
                var entry = new BeRealPost
                {
                    GuildId = guildChannel.Guild.Id,
                    ChannelId = guildChannel.Id,
                    UserId = message.Author.Id,
                    ImageUrl = att.Url,
                    PostedAtUtc = DateTime.UtcNow
                };
                _databaseService.Insert(BeRealPost.TableName, entry);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to process BeReal message");
        }
    }
}


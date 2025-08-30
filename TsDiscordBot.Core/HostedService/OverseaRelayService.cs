using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;

namespace TsDiscordBot.Core.HostedService;

public class OverseaRelayService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<OverseaRelayService> _logger;
    private readonly DatabaseService _databaseService;

    private OverseaChannel[] _channelCache = [];
    private OverseaUserSetting[] _userCache = [];
    private DateTime _lastQueryTime = DateTime.MinValue;
    private readonly TimeSpan _querySpan = TimeSpan.FromSeconds(15);

    public OverseaRelayService(DiscordSocketClient client, ILogger<OverseaRelayService> logger, DatabaseService databaseService)
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
            if (message.Source != MessageSource.User || message.Channel is not SocketGuildChannel)
            {
                return;
            }

            if (DateTime.Now - _lastQueryTime > _querySpan)
            {
                _channelCache = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName).ToArray();
                _userCache = _databaseService.FindAll<OverseaUserSetting>(OverseaUserSetting.TableName).ToArray();
                _lastQueryTime = DateTime.Now;
            }

            var current = _channelCache.FirstOrDefault(x => x.ChannelId == message.Channel.Id);
            if (current is null)
            {
                return;
            }

            var targets = _channelCache
                .Where(x => x.OverseaId == current.OverseaId && x.ChannelId != current.ChannelId)
                .ToArray();

            if (!targets.Any())
            {
                return;
            }

            var userSetting = _userCache.FirstOrDefault(x => x.UserId == message.Author.Id);
            bool anonymous = userSetting?.IsAnonymous ?? true;
            string username;
            string? avatarUrl = null;

            if (anonymous)
            {
                string hash = (message.Author.Id % 10000).ToString("D4");
                username = string.IsNullOrEmpty(userSetting?.AnonymousName)
                    ? $"どこかのサバの誰かさん#{hash}"
                    : userSetting.AnonymousName!;
                avatarUrl = userSetting?.AnonymousAvatarUrl;
            }
            else
            {
                username = message.Author.Username;
                avatarUrl = message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl();
            }

            string content = message.Content;
            if (message.Attachments.Any())
            {
                var urls = string.Join("\n", message.Attachments.Select(a => a.Url));
                content = string.IsNullOrWhiteSpace(content) ? urls : content + "\n" + urls;
            }

            foreach (var target in targets)
            {
                if (await _client.GetChannelAsync(target.ChannelId) is ITextChannel channel)
                {
                    var webhook = await channel.CreateWebhookAsync("oversea-relay");
                    var client = new DiscordWebhookClient(webhook);
                    try
                    {
                        await client.SendMessageAsync(content, username: username, avatarUrl: avatarUrl);
                    }
                    finally
                    {
                        await webhook.DeleteAsync();
                        client.Dispose();
                    }
                }
            }

            await message.DeleteAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to relay oversea message.");
        }
    }
}


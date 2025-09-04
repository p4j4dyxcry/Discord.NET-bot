using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TsDiscordBot.Core.Data;
using TsDiscordBot.Core.Services;
using TsDiscordBot.Core.Utility;

namespace TsDiscordBot.Core.HostedService;

public class AnonymousRelayService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AnonymousRelayService> _logger;
    private readonly DatabaseService _databaseService;

    private AnonymousGuildUserSetting[] _userCache = [];
    private OverseaChannel[] _overseaChannelCache = [];
    private DateTime _lastQueryTime = DateTime.MinValue;
    private readonly TimeSpan _querySpan = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<ulong, DiscordWebhookClient> _webhookCache = new();

    private async Task<DiscordWebhookClient> GetOrCreateWebhookClientAsync(ITextChannel channel)
    {
        if (_webhookCache.TryGetValue(channel.Id, out var cached))
        {
            return cached;
        }

        var hooks = await channel.GetWebhooksAsync();
        var hook = hooks.FirstOrDefault(h => h.Name == "anonymous-relay")
                   ?? await channel.CreateWebhookAsync("anonymous-relay");

        var client = new DiscordWebhookClient(hook);
        _webhookCache[channel.Id] = client;
        return client;
    }

    public AnonymousRelayService(DiscordSocketClient client, ILogger<AnonymousRelayService> logger, DatabaseService databaseService)
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
            if (message.Source != MessageSource.User || message.Channel is not SocketGuildChannel guildChannel)
            {
                return;
            }

            if (DateTime.Now - _lastQueryTime > _querySpan)
            {
                _userCache = _databaseService.FindAll<AnonymousGuildUserSetting>(AnonymousGuildUserSetting.TableName).ToArray();
                _overseaChannelCache = _databaseService.FindAll<OverseaChannel>(OverseaChannel.TableName).ToArray();
                _lastQueryTime = DateTime.Now;
            }

            if (_overseaChannelCache.Any(x => x.ChannelId == message.Channel.Id))
            {
                return; // Oversea relay has priority
            }

            var setting = _userCache.FirstOrDefault(x => x.GuildId == guildChannel.Guild.Id && x.UserId == message.Author.Id);
            if (setting is null || !setting.IsAnonymous)
            {
                return;
            }

            var profile = AnonymousProfileProvider.GetProfile(message.Author.Id);
            var discriminator = AnonymousProfileProvider.GetDiscriminator(message.Author.Id);
            var baseName = string.IsNullOrEmpty(setting.AnonymousName) ? profile.Name : setting.AnonymousName!;
            baseName = UserNameFixLogic.Fix(baseName);
            var username = $"{baseName}#{discriminator}";
            var avatarUrl = setting.AnonymousAvatarUrl ?? profile.AvatarUrl;

            string? content = string.IsNullOrWhiteSpace(message.Content) ? null : message.Content;

            List<(string FileName, string ContentType, byte[] Data)>? attachments = null;
            if (message.Attachments.Any())
            {
                attachments = new List<(string, string, byte[])>();
                using var http = new HttpClient();
                foreach (var a in message.Attachments)
                {
                    try
                    {
                        var data = await http.GetByteArrayAsync(a.Url);
                        attachments.Add((a.Filename, a.ContentType, data));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to download attachment {Url}", a.Url);
                    }
                }
            }

            try
            {
                await message.DeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete original message");
            }

            var client = await GetOrCreateWebhookClientAsync((ITextChannel)message.Channel);
            if (attachments is { Count: > 0 })
            {
                var files = attachments
                    .Select(a => new FileAttachment(new MemoryStream(a.Data), a.FileName, a.ContentType))
                    .ToList();
                try
                {
                    await client.SendFilesAsync(files, text: content, username: username, avatarUrl: avatarUrl);
                }
                finally
                {
                    foreach (var f in files)
                        f.Stream.Dispose();
                }
            }
            else
            {
                await client.SendMessageAsync(content ?? string.Empty, username: username, avatarUrl: avatarUrl);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to relay anonymous message.");
        }
    }
}
